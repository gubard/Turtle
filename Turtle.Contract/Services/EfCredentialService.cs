using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using Gaia.Helpers;
using Gaia.Models;
using Gaia.Services;
using Microsoft.EntityFrameworkCore;
using Nestor.Db.Helpers;
using Nestor.Db.Models;
using Nestor.Db.Services;
using Turtle.Contract.Models;

namespace Turtle.Contract.Services;

public interface IHttpCredentialService : ICredentialService;

public interface ICredentialService
    : IService<TurtleGetRequest, TurtlePostRequest, TurtleGetResponse, TurtlePostResponse>;

public interface IEfCredentialService
    : ICredentialService,
        IEfService<TurtleGetRequest, TurtlePostRequest, TurtleGetResponse, TurtlePostResponse>;

public sealed class EfCredentialService<TDbContext>
    : EfService<
        TurtleGetRequest,
        TurtlePostRequest,
        TurtleGetResponse,
        TurtlePostResponse,
        TDbContext
    >,
        IEfCredentialService
    where TDbContext : NestorDbContext, ICredentialDbContext
{
    private readonly GaiaValues _gaiaValues;

    public EfCredentialService(TDbContext dbContext, GaiaValues gaiaValues)
        : base(dbContext)
    {
        _gaiaValues = gaiaValues;
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
        var credentials = await CreateQuery(request).ToArrayAsync(ct);
        var response = CreateResponse(request, credentials);

        if (request.LastId != -1)
        {
            response.Events = await DbContext
                .Events.Where(x => x.Id > request.LastId)
                .ToArrayAsync(ct);
        }

        return response;
    }

    public override ConfiguredValueTaskAwaitable<TurtlePostResponse> PostAsync(
        Guid idempotentId,
        TurtlePostRequest request,
        CancellationToken ct
    )
    {
        return PostCore(idempotentId, request, ct).ConfigureAwait(false);
    }

    private async ValueTask<TurtlePostResponse> PostCore(
        Guid idempotentId,
        TurtlePostRequest request,
        CancellationToken ct
    )
    {
        var response = new TurtlePostResponse();
        var editEntities = new List<EditCredentialEntity>();
        await CreateAsync(idempotentId, request.CreateCredentials, ct);
        Edit(request.EditCredentials, editEntities);
        ChangeOrder(request.ChangeOrders, response.ValidationErrors, editEntities);

        await CredentialEntity.EditEntitiesAsync(
            DbContext,
            _gaiaValues.UserId.ToString(),
            idempotentId,
            editEntities.ToArray(),
            ct
        );

        await DeleteAsync(idempotentId, request.DeleteIds, ct);
        await DbContext.SaveChangesAsync(ct);

        response.Events = await DbContext
            .Events.Where(x => x.Id > request.LastLocalId)
            .ToArrayAsync(ct);

        return response;
    }

    public override TurtlePostResponse Post(Guid idempotentId, TurtlePostRequest request)
    {
        var response = new TurtlePostResponse();
        var editEntities = new List<EditCredentialEntity>();
        Create(idempotentId, request.CreateCredentials);
        Edit(request.EditCredentials, editEntities);
        ChangeOrder(request.ChangeOrders, response.ValidationErrors, editEntities);

        CredentialEntity.EditEntities(
            DbContext,
            _gaiaValues.UserId.ToString(),
            idempotentId,
            editEntities.ToArray()
        );

        Delete(idempotentId, request.DeleteIds);
        DbContext.SaveChanges();
        response.Events = DbContext.Events.Where(x => x.Id > request.LastLocalId).ToArray();

        return response;
    }

    public override TurtleGetResponse Get(TurtleGetRequest request)
    {
        var credentials = CreateQuery(request).ToArray();
        var response = CreateResponse(request, credentials);

        if (request.LastId != -1)
        {
            response.Events = DbContext.Events.Where(x => x.Id > request.LastId).ToArray();
        }

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

    private IQueryable<CredentialEntity> CreateQuery(TurtleGetRequest request)
    {
        var childrenIds = request.GetChildrenIds.Select(x => (Guid?)x).ToFrozenSet();
        var query = DbContext.Credentials.Where(x => x.Id == Guid.Empty);

        if (request.IsGetRoots)
        {
            query = query.Concat(DbContext.Credentials.Where(x => x.ParentId == null));
        }

        if (request.GetChildrenIds.Length != 0)
        {
            query = query.Concat(
                DbContext.Credentials.Where(x => childrenIds.Contains(x.ParentId))
            );
        }

        if (request.GetParentsIds.Length != 0)
        {
            var sql = CreateSqlForAllChildren(request.GetParentsIds);
            query = query.Concat(DbContext.Credentials.FromSqlRaw(sql));
        }

        return query;
    }

    private ConfiguredValueTaskAwaitable DeleteAsync(
        Guid idempotentId,
        Guid[] ids,
        CancellationToken ct
    )
    {
        if (ids.Length == 0)
        {
            return TaskHelper.ConfiguredCompletedTask;
        }

        return CredentialEntity.DeleteEntitiesAsync(
            DbContext,
            _gaiaValues.UserId.ToString(),
            idempotentId,
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
        Guid idempotentId,
        Credential[] creates,
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

        return CredentialEntity.AddEntitiesAsync(
            DbContext,
            $"{_gaiaValues.UserId}",
            idempotentId,
            entities.ToArray(),
            ct
        );
    }

    private void ChangeOrder(
        CredentialChangeOrder[] changeOrders,
        List<ValidationError> errors,
        List<EditCredentialEntity> editEntities
    )
    {
        if (changeOrders.Length == 0)
        {
            return;
        }

        var insertIds = changeOrders.SelectMany(x => x.InsertIds).Distinct().ToFrozenSet();
        var insertItems = DbContext.Credentials.Where(x => insertIds.Contains(x.Id));
        var insertItemsDictionary = insertItems.ToDictionary(x => x.Id).ToFrozenDictionary();
        var startIds = changeOrders.Select(x => x.StartId).Distinct().ToFrozenSet();
        var startItems = DbContext.Credentials.Where(x => startIds.Contains(x.Id));
        var startItemsDictionary = startItems.ToDictionary(x => x.Id).ToFrozenDictionary();
        var parentItems = startItems.Select(x => x.ParentId).Distinct().ToFrozenSet();
        var siblings = DbContext.Credentials.Where(x => parentItems.Contains(x.Id)).ToArray();

        for (var index = 0; index < changeOrders.Length; index++)
        {
            var changeOrder = changeOrders[index];

            var inserts = changeOrder.InsertIds.Select(x => insertItemsDictionary[x]).ToFrozenSet();

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

    private void Delete(Guid idempotentId, Guid[] ids)
    {
        if (ids.Length == 0)
        {
            return;
        }

        CredentialEntity.DeleteEntities(
            DbContext,
            _gaiaValues.UserId.ToString(),
            idempotentId,
            ids
        );
    }

    private void Create(Guid idempotentId, Credential[] creates)
    {
        if (creates.Length == 0)
        {
            return;
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

        CredentialEntity.AddEntities(
            DbContext,
            _gaiaValues.UserId.ToString(),
            idempotentId,
            entities.ToArray()
        );
    }

    private string CreateSqlForAllChildren(Guid[] ids)
    {
        var idsString = ids.Select(i => i.ToString().ToUpperInvariant()).JoinString("', '");

        return $$"""
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
                     FROM ToDoItem
                     WHERE Id IN ('{{idsString}}')

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
                     FROM ToDoItem t
                     INNER JOIN hierarchy h ON t.ParentId = h.Id
                 )
                 SELECT * FROM hierarchy;
            """;
    }
}
