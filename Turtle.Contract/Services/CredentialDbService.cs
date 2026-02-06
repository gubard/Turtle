using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Gaia.Helpers;
using Gaia.Models;
using Gaia.Services;
using Nestor.Db.Helpers;
using Nestor.Db.Models;
using Nestor.Db.Services;
using Turtle.Contract.Helpers;
using Turtle.Contract.Models;

namespace Turtle.Contract.Services;

public interface ICredentialHttpService
    : ICredentialService,
        IHttpService<TurtleGetRequest, TurtlePostRequest, TurtleGetResponse, TurtlePostResponse>;

public interface ICredentialService
    : IService<TurtleGetRequest, TurtlePostRequest, TurtleGetResponse, TurtlePostResponse>;

public interface ICredentialDbCache : IDbCache<TurtlePostRequest, TurtleGetResponse>;

public interface ICredentialDbService
    : ICredentialService,
        IDbService<TurtleGetRequest, TurtlePostRequest, TurtleGetResponse, TurtlePostResponse>;

public sealed class CredentialDbService
    : DbService<TurtleGetRequest, TurtlePostRequest, TurtleGetResponse, TurtlePostResponse>,
        ICredentialDbService,
        ICredentialDbCache
{
    private readonly IFactory<DbValues> _dbValuesFactory;
    private readonly IFactory<DbServiceOptions> _factoryOptions;

    public CredentialDbService(
        IDbConnectionFactory factory,
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
        return GetCore(request, ct).ConfigureAwait(false);
    }

    private async ValueTask<TurtleGetResponse> GetCore(
        TurtleGetRequest request,
        CancellationToken ct
    )
    {
        await using var session = await Factory.CreateSessionAsync(ct);
        var query = CredentialsExt.SelectQuery;
        var credentials = await session.GetCredentialsAsync(query, ct);
        var response = CreateResponse(request, credentials);

        return response;
    }

    protected override ConfiguredValueTaskAwaitable<TurtlePostResponse> ExecuteAsync(
        Guid idempotentId,
        TurtlePostResponse response,
        TurtlePostRequest request,
        CancellationToken ct
    )
    {
        return ExecuteCore(idempotentId, response, request, ct).ConfigureAwait(false);
    }

    private async ValueTask<TurtlePostResponse> ExecuteCore(
        Guid idempotentId,
        TurtlePostResponse response,
        TurtlePostRequest request,
        CancellationToken ct
    )
    {
        var dbValues = _dbValuesFactory.Create();
        var edits = new AutoDictionary<Guid, EditCredentialEntity>();
        await using var session = await Factory.CreateSessionAsync(ct);
        var options = _factoryOptions.Create();
        await CreateAsync(session, options, idempotentId, request.CreateCredentials, dbValues, ct);
        Edit(request.Edits, edits);

        await ChangeOrderAsync(session, request.ChangeOrders, response.ValidationErrors, edits, ct);

        await session.EditEntitiesAsync(
            dbValues.UserId.ToString(),
            idempotentId,
            options.IsUseEvents,
            edits.ToItemsArray(),
            ct
        );

        await DeleteAsync(session, options, idempotentId, request.DeleteIds, dbValues, ct);
        await session.CommitAsync(ct);

        return response;
    }

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

    private ConfiguredValueTaskAwaitable DeleteAsync(
        DbSession session,
        DbServiceOptions options,
        Guid idempotentId,
        Guid[] ids,
        DbValues dbValues,
        CancellationToken ct
    )
    {
        if (ids.Length == 0)
        {
            return TaskHelper.ConfiguredCompletedTask;
        }

        return session.DeleteEntitiesAsync(
            dbValues.UserId.ToString(),
            idempotentId,
            options.IsUseEvents,
            ids,
            ct
        );
    }

    private void Edit(EditCredential[] edits, AutoDictionary<Guid, EditCredentialEntity> dictionary)
    {
        foreach (var edit in edits)
        {
            dictionary.AddRange(edit.ToEditCredentialEntities());
        }
    }

    private async ValueTask CreateAsync(
        DbSession session,
        DbServiceOptions options,
        Guid idempotentId,
        Credential[] creates,
        DbValues dbValues,
        CancellationToken ct
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
            int siblingCount;

            if (credential.ParentId is null)
            {
                siblingCount = await session.ExecuteScalarInt32Async(
                    new(CredentialsExt.SelectCountQuery + " WHERE ParentId IS NULL"),
                    ct
                );
            }
            else
            {
                siblingCount = await session.ExecuteScalarInt32Async(
                    new(
                        CredentialsExt.SelectCountQuery + " WHERE ParentId = @ParentId",
                        session.CreateParameter("@ParentId", credential.ParentId)
                    ),
                    ct
                );
            }

            var entity = credential.ToCredentialEntity();
            entity.OrderIndex = (uint)siblingCount + 1;
            entities[index] = entity;
        }

        await session.AddEntitiesAsync(
            $"{dbValues.UserId}",
            idempotentId,
            options.IsUseEvents,
            entities,
            ct
        );
    }

    private async ValueTask ChangeOrderAsync(
        DbSession session,
        ChangeOrder[] changeOrders,
        List<ValidationError> errors,
        AutoDictionary<Guid, EditCredentialEntity> edits,
        CancellationToken ct
    )
    {
        if (changeOrders.Length == 0)
        {
            return;
        }

        var allInsertIds = changeOrders.SelectMany(x => x.InsertIds).Distinct().ToArray();
        var insertItems = await session.GetCredentialsAsync(allInsertIds, ct);
        var insertItemsDictionary = insertItems.ToDictionary(x => x.Id).ToFrozenDictionary();
        var startIds = changeOrders.Select(x => x.StartId).Distinct().ToArray();
        var startItems = await session.GetCredentialsAsync(startIds, ct);
        var startItemsDictionary = startItems.ToDictionary(x => x.Id).ToFrozenDictionary();

        var parentItems = startItems
            .Select(x => x.ParentId)
            .WhereNotNullStruct()
            .Distinct()
            .ToArray();

        var allSiblings = await session.GetCredentialsAsync(
            new SqlQuery(
                CredentialsExt.SelectQuery
                    + $" WHERE ParentId IN ({parentItems.ToParameterNames("ParentId")})",
                session.ToDbParameters(parentItems, "ParentId")
            ),
            ct
        );

        if (startItems.Any(x => x.ParentId is null))
        {
            var siblingsRoots = await session.GetCredentialsAsync(
                CredentialsExt.SelectQuery + " WHERE ParentId IS NULL",
                ct
            );

            allSiblings = allSiblings.Concat(siblingsRoots).ToArray();
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
            session.ToDbParameters(ids, "Id")
        );
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
        return UpdateCore(source, ct).ConfigureAwait(false);
    }

    public async ValueTask UpdateCore(TurtleGetResponse source, CancellationToken ct)
    {
        await using var session = await Factory.CreateSessionAsync(ct);
        var entities = GetCredentialEntities(source);

        if (entities.Length == 0)
        {
            return;
        }

        var exists = await session.IsExistsAsync(entities, ct);

        var updateQueries = entities
            .Where(x => exists.Contains(x.Id))
            .Select(x => x.CreateUpdateCredentialsQuery(session))
            .ToArray();

        var inserts = entities.Where(x => !exists.Contains(x.Id)).ToArray();

        if (inserts.Length != 0)
        {
            await session.ExecuteNonQueryAsync(inserts.CreateInsertQuery(session), ct);
        }

        foreach (var query in updateQueries)
        {
            await session.ExecuteNonQueryAsync(query, ct);
        }

        await session.CommitAsync(ct);
    }

    private static CredentialEntity[] GetCredentialEntities(TurtleGetResponse source)
    {
        return source
            .Children.SelectMany(x => x.Value)
            .Select(x => x.ToCredentialEntity())
            .Concat(source.Parents.SelectMany(x => x.Value).Select(x => x.ToCredentialEntity()))
            .Concat(
                source.Roots?.Select(x => x.ToCredentialEntity())
                    ?? Enumerable.Empty<CredentialEntity>()
            )
            .ToArray();
    }
}
