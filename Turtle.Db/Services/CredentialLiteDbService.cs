using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Gaia.Helpers;
using Gaia.Models;
using Gaia.Services;
using Nestor.Db.Helpers;
using Nestor.Db.LiteDb.Services;
using Nestor.Db.Models;
using Nestor.Db.Services;
using Turtle.Contract.Helpers;
using Turtle.Contract.Models;
using Turtle.Contract.Services;
using UltraLiteDB;

namespace Turtle.Db.Services;

public sealed class CredentialLiteDbService
    : LiteDbService<TurtleGetRequest, TurtlePostRequest, TurtleGetResponse, TurtlePostResponse>,
        ICredentialDbService,
        ICredentialDbCache
{
    public CredentialLiteDbService(
        IDatabaseFactory factory,
        IFactory<DbValues> dbValuesFactory,
        IFactory<DbServiceOptions> factoryOptions
    )
        : base(factory, nameof(CredentialEntity))
    {
        _dbValuesFactory = dbValuesFactory;
        _factoryOptions = factoryOptions;
    }

    public override ConfiguredValueTaskAwaitable<TurtleGetResponse> GetAsync(
        TurtleGetRequest request,
        CancellationToken ct
    )
    {
        using var database = Factory.Create();
        var collection = database.GetCredentialEntityCollection();
        var credentials = collection.FindAll().Select(x => x.ToCredentialEntity()).ToArray();
        var response = CreateResponse(request, credentials);

        return TaskHelper.FromResult(response);
    }

    public ConfiguredValueTaskAwaitable UpdateAsync(TurtlePostRequest source, CancellationToken ct)
    {
        return UpdateCore(source, ct).ConfigureAwait(false);
    }

    private async ValueTask UpdateCore(TurtlePostRequest source, CancellationToken ct)
    {
        await ExecuteAsync(Guid.NewGuid(), new(), source, ct);
    }

    public ConfiguredValueTaskAwaitable UpdateAsync(TurtleGetResponse source, CancellationToken ct)
    {
        var entities = GetCredentialEntities(source);

        if (entities.Length == 0)
        {
            return TaskHelper.ConfiguredCompletedTask;
        }

        using var database = Factory.Create();
        var collection = database.GetCredentialEntityCollection();

        var exists = entities
            .Where(x => collection.Exists(Query.EQ("_id", x.Id)))
            .Select(x => x.Id)
            .ToArray();

        var updates = entities
            .Where(x => exists.Contains(x.Id))
            .Select(x => x.ToBsonDocument())
            .ToArray();

        var inserts = entities
            .Where(x => !exists.Contains(x.Id))
            .Select(x => x.ToBsonDocument())
            .ToArray();

        if (inserts.Length != 0)
        {
            collection.Insert(inserts);
        }

        if (updates.Length != 0)
        {
            collection.Update(updates);
        }

        if (source.Selectors is not null)
        {
            var ids = source
                .Selectors.SelectMany(x => GetCredentialEntities(x).Select(y => y.Id))
                .ToArray();

            var deleteIds = collection
                .Find(Query.Not(Query.In("_id", ids.Select(x => new BsonValue(x)))))
                .Select(x => x["_id"])
                .ToArray();

            if (deleteIds.Length != 0)
            {
                collection.Delete(Query.In("_id", deleteIds));
            }
        }

        database.SaveChanges();

        return TaskHelper.ConfiguredCompletedTask;
    }

    protected override ConfiguredValueTaskAwaitable ExecuteAsync(
        Guid idempotentId,
        TurtlePostResponse response,
        TurtlePostRequest request,
        CancellationToken ct
    )
    {
        var dbValues = _dbValuesFactory.Create();
        var edits = new AutoDictionary<Guid, EditCredentialEntity>();
        using var database = Factory.Create();
        var collection = database.GetCredentialEntityCollection();
        var options = _factoryOptions.Create();
        Create(database, collection, options, idempotentId, request.CreateCredentials, dbValues);
        Edit(request.Edits, edits);

        UpdateChangeOrder(
            database,
            collection,
            request.ChangeOrders,
            response.ValidationErrors,
            edits
        );

        database.EditEntities(
            dbValues.UserId.ToString(),
            idempotentId,
            options.IsUseEvents,
            edits.ToItemsArray()
        );

        Delete(database, options, idempotentId, request.DeleteIds, dbValues);
        database.SaveChanges();

        return TaskHelper.ConfiguredCompletedTask;
    }

    private readonly IFactory<DbValues> _dbValuesFactory;
    private readonly IFactory<DbServiceOptions> _factoryOptions;

    private void AddParents(
        TurtleGetResponse response,
        Guid rootId,
        FrozenDictionary<Guid, CredentialEntity> credentials
    )
    {
        var credential = credentials[rootId].ToCredential();
        response.Parents.Add(rootId, [credential]);

        if (credential.ParentId is null)
        {
            return;
        }

        AddParents(response, rootId, credential.ParentId.Value, credentials);
    }

    private void AddParents(
        TurtleGetResponse response,
        Guid rootId,
        Guid parentId,
        FrozenDictionary<Guid, CredentialEntity> credentials
    )
    {
        var credential = credentials[parentId].ToCredential();
        response.Parents[rootId].Add(credential);

        if (credential.ParentId is null)
        {
            return;
        }

        AddParents(response, rootId, credential.ParentId.Value, credentials);
    }

    private TurtleGetResponse CreateResponse(
        TurtleGetRequest request,
        CredentialEntity[] credentials
    )
    {
        var response = new TurtleGetResponse();
        var dictionary = credentials.ToDictionary(x => x.Id).ToFrozenDictionary();
        var roots = dictionary.Values.Where(x => x.ParentId is null).ToArray();

        if (request.IsGetBookmarks)
        {
            response.Bookmarks = dictionary
                .Values.Where(x => x.IsBookmark)
                .Select(x => x.ToCredential())
                .ToArray();
        }

        if (request.IsGetSelectors)
        {
            response.Selectors = roots
                .Select(x => new CredentialSelector
                {
                    Item = x.ToCredential(),
                    Children = GetToDoSelectorItems(credentials, x.Id).ToArray(),
                })
                .ToArray();
        }

        if (request.IsGetRoots)
        {
            response.Roots = roots.Select(x => x.ToCredential()).ToArray();
        }

        foreach (var id in request.GetChildrenIds)
        {
            response.Children.Add(
                id,
                credentials.Where(y => y.ParentId == id).Select(x => x.ToCredential()).ToList()
            );
        }

        foreach (var id in request.GetParentsIds)
        {
            AddParents(response, id, dictionary);
            response.Parents[id].Reverse();
        }

        return response;
    }

    private CredentialSelector[] GetToDoSelectorItems(CredentialEntity[] items, Guid id)
    {
        var children = items.Where(x => x.ParentId == id).OrderBy(x => x.OrderIndex).ToArray();

        var result = new CredentialSelector[children.Length];

        for (var i = 0; i < children.Length; i++)
        {
            result[i] = new()
            {
                Item = children[i].ToCredential(),
                Children = GetToDoSelectorItems(items, children[i].Id),
            };
        }

        return result;
    }

    private void Delete(
        IDatabase database,
        DbServiceOptions options,
        Guid idempotentId,
        Guid[] ids,
        DbValues dbValues
    )
    {
        if (ids.Length == 0)
        {
            return;
        }

        database.DeleteEntities(dbValues.UserId.ToString(), idempotentId, options.IsUseEvents, ids);
    }

    private void Edit(EditCredential[] edits, AutoDictionary<Guid, EditCredentialEntity> dictionary)
    {
        foreach (var edit in edits)
        {
            dictionary.AddRange(edit.ToEditCredentialEntities());
        }
    }

    private void Create(
        IDatabase database,
        UltraLiteCollection<BsonDocument> collection,
        DbServiceOptions options,
        Guid idempotentId,
        Credential[] creates,
        DbValues dbValues
    )
    {
        if (creates.Length == 0)
        {
            return;
        }

        var entities = new CredentialEntity[creates.Length];

        for (var index = 0; index < creates.Length; index++)
        {
            var credential = creates[index];

            var query = credential.ParentId is null
                ? Query.EQ(nameof(CredentialEntity.ParentId), BsonValue.Null)
                : Query.EQ(nameof(CredentialEntity.ParentId), credential.ParentId);

            var siblingCount = collection.Count(query);
            var entity = credential.ToCredentialEntity();
            entity.OrderIndex = (uint)siblingCount + 1;
            entities[index] = entity;
        }

        database.AddEntities($"{dbValues.UserId}", idempotentId, options.IsUseEvents, entities);
    }

    private void UpdateChangeOrder(
        IDatabase database,
        UltraLiteCollection<BsonDocument> collection,
        ChangeOrder[] changeOrders,
        List<ValidationError> errors,
        AutoDictionary<Guid, EditCredentialEntity> edits
    )
    {
        if (changeOrders.Length == 0)
        {
            return;
        }

        var allInsertIds = changeOrders.SelectMany(x => x.InsertIds).Distinct().ToArray();

        var insertItems = collection
            .Find(Query.In("_id", allInsertIds.Select(x => new BsonValue(x))))
            .Select(x => x.ToCredentialEntity())
            .ToArray();

        var insertItemsDictionary = insertItems.ToDictionary(x => x.Id).ToFrozenDictionary();
        var startIds = changeOrders.Select(x => x.StartId).Distinct().ToArray();

        var startItems = collection
            .Find(Query.In("_id", startIds.Select(x => new BsonValue(x))))
            .Select(x => x.ToCredentialEntity())
            .ToArray();

        var startItemsDictionary = startItems.ToDictionary(x => x.Id).ToFrozenDictionary();

        var parentItems = startItems
            .Select(x => x.ParentId)
            .WhereNotNullStruct()
            .Distinct()
            .ToArray();

        var allSiblings = collection
            .Find(
                Query.In(
                    nameof(CredentialEntity.ParentId),
                    parentItems.Select(x => new BsonValue(x))
                )
            )
            .Select(x => x.ToCredentialEntity())
            .ToArray();

        if (startItems.Any(x => x.ParentId is null))
        {
            allSiblings = collection
                .Find(Query.EQ(nameof(CredentialEntity.ParentId), BsonValue.Null))
                .Select(x => x.ToCredentialEntity())
                .Concat(allSiblings)
                .ToArray();
        }

        for (var index = 0; index < changeOrders.Length; index++)
        {
            var changeOrder = changeOrders[index];

            var inserts = changeOrder
                .InsertIds.Select(x => insertItemsDictionary[x])
                .OrderBy(x => x.OrderIndex)
                .ToFrozenSet();

            if (!startItemsDictionary.TryGetValue(changeOrder.StartId, out var item))
            {
                errors.Add(new NotFoundValidationError(changeOrder.StartId.ToString()));

                continue;
            }

            var siblings = allSiblings
                .Where(x => x.ParentId == item.ParentId && !changeOrder.InsertIds.Contains(x.Id))
                .OrderBy(x => x.OrderIndex)
                .ToList();

            var startItem = siblings.First(x => x.Id == changeOrder.StartId);
            var startIndex = siblings.IndexOf(startItem);
            siblings.InsertRange(changeOrder.IsAfter ? startIndex + 1 : startIndex, inserts);

            for (var i = 0; i < siblings.Count; i++)
            {
                var isEditOrderIndex = siblings[i].OrderIndex != i + 1;
                var isEditParentId = siblings[i].ParentId != startItem.ParentId;

                if (isEditOrderIndex || isEditParentId)
                {
                    var edit = edits.GetItem(siblings[i].Id);
                    edit.IsEditOrderIndex = isEditOrderIndex;
                    edit.IsEditParentId = isEditParentId;
                    edit.OrderIndex = (uint)i + 1;
                    edit.ParentId = item.ParentId;
                }
            }
        }
    }

    private SqlQuery CreateSqlForAllChildren(Guid[] ids, DbSession session)
    {
        return new(
            $$"""
            WITH RECURSIVE hierarchy(
                     Id,
                     Name,
                     Login,
                     Key,
                     IsAvailableUpperLatin,
                     IsAvailableLowerLatin,
                     IsAvailableNumber,
                     IsAvailableSpecialSymbols,
                     CustomAvailableCharacters,
                     Length,
                     Regex,
                     Type,
                     OrderIndex,
                     ParentId
                 ) AS (
                     SELECT
                     Id,
                     Name,
                     Login,
                     Key,
                     IsAvailableUpperLatin,
                     IsAvailableLowerLatin,
                     IsAvailableNumber,
                     IsAvailableSpecialSymbols,
                     CustomAvailableCharacters,
                     Length,
                     Regex,
                     Type,
                     OrderIndex,
                     ParentId
                     FROM Credentials
                     WHERE Id IN ({{ids.ToParameterNames("Id")}})

                     UNION ALL

                     SELECT
                     t.Id,
                     t.Name,
                     t.Login,
                     t.Key,
                     t.IsAvailableUpperLatin,
                     t.IsAvailableLowerLatin,
                     t.IsAvailableNumber,
                     t.IsAvailableSpecialSymbols,
                     t.CustomAvailableCharacters,
                     t.Length,
                     t.Regex,
                     t.Type,
                     t.OrderIndex,
                     t.ParentId
                     FROM Credentials t
                     INNER JOIN hierarchy h ON t.ParentId = h.Id
                 )
                 SELECT * FROM hierarchy
            """,
            ids.ToQueryParameters("Id")
        );
    }

    private static CredentialEntity[] GetCredentialEntities(TurtleGetResponse source)
    {
        return source
            .Children.SelectMany(x => x.Value)
            .Select(x => x.ToCredentialEntity())
            .Concat(
                source.Selectors?.SelectMany(GetCredentialEntities)
                    ?? Enumerable.Empty<CredentialEntity>()
            )
            .Concat(source.Parents.SelectMany(x => x.Value).Select(x => x.ToCredentialEntity()))
            .Concat(
                source.Roots?.Select(x => x.ToCredentialEntity())
                    ?? Enumerable.Empty<CredentialEntity>()
            )
            .ToArray();
    }

    private static IEnumerable<CredentialEntity> GetCredentialEntities(CredentialSelector selector)
    {
        yield return selector.Item.ToCredentialEntity();

        foreach (var child in selector.Children)
        {
            foreach (var item in GetCredentialEntities(child))
            {
                yield return item;
            }
        }
    }
}
