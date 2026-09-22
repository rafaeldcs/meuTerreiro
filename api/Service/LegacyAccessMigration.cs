using MongoDB.Bson;
using MongoDB.Driver;
using Terreiro.Data;
using Terreiro.Domain;
namespace Terreiro.Service;

// Only for installations of the earlier alpha that predate the Permissions field.
// Explicit deployment opt-in, audit and session revocation; never silently grants new permissions.
public static class LegacyAccessMigration
{
    public static async Task Run(MongoStore db,string house,bool authorized,CancellationToken ct)
    {
        var users=db.Database.GetCollection<BsonDocument>("users");
        var filter=Builders<BsonDocument>.Filter.Eq("HouseId",house)&Builders<BsonDocument>.Filter.Exists("Permissions",false);
        if(!await users.Find(filter).AnyAsync(ct))return;
        if(!authorized)throw new InvalidOperationException("Há usuários do alpha anterior sem permissões explícitas. Faça backup e revise a migração descrita em docs/PUBLICACAO.md; não redefina senhas nem apague volumes.");
        await db.Transaction(async(s,c)=>
        {
            var legacy=await users.Find(s,filter).ToListAsync(c);
            foreach(var row in legacy)
            {
                var role=row.GetValue("Role",Roles.Member).AsString;
                Rules.Require(Roles.All.Contains(role),"migration_role","Perfil legado desconhecido. Revise a migração.",409);
                var id=row["_id"].AsString;
                var permissions=role==Roles.Admin?Access.Permissions.ToList():Access.ForRole(role);
                await users.UpdateOneAsync(s,filter&Builders<BsonDocument>.Filter.Eq("_id",id),Builders<BsonDocument>.Update.Set("Permissions",new BsonArray(permissions)).Inc("AuthRevision",1L).Inc("AccessRevision",1L),cancellationToken:c);
                await db.Sessions.DeleteManyAsync(s,x=>x.HouseId==house&&x.UserId==id,cancellationToken:c);
                await db.Devices.DeleteManyAsync(s,x=>x.HouseId==house&&x.UserId==id,cancellationToken:c);
                await db.Audit.InsertOneAsync(s,new AuditEvent{HouseId=house,ActorId="deployment:authorized-legacy-access-migration",Action="access.legacy_migrated",ResourceId=id,Detail="Migração inicial autorizada; revise as permissões antes do uso real."},cancellationToken:c);
            }
            return true;
        },ct);
    }
}
