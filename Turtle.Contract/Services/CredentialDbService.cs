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
        var query = CreateQuery(request, session);
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
        var editEntities = new List<EditCredentialEntity>();
        await using var session = await Factory.CreateSessionAsync(ct);
        var options = _factoryOptions.Create();
        await CreateAsync(session, options, idempotentId, request.CreateCredentials, dbValues, ct);
        Edit(request.Edits, editEntities);
        ChangeOrder(session, request.ChangeOrders, response.ValidationErrors, editEntities);

        await session.EditEntitiesAsync(
            dbValues.UserId.ToString(),
            idempotentId,
            options.IsUseEvents,
            editEntities.ToArray(),
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
        var credential = ToCredential(credentials[rootId]);
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
        var credential = ToCredential(credentials[parentId]);
        response.Parents[rootId].Add(credential);

        if (credential.ParentId is null)
        {
            return;
        }

        AddParents(response, rootId, credential.ParentId.Value, credentials);
    }

    private static Credential ToCredential(CredentialEntity entity)
    {
        return new()
        {
            Id = entity.Id,
            Name = entity.Name,
            CustomAvailableCharacters = entity.CustomAvailableCharacters,
            IsAvailableLowerLatin = entity.IsAvailableLowerLatin,
            IsAvailableNumber = entity.IsAvailableNumber,
            IsAvailableSpecialSymbols = entity.IsAvailableSpecialSymbols,
            IsAvailableUpperLatin = entity.IsAvailableUpperLatin,
            Key = entity.Key,
            Length = entity.Length,
            Regex = entity.Regex,
            Type = entity.Type,
            Login = entity.Login,
            OrderIndex = entity.OrderIndex,
            ParentId = entity.ParentId,
        };
    }

    private TurtleGetResponse CreateResponse(
        TurtleGetRequest request,
        CredentialEntity[] credentials
    )
    {
        var response = new TurtleGetResponse();
        var credentialsDictionary = credentials.ToDictionary(x => x.Id).ToFrozenDictionary();

        if (request.IsGetRoots)
        {
            response.Roots = credentials
                .Where(x => x.ParentId is null)
                .Select(ToCredential)
                .ToArray();
        }

        foreach (var id in request.GetChildrenIds)
        {
            response.Children.Add(
                id,
                credentials.Where(y => y.ParentId == id).Select(ToCredential).ToList()
            );
        }

        foreach (var id in request.GetParentsIds)
        {
            AddParents(response, id, credentialsDictionary);
            response.Parents[id].Reverse();
        }

        return response;
    }

    private SqlQuery CreateQuery(TurtleGetRequest request, DbSession session)
    {
        var queries = new List<SqlQuery>();

        if (request.GetParentsIds.Length != 0)
        {
            var sql = CreateSqlForAllChildren(request.GetParentsIds, session);
            queries.Add(sql);
        }

        if (request.IsGetRoots)
        {
            queries.Add(
                CredentialsExt.SelectQuery + $" WHERE {nameof(CredentialEntity.ParentId)} IS NULL"
            );
        }

        if (request.GetChildrenIds.Length != 0)
        {
            queries.Add(
                new(
                    CredentialsExt.SelectQuery
                        + $" WHERE {nameof(CredentialEntity.ParentId)} IN ({request.GetChildrenIds.ToParameterNames(nameof(CredentialEntity.ParentId))})",
                    session.ToDbParameters(
                        request.GetChildrenIds,
                        nameof(CredentialEntity.ParentId)
                    )
                )
            );
        }

        var result = new SqlQuery(
            queries
                .Select(x => x.Sql)
                .JoinString($"{Environment.NewLine}UNION ALL{Environment.NewLine}"),
            queries.SelectMany(x => x.Parameters).ToArray()
        );

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

    private void Edit(EditCredential[] edits, List<EditCredentialEntity> editEntities)
    {
        foreach (var edit in edits)
        {
            foreach (var id in edit.Ids)
            {
                editEntities.Add(
                    new(id)
                    {
                        CustomAvailableCharacters = edit.CustomAvailableCharacters,
                        IsEditCustomAvailableCharacters = edit.IsEditCustomAvailableCharacters,
                        IsAvailableLowerLatin = edit.IsAvailableLowerLatin,
                        IsEditIsAvailableLowerLatin = edit.IsEditIsAvailableLowerLatin,
                        IsAvailableNumber = edit.IsAvailableNumber,
                        IsEditIsAvailableNumber = edit.IsEditIsAvailableNumber,
                        IsAvailableSpecialSymbols = edit.IsAvailableSpecialSymbols,
                        IsEditIsAvailableSpecialSymbols = edit.IsEditIsAvailableSpecialSymbols,
                        IsAvailableUpperLatin = edit.IsAvailableUpperLatin,
                        IsEditIsAvailableUpperLatin = edit.IsEditIsAvailableUpperLatin,
                        Key = edit.Key,
                        IsEditKey = edit.IsEditKey,
                        Length = edit.Length,
                        IsEditLength = edit.IsEditLength,
                        Login = edit.Login,
                        IsEditLogin = edit.IsEditLogin,
                        Name = edit.Name,
                        IsEditName = edit.IsEditName,
                        Regex = edit.Regex,
                        IsEditRegex = edit.IsEditRegex,
                        Type = edit.Type,
                        IsEditType = edit.IsEditType,
                        ParentId = edit.ParentId,
                        IsEditParentId = edit.IsEditParentId,
                    }
                );
            }
        }
    }

    private ConfiguredValueTaskAwaitable CreateAsync(
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
            return TaskHelper.ConfiguredCompletedTask;
        }

        var entities = new Span<CredentialEntity>(new CredentialEntity[creates.Length]);

        for (var index = 0; index < creates.Length; index++)
        {
            var createCredential = creates[index];
            entities[index] = new()
            {
                CustomAvailableCharacters = createCredential.CustomAvailableCharacters,
                IsAvailableLowerLatin = createCredential.IsAvailableLowerLatin,
                Id = createCredential.Id,
                IsAvailableNumber = createCredential.IsAvailableNumber,
                IsAvailableSpecialSymbols = createCredential.IsAvailableSpecialSymbols,
                IsAvailableUpperLatin = createCredential.IsAvailableUpperLatin,
                Key = createCredential.Key,
                Length = createCredential.Length,
                Login = createCredential.Login,
                Name = createCredential.Name,
                Regex = createCredential.Regex,
                Type = createCredential.Type,
                ParentId = createCredential.ParentId,
            };
        }

        return session.AddEntitiesAsync(
            $"{dbValues.UserId}",
            idempotentId,
            options.IsUseEvents,
            entities.ToArray(),
            ct
        );
    }

    private void ChangeOrder(
        DbSession session,
        ChangeOrder[] changeOrders,
        List<ValidationError> errors,
        List<EditCredentialEntity> editEntities
    )
    {
        if (changeOrders.Length == 0)
        {
            return;
        }

        var insertIds = changeOrders.SelectMany(x => x.InsertIds).Distinct().ToArray();
        var insertItems = session.GetCredentials(insertIds);
        var insertItemsDictionary = insertItems.ToDictionary(x => x.Id).ToFrozenDictionary();
        var startIds = changeOrders.Select(x => x.StartId).Distinct().ToArray();
        var startItems = session.GetCredentials(startIds);
        var startItemsDictionary = startItems.ToDictionary(x => x.Id).ToFrozenDictionary();

        var parentItems = startItems
            .Select(x => x.ParentId)
            .WhereNotNullStruct()
            .Distinct()
            .ToArray();
        var siblings = session.GetCredentials(parentItems);

        for (var index = 0; index < changeOrders.Length; index++)
        {
            var changeOrder = changeOrders[index];

            var inserts = changeOrder.InsertIds.Select(x => insertItemsDictionary[x]).ToArray();

            if (!startItemsDictionary.TryGetValue(changeOrder.StartId, out var item))
            {
                errors.Add(new NotFoundValidationError(changeOrder.StartId.ToString()));

                continue;
            }

            var startIndex = changeOrder.IsAfter ? item.OrderIndex + 1 : item.OrderIndex;
            var items = siblings.Where(x => x.ParentId == item.ParentId).OrderBy(x => x.OrderIndex);

            var usedItems = changeOrder.IsAfter
                ? items.Where(x => x.OrderIndex > item.OrderIndex)
                : items.Where(x => x.OrderIndex >= item.OrderIndex);

            var newOrder = inserts
                .Concat(usedItems.Where(x => !insertIds.Contains(x.Id)))
                .ToFrozenSet();

            foreach (var newItem in newOrder)
            {
                editEntities.Add(
                    new(newItem.Id)
                    {
                        IsEditOrderIndex = startIndex != newItem.OrderIndex,
                        OrderIndex = startIndex++,
                        IsEditParentId = newItem.ParentId != item.ParentId,
                        ParentId = item.ParentId,
                    }
                );
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
