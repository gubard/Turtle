using System.Collections.Frozen;
using Gaia.Helpers;
using Gaia.Models;
using Gaia.Services;
using Microsoft.EntityFrameworkCore;
using Nestor.Db.Helpers;
using Nestor.Db.Models;
using Turtle.Contract.Models;

namespace Turtle.Contract.Services;

public interface ICredentialService : IService<TurtleGetRequest, TurtlePostRequest, TurtleGetResponse, TurtlePostResponse>;
public interface IHttpCredentialService : ICredentialService, IHttpService<TurtleGetRequest, TurtlePostRequest, TurtleGetResponse, TurtlePostResponse>;

public interface IEfCredentialService : ICredentialService
{
    ValueTask SaveEventsAsync(ReadOnlyMemory<EventEntity> events, CancellationToken ct);
    ValueTask<long> GetLastIdAsync(CancellationToken ct);
}

public class EfCredentialService : IEfCredentialService
{
    private readonly DbContext _dbContext;

    public EfCredentialService(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async ValueTask<TurtleGetResponse> GetAsync(TurtleGetRequest request, CancellationToken ct)
    {
        var response = new TurtleGetResponse();
        var childrenIds = request.GetChildrenIds.Select(x => (Guid?)x).ToFrozenSet();
        var query = _dbContext.Set<EventEntity>().Where(x => x.Id == -1).Select(x => x.EntityId);

        if (request.IsGetRoots)
        {
            query = query.Concat(_dbContext.Set<EventEntity>().GetProperty(nameof(CredentialEntity), nameof(CredentialEntity.ParentId)).Where(x => x.EntityGuidValue == null).Distinct().Select(x => x.EntityId));
        }

        if (request.GetChildrenIds.Length != 0)
        {
            query = query.Concat(_dbContext.Set<EventEntity>().GetProperty(nameof(CredentialEntity), nameof(CredentialEntity.ParentId)).Where(x => childrenIds.Contains(x.EntityGuidValue)).Distinct().Select(x => x.EntityId));
        }

        if (request.GetParentsIds.Length != 0)
        {
            var sql = CreateSqlForAllChildren(request.GetParentsIds);
            query = query.Concat(_dbContext.Set<TempEntity>().FromSqlRaw(sql).Select(x => x.EntityId));
        }

        var credentials = await CredentialEntity.GetCredentialEntitysAsync(_dbContext.Set<EventEntity>().Where(x => query.Contains(x.EntityId)), ct);
        var credentialsDictionary = credentials.ToDictionary(x => x.Id).ToFrozenDictionary();
        response.Roots.AddRange(credentials.Where(x => x.ParentId is null).Select(ToCredential));

        foreach (var id in request.GetChildrenIds)
        {
            response.Children.Add(id, credentials.Where(y => y.ParentId == id).Select(ToCredential).ToList());
        }

        foreach (var id in request.GetParentsIds)
        {
            AddParents(response, id, credentialsDictionary);
            response.Parents[id].Reverse();
        }

        if (request.LastId != -1)
        {
            response.Events = await _dbContext.Set<EventEntity>().Where(x => x.Id > request.LastId).ToArrayAsync(ct);
        }

        return response;
    }

    private void AddParents(TurtleGetResponse response, Guid rootId, FrozenDictionary<Guid, CredentialEntity> credentials)
    {
        var credential = ToCredential(credentials[rootId]);
        response.Parents.Add(rootId, [credential]);

        if (credential.ParentId is null)
        {
            return;
        }

        AddParents(response, rootId, credential.ParentId.Value, credentials);
    }

    private void AddParents(TurtleGetResponse response, Guid rootId, Guid parentId, FrozenDictionary<Guid, CredentialEntity> credentials)
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

    public async ValueTask<TurtlePostResponse> PostAsync(TurtlePostRequest request, CancellationToken ct)
    {
        var response = new TurtlePostResponse();
        await DeleteAsync(request.DeleteIds, ct);
        await CreateAsync(request.CreateCredentials, ct);
        await EditAsync(request.EditCredentials, ct);
        await ChangeOrderAsync(request.ChangeOrders, response.ValidationErrors, ct);
        await _dbContext.SaveChangesAsync(ct);
        response.Events = await _dbContext.Set<EventEntity>().Where(x => x.Id > request.LastLocalId).ToArrayAsync(ct);

        return response;
    }

    public async ValueTask SaveEventsAsync(ReadOnlyMemory<EventEntity> events, CancellationToken ct)
    {
        if (events.IsEmpty)
        {
            return;
        }

        await _dbContext.AddRangeAsync(events.ToArray(), ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async ValueTask<long> GetLastIdAsync(CancellationToken ct)
    {
        var lastId = await _dbContext.Set<EventEntity>().MaxAsync(x => (long?)x.Id, ct);

        if (lastId is null)
        {
            return 0;
        }

        return lastId.Value;
    }

    private async ValueTask ChangeOrderAsync(ChangeOrder[] changeOrders, List<ValidationError> errors, CancellationToken ct)
    {
        if (changeOrders.Length == 0)
        {
            return;
        }

        var insertIds = changeOrders.SelectMany(x => x.InsertIds).Distinct().ToFrozenSet();
        var insertItems = await CredentialEntity.GetCredentialEntitysAsync(_dbContext.Set<EventEntity>().Where(x => insertIds.Contains(x.EntityId)), ct);
        var insertItemsDictionary = insertItems.ToDictionary(x => x.Id).ToFrozenDictionary();
        var startIds = changeOrders.Select(x => x.StartId).Distinct().ToFrozenSet();
        var startItems = await CredentialEntity.GetCredentialEntitysAsync(_dbContext.Set<EventEntity>().Where(x => startIds.Contains(x.EntityId)), ct);
        var startItemsDictionary = startItems.ToDictionary(x => x.Id).ToFrozenDictionary();
        var parentItems = startItems.Select(x => x.ParentId).Distinct().ToFrozenSet();
        var query = _dbContext.Set<EventEntity>().GetProperty(nameof(CredentialEntity), nameof(CredentialEntity.ParentId)).Where(x => parentItems.Contains(x.EntityGuidValue)).Select(x => x.EntityId).Distinct();
        var siblings = await CredentialEntity.GetCredentialEntitysAsync(_dbContext.Set<EventEntity>().Where(x => query.Contains(x.EntityId)), ct);
        var edits = new List<EditCredentialEntity>();

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

            var newOrder = usedItems.Concat(inserts).ToFrozenSet();

            foreach (var newItem in newOrder)
            {
                edits.Add(new(newItem.Id)
                {
                    IsEditOrderIndex = startIndex != newItem.OrderIndex,
                    OrderIndex = startIndex++,
                    IsEditParentId = newItem.ParentId != item.ParentId,
                    ParentId = item.ParentId,
                });
            }
        }

        await CredentialEntity.EditCredentialEntitysAsync(_dbContext, "Service", edits.ToArray(), ct);
    }

    private ValueTask DeleteAsync(Guid[] ids, CancellationToken ct)
    {
        if (ids.Length == 0)
        {
            return ValueTask.CompletedTask;
        }

        return CredentialEntity.DeleteCredentialEntitysAsync(_dbContext, "Service", ct, ids);
    }

    private ValueTask EditAsync(EditCredential[] edits, CancellationToken ct)
    {
        if (edits.Length == 0)
        {
            return ValueTask.CompletedTask;
        }

        var editEntities = new Span<EditCredentialEntity>(new EditCredentialEntity[edits.Length]);

        for (var index = 0; index < edits.Length; index++)
        {
            var editCredential = edits[index];
            editEntities[index] = new(editCredential.Id)
            {
                CustomAvailableCharacters = editCredential.CustomAvailableCharacters,
                IsAvailableLowerLatin = editCredential.IsAvailableLowerLatin,
                IsAvailableNumber = editCredential.IsAvailableNumber,
                IsAvailableSpecialSymbols = editCredential.IsAvailableSpecialSymbols,
                IsAvailableUpperLatin = editCredential.IsAvailableUpperLatin,
                Key = editCredential.Key,
                Length = editCredential.Length,
                Login = editCredential.Login,
                Name = editCredential.Name,
                Regex = editCredential.Regex,
                Type = editCredential.Type,
                ParentId = editCredential.ParentId,
            };
        }

        return CredentialEntity.EditCredentialEntitysAsync(_dbContext, "Service", editEntities.ToArray(), ct);
    }

    private ValueTask CreateAsync(Credential[] creates, CancellationToken ct)
    {
        if (creates.Length == 0)
        {
            return ValueTask.CompletedTask;
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

        return CredentialEntity.AddCredentialEntitysAsync(_dbContext, "Service", ct, entities.ToArray());
    }

    private string CreateSqlForAllChildren(params Guid[] ids)
    {
        var idsString = ids.Select(i => i.ToString().ToUpperInvariant()).JoinString("', '");

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