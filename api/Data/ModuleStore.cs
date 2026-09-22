using System.Linq.Expressions;
using System.Text.Json;
using MongoDB.Driver;
using Terreiro.Domain;
namespace Terreiro.Data;

// Single-house write gate: modest administrative volume, no write skew during period closing.
// External HTTP and file processing MUST NOT run inside Transaction().
public sealed class ModuleStore(MongoStore db)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public MongoStore Root => db;
    public IMongoCollection<T> Set<T>() where T : Entity => db.Database.GetCollection<T>("m_" + typeof(T).Name.ToLowerInvariant());
    public async Task Initialize(string house, CancellationToken ct)
    {
        using var cursor = await db.Database.ListCollectionNamesAsync(cancellationToken: ct);
        var names = await cursor.ToListAsync(ct);
        foreach (var type in typeof(Entity).Assembly.GetTypes().Where(x => x.IsSubclassOf(typeof(Entity)) && !x.IsAbstract))
        {
            var name = "m_" + type.Name.ToLowerInvariant();
            if (!names.Contains(name))
                try { await db.Database.CreateCollectionAsync(name, cancellationToken: ct); }
                catch (MongoCommandException e) when (e.Code == 48) { }
        }
        await Set<HouseGate>().UpdateOneAsync(x => x.Id == house, Builders<HouseGate>.Update.SetOnInsert(x => x.HouseId, house).SetOnInsert(x => x.Sequence, 0), new UpdateOptions { IsUpsert = true }, ct);
        await Set<Receipt>().Indexes.CreateOneAsync(new CreateIndexModel<Receipt>(Builders<Receipt>.IndexKeys.Ascending(x => x.HouseId).Ascending(x => x.AccountId).Ascending(x => x.FinancialReference), new CreateIndexOptions { Unique = true }), cancellationToken: ct);
        await Set<ExpensePayment>().Indexes.CreateOneAsync(new CreateIndexModel<ExpensePayment>(Builders<ExpensePayment>.IndexKeys.Ascending(x => x.HouseId).Ascending(x => x.AccountId).Ascending(x => x.FinancialReference), new CreateIndexOptions { Unique = true }), cancellationToken: ct);
        await Set<Due>().Indexes.CreateOneAsync(new CreateIndexModel<Due>(Builders<Due>.IndexKeys.Ascending(x => x.HouseId).Ascending(x => x.MemberId).Ascending(x => x.Competence), new CreateIndexOptions { Unique = true }), cancellationToken: ct);
        await Set<Due>().Indexes.CreateOneAsync(new CreateIndexModel<Due>(Builders<Due>.IndexKeys.Ascending(x=>x.HouseId).Descending(x=>x.Competence).Ascending(x=>x.MemberId)), cancellationToken:ct);
        await Set<MemberExemption>().Indexes.CreateOneAsync(new CreateIndexModel<MemberExemption>(Builders<MemberExemption>.IndexKeys.Ascending(x=>x.HouseId).Ascending(x=>x.MemberId).Ascending(x=>x.EffectiveFrom)), cancellationToken:ct);
        await Set<Allocation>().Indexes.CreateOneAsync(new CreateIndexModel<Allocation>(Builders<Allocation>.IndexKeys.Ascending(x=>x.HouseId).Ascending(x=>x.DueId)), cancellationToken:ct);
        await Set<Approval>().Indexes.CreateOneAsync(new CreateIndexModel<Approval>(Builders<Approval>.IndexKeys.Ascending(x=>x.HouseId).Ascending(x=>x.Type).Ascending(x=>x.TargetId)), cancellationToken:ct);
        await Set<StatementLine>().Indexes.CreateOneAsync(new CreateIndexModel<StatementLine>(Builders<StatementLine>.IndexKeys.Ascending(x => x.HouseId).Ascending(x => x.AccountId).Ascending(x => x.FinancialReference), new CreateIndexOptions { Unique = true }), cancellationToken: ct);
        await Set<Evidence>().Indexes.CreateOneAsync(new CreateIndexModel<Evidence>(Builders<Evidence>.IndexKeys.Ascending(x => x.HouseId).Ascending(x => x.Sha256)), cancellationToken: ct);
        await Set<LedgerEntry>().Indexes.CreateOneAsync(new CreateIndexModel<LedgerEntry>(Builders<LedgerEntry>.IndexKeys.Ascending(x => x.HouseId).Ascending(x => x.OccurredOn)), cancellationToken: ct);
        await Set<Closing>().Indexes.CreateOneAsync(new CreateIndexModel<Closing>(Builders<Closing>.IndexKeys.Ascending(x => x.HouseId).Ascending(x => x.Period), new CreateIndexOptions { Unique = true }), cancellationToken: ct);
    }
    public async Task<List<T>> List<T>(string house, CancellationToken ct, int limit = 500) where T : Entity =>
        await Set<T>().Find(x => x.HouseId == house).SortByDescending(x => x.CreatedAt).Limit(Math.Clamp(limit, 1, 5000)).ToListAsync(ct);
    public async Task<T> Get<T>(string house, string id, CancellationToken ct) where T : Entity
    {
        var item = await Set<T>().Find(x => x.Id == id && x.HouseId == house).FirstOrDefaultAsync(ct);
        Rules.Require(item != null, "not_found", "Registro não encontrado.", 404); return item;
    }
    public async Task<T> Get<T>(IClientSessionHandle s, string house, string id, CancellationToken ct) where T : Entity
    {
        var item = await Set<T>().Find(s, x => x.Id == id && x.HouseId == house).FirstOrDefaultAsync(ct);
        Rules.Require(item != null, "not_found", "Registro não encontrado.", 404); return item;
    }
    public Task Insert<T>(IClientSessionHandle s, T item, CancellationToken ct) where T : Entity => Set<T>().InsertOneAsync(s, item, cancellationToken: ct);
    public async Task Save<T>(IClientSessionHandle s, T item, CancellationToken ct) where T : Entity
    {
        var version = item.Revision; item.Revision++; item.UpdatedAt = DateTime.UtcNow;
        var result = await Set<T>().ReplaceOneAsync(s, x => x.Id == item.Id && x.HouseId == item.HouseId && x.Revision == version, item, cancellationToken: ct);
        Rules.Require(result.ModifiedCount == 1, "conflict", "Outra operação alterou o registro. Atualize a tela.", 409);
    }
    public async Task<T> Execute<T>(User actor, string kind, string operationId, object input, Func<IClientSessionHandle,CancellationToken,Task<T>> action, CancellationToken ct)
    {
        Rules.Require(Guid.TryParse(operationId, out _), "operation_invalid", "Identificador da operação inválido.");
        var id = Rules.Hash(actor.HouseId + "|" + actor.Id + "|" + kind + "|" + operationId);
        var requestHash = Rules.Hash(JsonSerializer.Serialize(input,JsonOptions));
        return await db.Transaction(async (s, c) =>
        {
            await Set<HouseGate>().UpdateOneAsync(s, x => x.Id == actor.HouseId, Builders<HouseGate>.Update.Inc(x => x.Sequence, 1), cancellationToken: c);
            // Revalidate revocation/permissions under the same write transaction.
            var current = await db.Users.Find(s, x => x.Id == actor.Id && x.HouseId == actor.HouseId && x.Active).FirstOrDefaultAsync(c);
            Rules.Require(current != null && current.AuthRevision == actor.AuthRevision, "access_changed", "Seu acesso foi alterado. Entre novamente.", 401);
            var actorLock=await db.Users.UpdateOneAsync(s,x=>x.Id==actor.Id&&x.HouseId==actor.HouseId&&x.Active&&x.AuthRevision==actor.AuthRevision,Builders<User>.Update.Inc(x=>x.OperationRevision,1),cancellationToken:c);
            Rules.Require(actorLock.MatchedCount==1,"access_changed","Seu acesso foi alterado.",401);
            var prior = await Set<OperationRecord>().Find(s, x => x.Id == id && x.HouseId == actor.HouseId).FirstOrDefaultAsync(c);
            if (prior != null)
            {
                Rules.Require(prior.RequestHash == requestHash, "operation_reused", "A mesma operação foi reapresentada com dados diferentes.", 409);
                return JsonSerializer.Deserialize<T>(prior.ResultJson,JsonOptions)!;
            }
            var result = await action(s,c);
            await Insert(s, new OperationRecord { Id=id, HouseId=actor.HouseId, ActorId=actor.Id, Kind=kind, RequestHash=requestHash, ResultJson=JsonSerializer.Serialize(result,JsonOptions) }, c);
            await db.Audit.InsertOneAsync(s, new AuditEvent { HouseId=actor.HouseId, ActorId=actor.Id, Action=kind, ResourceId=id }, cancellationToken:c);
            return result;
        }, ct);
    }
    public Task Notify(IClientSessionHandle s, string house, string user, string key, string title, string body, string path, string category, CancellationToken ct, string? referenceId=null)
    {
        var n = new Notification { Id=Rules.Hash(house+"|"+key+"|"+user), HouseId=house, UserId=user, Title=title, Body=body, Path=path, Category=category };
        return db.Notifications.UpdateOneAsync(s, x => x.Id==n.Id, Builders<Notification>.Update
            .SetOnInsert(x=>x.HouseId,n.HouseId).SetOnInsert(x=>x.UserId,n.UserId).SetOnInsert(x=>x.Title,n.Title)
            .SetOnInsert(x=>x.Body,n.Body).SetOnInsert(x=>x.Path,n.Path).SetOnInsert(x=>x.Category,n.Category)
            .SetOnInsert(x=>x.ReferenceId,referenceId).SetOnInsert(x=>x.CreatedAt,n.CreatedAt).SetOnInsert(x=>x.DueAt,n.DueAt).SetOnInsert(x=>x.ExpiresAt,n.ExpiresAt).SetOnInsert(x=>x.PushState,"Pending"),
            new UpdateOptions{IsUpsert=true},ct);
    }
    public async Task OpenPeriod(IClientSessionHandle s, string house, string date, CancellationToken ct)
    {
        var period = BusinessRules.Date(date)[..7];
        var closed = await Set<Closing>().Find(s, x => x.HouseId==house && x.Period==period && x.State != "Reopened").AnyAsync(ct);
        Rules.Require(!closed,"period_closed","O período está fechado ou em revisão. Solicite reabertura autorizada.",409);
    }
}
