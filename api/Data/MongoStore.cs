using MongoDB.Bson;
using MongoDB.Driver;
using Terreiro.Domain;
namespace Terreiro.Data;

public sealed class MongoStore
{
    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }
    public IMongoCollection<User> Users => Database.GetCollection<User>("users");
    public IMongoCollection<LoginSession> Sessions => Database.GetCollection<LoginSession>("sessions");
    public IMongoCollection<Cleaning> Cleanings => Database.GetCollection<Cleaning>("cleanings");
    public IMongoCollection<Notification> Notifications => Database.GetCollection<Notification>("notifications");
    public IMongoCollection<PushDevice> Devices => Database.GetCollection<PushDevice>("push_devices");
    public IMongoCollection<PushDelivery> Deliveries => Database.GetCollection<PushDelivery>("push_deliveries");
    public IMongoCollection<AuditEvent> Audit => Database.GetCollection<AuditEvent>("audit");
    public MongoStore(string connectionString, string database)
    {
        var settings = MongoClientSettings.FromConnectionString(connectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(15);
        Client = new MongoClient(settings);
        Database = Client.GetDatabase(database);
    }
    public async Task Initialize(CancellationToken ct)
    {
        var hello = await Database.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), cancellationToken: ct);
        if (!hello.Contains("setName") && (!hello.TryGetValue("msg", out var msg) || msg != "isdbgrid"))
            throw new InvalidOperationException("MongoDB deve executar como replica set ou cluster com transações. Não iniciar em standalone.");
        // Explicit creation ensures transactional writes never need to create collections.
        using var names = await Database.ListCollectionNamesAsync(cancellationToken: ct);
        var existing = await names.ToListAsync(ct);
        foreach (var name in new[] { "users", "sessions", "cleanings", "notifications", "push_devices", "push_deliveries", "audit" })
            if (!existing.Contains(name))
                try { await Database.CreateCollectionAsync(name, cancellationToken: ct); }
                catch (MongoCommandException ex) when (ex.Code == 48) { /* Concurrent initialization. */ }
        await Users.Indexes.CreateOneAsync(new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(x => x.HouseId).Ascending(x => x.Login), new CreateIndexOptions { Unique = true }), cancellationToken: ct);
        await Sessions.Indexes.CreateOneAsync(new CreateIndexModel<LoginSession>(Builders<LoginSession>.IndexKeys.Ascending(x => x.ExpiresAt), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }), cancellationToken: ct);
        await Cleanings.Indexes.CreateOneAsync(new CreateIndexModel<Cleaning>(Builders<Cleaning>.IndexKeys.Ascending(x => x.HouseId).Descending(x => x.StartsAt)), cancellationToken: ct);
        await Notifications.Indexes.CreateOneAsync(new CreateIndexModel<Notification>(Builders<Notification>.IndexKeys.Ascending(x => x.HouseId).Ascending(x => x.UserId).Descending(x => x.CreatedAt)), cancellationToken: ct);
        await Notifications.Indexes.CreateOneAsync(new CreateIndexModel<Notification>(Builders<Notification>.IndexKeys.Ascending(x => x.PushState).Ascending(x => x.DueAt)), cancellationToken: ct);
        await Devices.Indexes.CreateOneAsync(new CreateIndexModel<PushDevice>(Builders<PushDevice>.IndexKeys.Ascending(x => x.UserId).Ascending(x => x.HouseId)), cancellationToken: ct);
    }
    public async Task<T> Transaction<T>(Func<IClientSessionHandle, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        using var session = await Client.StartSessionAsync(cancellationToken: ct);
        return await session.WithTransactionAsync(action,
            new TransactionOptions(readConcern: ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority, readPreference: ReadPreference.Primary), ct);
    }
}
