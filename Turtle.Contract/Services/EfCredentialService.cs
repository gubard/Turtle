using System.Collections.Frozen;
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

public interface ICredentialService : IService<TurtleGetRequest,
    TurtlePostRequest, TurtleGetResponse, TurtlePostResponse>;

public interface IEfCredentialService : ICredentialService,
    IEfService<TurtleGetRequest, TurtlePostRequest, TurtleGetResponse,
        TurtlePostResponse>;

public sealed class EfCredentialService : EfService<TurtleGetRequest,
    TurtlePostRequest, TurtleGetResponse,
    TurtlePostResponse>, IEfCredentialService
{
    private readonly GaiaValues _gaiaValues;

    public EfCredentialService(DbContext dbContext, GaiaValues gaiaValues) : base(dbContext)
    {
        _gaiaValues = gaiaValues;
    }

    public override async ValueTask<TurtleGetResponse> GetAsync(
        TurtleGetRequest request,
        CancellationToken ct)
    {
        var credentials = await CredentialEntity.GetEntitiesAsync(
            CreateQuery(request), ct);

        var response = CreateResponse(request, credentials);

        if (request.LastId != -1)
        {
            response.Events = await DbContext.Set<EventEntity>()
               .Where(x => x.Id > request.LastId).ToArrayAsync(ct);
        }

        return response;
    }

    public override async ValueTask<TurtlePostResponse> PostAsync(
        TurtlePostRequest request, CancellationToken ct)
    {
        var response = new TurtlePostResponse();
        var editEntities = new List<EditCredentialEntity>();
        await CreateAsync(request.CreateCredentials, ct);
        Edit(request.EditCredentials, editEntities);
        ChangeOrder(request.ChangeOrders, response.ValidationErrors, editEntities);
        await CredentialEntity.EditEntitiesAsync(DbContext, _gaiaValues.UserId.ToString(),
            editEntities.ToArray(), ct);
        await DeleteAsync(request.DeleteIds, ct);
        await DbContext.SaveChangesAsync(ct);
        response.Events = await DbContext.Set<EventEntity>().Where(x => x.Id > request.LastLocalId).ToArrayAsync(ct);

        return response;
    }

    public override TurtlePostResponse Post(TurtlePostRequest request)
    {
        var response = new TurtlePostResponse();
        var editEntities = new List<EditCredentialEntity>();
        Create(request.CreateCredentials);
        Edit(request.EditCredentials, editEntities);
        ChangeOrder(request.ChangeOrders, response.ValidationErrors, editEntities);
        CredentialEntity.EditEntities(DbContext, _gaiaValues.UserId.ToString(), editEntities.ToArray());
        Delete(request.DeleteIds);
        DbContext.SaveChanges();
        response.Events = DbContext.Set<EventEntity>().Where(x => x.Id > request.LastLocalId).ToArray();

        return response;
    }

    public override TurtleGetResponse Get(TurtleGetRequest request)
    {
        var credentials = CredentialEntity.GetEntities(
            CreateQuery(request));

        var response = CreateResponse(request, credentials);

        if (request.LastId != -1)
        {
            response.Events = DbContext.Set<EventEntity>()
               .Where(x => x.Id > request.LastId).ToArray();
        }

        return response;
    }

    private void AddParents(TurtleGetResponse response, Guid rootId,
        FrozenDictionary<Guid, CredentialEntity> credentials)
    {
        var credential = ToCredential(credentials[rootId]);
        response.Parents.Add(rootId, [credential,]);

        if (credential.ParentId is null)
        {
            return;
        }

        AddParents(response, rootId, credential.ParentId.Value, credentials);
    }

    private void AddParents(TurtleGetResponse response, Guid rootId,
        Guid parentId, FrozenDictionary<Guid, CredentialEntity> credentials)
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

    private TurtleGetResponse CreateResponse(TurtleGetRequest request,
        CredentialEntity[] credentials)
    {
        var response = new TurtleGetResponse();
        var credentialsDictionary =
            credentials.ToDictionary(x => x.Id).ToFrozenDictionary();

        if (request.IsGetRoots)
        {
            response.Roots = credentials.Where(x => x.ParentId is null)
               .Select(ToCredential).ToArray();
        }

        foreach (var id in request.GetChildrenIds)
        {
            response.Children.Add(id,
                credentials.Where(y => y.ParentId == id).Select(ToCredential)
                   .ToList());
        }

        foreach (var id in request.GetParentsIds)
        {
            AddParents(response, id, credentialsDictionary);
            response.Parents[id].Reverse();
        }

        return response;
    }

    private IQueryable<EventEntity> CreateQuery(TurtleGetRequest request)
    {
        var childrenIds =
            request.GetChildrenIds.Select(x => (Guid?)x).ToFrozenSet();
        var query = DbContext.Set<EventEntity>().Where(x => x.Id == -1)
           .Select(x => x.EntityId);

        if (request.IsGetRoots)
        {
            query = query.Concat(DbContext.Set<EventEntity>()
               .GetProperty(nameof(CredentialEntity),
                    nameof(CredentialEntity.ParentId))
               .Where(x => x.EntityGuidValue == null).Distinct()
               .Select(x => x.EntityId));
        }

        if (request.GetChildrenIds.Length != 0)
        {
            query = query.Concat(DbContext.Set<EventEntity>()
               .GetProperty(nameof(CredentialEntity),
                    nameof(CredentialEntity.ParentId))
               .Where(x => childrenIds.Contains(x.EntityGuidValue)).Distinct()
               .Select(x => x.EntityId));
        }

        if (request.GetParentsIds.Length != 0)
        {
            var sql = CreateSqlForAllChildren(request.GetParentsIds);
            query = query.Concat(DbContext.Set<TempEntity>().FromSqlRaw(sql)
               .Select(x => x.EntityId));
        }

        return DbContext.Set<EventEntity>()
           .Where(x => query.Contains(x.EntityId));
    }

    private ValueTask DeleteAsync(Guid[] ids, CancellationToken ct)
    {
        if (ids.Length == 0)
        {
            return ValueTask.CompletedTask;
        }

        return CredentialEntity.DeleteEntitiesAsync(DbContext, _gaiaValues.UserId.ToString(), ct, ids);
    }

    private void Edit(EditCredential[] edits, List<EditCredentialEntity> editEntities)
    {
        foreach (var edit in edits)
        {
            foreach (var id in edit.Ids)
            {
                editEntities.Add(new(id)
                {
                    CustomAvailableCharacters =
                        edit.CustomAvailableCharacters,
                    IsEditCustomAvailableCharacters =
                        edit.IsEditCustomAvailableCharacters,
                    IsAvailableLowerLatin =
                        edit.IsAvailableLowerLatin,
                    IsEditIsAvailableLowerLatin =
                        edit.IsEditIsAvailableLowerLatin,
                    IsAvailableNumber = edit.IsAvailableNumber,
                    IsEditIsAvailableNumber =
                        edit.IsEditIsAvailableNumber,
                    IsAvailableSpecialSymbols =
                        edit.IsAvailableSpecialSymbols,
                    IsEditIsAvailableSpecialSymbols =
                        edit.IsEditIsAvailableSpecialSymbols,
                    IsAvailableUpperLatin =
                        edit.IsAvailableUpperLatin,
                    IsEditIsAvailableUpperLatin =
                        edit.IsEditIsAvailableUpperLatin,
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
                });
            }
        }
    }

    private ValueTask CreateAsync(Credential[] creates, CancellationToken ct)
    {
        if (creates.Length == 0)
        {
            return ValueTask.CompletedTask;
        }

        var entities =
            new Span<CredentialEntity>(new CredentialEntity[creates.Length]);

        for (var index = 0; index < creates.Length; index++)
        {
            var createCredential = creates[index];
            entities[index] = new()
            {
                CustomAvailableCharacters =
                    createCredential.CustomAvailableCharacters,
                IsAvailableLowerLatin = createCredential.IsAvailableLowerLatin,
                Id = createCredential.Id,
                IsAvailableNumber = createCredential.IsAvailableNumber,
                IsAvailableSpecialSymbols =
                    createCredential.IsAvailableSpecialSymbols,
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

        return CredentialEntity.AddEntitiesAsync(DbContext, $"{_gaiaValues.UserId}", ct, entities.ToArray());
    }

    private void ChangeOrder(CredentialChangeOrder[] changeOrders,
        List<ValidationError> errors, List<EditCredentialEntity> editEntities)
    {
        if (changeOrders.Length == 0)
        {
            return;
        }

        var insertIds = changeOrders.SelectMany(x => x.InsertIds).Distinct()
           .ToFrozenSet();
        var insertItems = CredentialEntity.GetEntities(
            DbContext.Set<EventEntity>()
               .Where(x => insertIds.Contains(x.EntityId)));
        var insertItemsDictionary =
            insertItems.ToDictionary(x => x.Id).ToFrozenDictionary();
        var startIds = changeOrders.Select(x => x.StartId).Distinct()
           .ToFrozenSet();
        var startItems = CredentialEntity.GetEntities(
            DbContext.Set<EventEntity>()
               .Where(x => startIds.Contains(x.EntityId)));
        var startItemsDictionary =
            startItems.ToDictionary(x => x.Id).ToFrozenDictionary();
        var parentItems = startItems.Select(x => x.ParentId).Distinct()
           .ToFrozenSet();
        var query = DbContext.Set<EventEntity>()
           .GetProperty(nameof(CredentialEntity),
                nameof(CredentialEntity.ParentId))
           .Where(x => parentItems.Contains(x.EntityGuidValue))
           .Select(x => x.EntityId).Distinct();
        var siblings = CredentialEntity.GetEntities(
            DbContext.Set<EventEntity>()
               .Where(x => query.Contains(x.EntityId)));

        for (var index = 0; index < changeOrders.Length; index++)
        {
            var changeOrder = changeOrders[index];

            var inserts = changeOrder.InsertIds
               .Select(x => insertItemsDictionary[x]).ToFrozenSet();

            if (!startItemsDictionary.TryGetValue(changeOrder.StartId,
                out var item))
            {
                errors.Add(
                    new NotFoundValidationError(changeOrder.StartId
                       .ToString()));

                continue;
            }

            var startIndex = changeOrder.IsAfter
                ? item.OrderIndex + 1
                : item.OrderIndex;
            var items = siblings.Where(x => x.ParentId == item.ParentId)
               .OrderBy(x => x.OrderIndex);

            var usedItems = changeOrder.IsAfter
                ? items.Where(x => x.OrderIndex > item.OrderIndex)
                : items.Where(x => x.OrderIndex >= item.OrderIndex);

            var newOrder = inserts
               .Concat(usedItems.Where(x => !insertIds.Contains(x.Id)))
               .ToFrozenSet();

            foreach (var newItem in newOrder)
            {
                editEntities.Add(new(newItem.Id)
                {
                    IsEditOrderIndex = startIndex != newItem.OrderIndex,
                    OrderIndex = startIndex++,
                    IsEditParentId = newItem.ParentId != item.ParentId,
                    ParentId = item.ParentId,
                });
            }
        }
    }

    private void Delete(Guid[] ids)
    {
        if (ids.Length == 0)
        {
            return;
        }

        CredentialEntity.DeleteEntities(DbContext, _gaiaValues.UserId.ToString(), ids);
    }

    private void Create(Credential[] creates)
    {
        if (creates.Length == 0)
        {
            return;
        }

        var entities =
            new Span<CredentialEntity>(new CredentialEntity[creates.Length]);

        for (var index = 0; index < creates.Length; index++)
        {
            var createCredential = creates[index];
            entities[index] = new()
            {
                CustomAvailableCharacters =
                    createCredential.CustomAvailableCharacters,
                IsAvailableLowerLatin = createCredential.IsAvailableLowerLatin,
                Id = createCredential.Id,
                IsAvailableNumber = createCredential.IsAvailableNumber,
                IsAvailableSpecialSymbols =
                    createCredential.IsAvailableSpecialSymbols,
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

        CredentialEntity.AddEntities(DbContext, _gaiaValues.UserId.ToString(), entities.ToArray());
    }

    private string CreateSqlForAllChildren(params Guid[] ids)
    {
        var idsString = ids.Select(i => i.ToString().ToUpperInvariant())
           .JoinString("', '");

        return
            $"""
            WITH RECURSIVE hierarchy(Id, EntityId, EntityGuidValue) AS (
                SELECT
                    Id,
                    EntityId,
                    EntityGuidValue
                FROM
                    EventEntity
                WHERE
                    EntityId IN ('{idsString}')
                  AND EntityProperty = 'ParentId'
                  AND EntityType = 'CredentialEntity'
                  AND Id IN (
                    SELECT
                        MAX(
                                CASE WHEN s.EntityProperty = 'ParentId'
                                    AND s.EntityType = 'CredentialEntity' THEN s.Id END
                        )
                    FROM
                        EventEntity AS s
                    GROUP BY
                        s.EntityId
                )
                UNION ALL
                SELECT
                    t.Id,
                    t.EntityId,
                    t.EntityGuidValue
                FROM
                    EventEntity AS t
                INNER JOIN hierarchy h ON h.EntityGuidValue = t.EntityId
                WHERE
                    t.EntityProperty = 'ParentId'
                  AND t.EntityType = 'CredentialEntity'
                  AND t.Id IN (
                    SELECT
                        MAX(
                                CASE WHEN e.EntityProperty = 'ParentId'
                                    AND e.EntityType = 'CredentialEntity' THEN e.Id END
                        )
                    FROM
                        EventEntity AS e
                    GROUP BY
                        e.EntityId
                )
            )
            SELECT
                DISTINCT EntityId
            FROM
                hierarchy
            """;
    }
}