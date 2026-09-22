using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;
using Terreiro.Data;
using Terreiro.Domain;
namespace Terreiro.Service;

public sealed class AuthService(MongoStore db, string houseId)
{
    private readonly PasswordHasher<User> hasher = new();
    public string HouseId { get; } = houseId;
    public async Task Bootstrap(string login, string password, CancellationToken ct)
    {
        Rules.Password(password);
        if (await db.Users.Find(u => u.HouseId == HouseId && u.Role == Roles.Admin).AnyAsync(ct)) return;
        var user = new User { HouseId = HouseId, Name = "Administrador da casa", Login = Rules.Login(login), Role = Roles.Admin, Permissions = Access.Permissions.ToList() };
        user.PasswordHash = hasher.HashPassword(user, password);
        try { await db.Users.InsertOneAsync(user, cancellationToken: ct); }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey) { }
    }
    public async Task<(User User, string Token)> Login(string login, string password, CancellationToken ct)
    {
        var normalized = Rules.Login(login);
        Rules.Require(password.Length <= 128, "login_failed", "Usuário ou senha inválidos.", 401);
        var user = await db.Users.Find(u => u.HouseId == HouseId && u.Login == normalized).FirstOrDefaultAsync(ct);
        if (user == null)
        {
            // Same expensive password hashing path for nonexistent identities.
            _ = hasher.HashPassword(new User(), password);
            throw new RuleException("login_failed", "Usuário ou senha inválidos.", 401);
        }
        Rules.Require(user.Active && (user.LockedUntil == null || user.LockedUntil <= DateTime.UtcNow), "login_failed", "Usuário ou senha inválidos, ou acesso temporariamente indisponível.", 401);
        var valid = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (valid == PasswordVerificationResult.Failed)
        {
            var updated = await db.Users.FindOneAndUpdateAsync(u => u.Id == user.Id,
                Builders<User>.Update.Inc(u => u.FailedLogins, 1),
                new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After }, ct);
            if (updated.FailedLogins >= 5)
                await db.Users.UpdateOneAsync(u => u.Id == user.Id, Builders<User>.Update.Set(u => u.LockedUntil, DateTime.UtcNow.AddMinutes(15)), cancellationToken: ct);
            throw new RuleException("login_failed", "Usuário ou senha inválidos.", 401);
        }
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        await db.Transaction(async (s, c) =>
        {
            // Revalidate the credential version inside the transaction. A concurrent password
            // change must not leave a newly issued session based on the previous password.
            var account = await db.Users.UpdateOneAsync(s,
                u => u.Id == user.Id && u.HouseId == HouseId && u.Active && u.PasswordHash == user.PasswordHash,
                Builders<User>.Update.Set(u => u.FailedLogins, 0).Set(u => u.LockedUntil, null).Inc(u => u.AuthRevision, 1),
                cancellationToken: c);
            Rules.Require(account.MatchedCount == 1, "login_failed", "Credenciais alteradas. Entre novamente.", 401);
            await db.Sessions.InsertOneAsync(s, new LoginSession { Id = Rules.Hash(token), HouseId = HouseId, UserId = user.Id, ExpiresAt = DateTime.UtcNow.AddHours(12) }, cancellationToken: c);
            await db.Audit.InsertOneAsync(s, new AuditEvent { HouseId = HouseId, ActorId = user.Id, Action = "auth.login", ResourceId = user.Id }, cancellationToken: c);
            return true;
        }, ct);
        return (user, token);
    }
    public async Task<User?> Authenticate(string? token, CancellationToken ct)
    {
        if (token == null || token.Length != 64) return null;
        var id = Rules.Hash(token);
        var session = await db.Sessions.Find(s => s.Id == id && s.HouseId == HouseId && s.ExpiresAt > DateTime.UtcNow).FirstOrDefaultAsync(ct);
        return session == null ? null : await db.Users.Find(u => u.Id == session.UserId && u.HouseId == HouseId && u.Active).FirstOrDefaultAsync(ct);
    }
    public async Task Logout(string? token, CancellationToken ct)
    {
        if (token == null) return;
        var hash = Rules.Hash(token);
        await db.Sessions.DeleteOneAsync(s => s.Id == hash && s.HouseId == HouseId, ct);
        await db.Devices.DeleteManyAsync(d => d.SessionId == hash && d.HouseId == HouseId, ct);
    }
    public async Task ChangePassword(User user, string current, string next, CancellationToken ct)
    {
        Rules.Password(next);
        Rules.Require(current.Length <= 128 && hasher.VerifyHashedPassword(user, user.PasswordHash, current) != PasswordVerificationResult.Failed, "password_wrong", "Senha atual incorreta.", 400);
        Rules.Require(current != next, "password_unchanged", "Escolha uma nova senha.");
        var newHash = hasher.HashPassword(user, next);
        await db.Transaction(async (s, c) =>
        {
            var result = await db.Users.UpdateOneAsync(s, u => u.Id == user.Id && u.PasswordHash == user.PasswordHash,
                Builders<User>.Update.Set(u => u.PasswordHash, newHash).Set(u => u.MustChangePassword, false).Inc(u=>u.AuthRevision,1).Set(u=>u.RecoveryHashes,new List<string>()), cancellationToken: c);
            Rules.Require(result.ModifiedCount == 1, "conflict", "Credenciais já alteradas. Entre novamente.", 409);
            await db.Sessions.DeleteManyAsync(s, x => x.UserId == user.Id && x.HouseId == HouseId, cancellationToken: c);
            await db.Devices.DeleteManyAsync(s, x => x.UserId == user.Id && x.HouseId == HouseId, cancellationToken: c);
            await db.Audit.InsertOneAsync(s, new AuditEvent { HouseId = HouseId, ActorId = user.Id, Action = "auth.password_changed", ResourceId = user.Id }, cancellationToken: c);
            return true;
        }, ct);
    }
    public async Task<User> CreateMember(User actor, string name, string login, string password, string role, CancellationToken ct)
    {
        Rules.Require(actor.Role == Roles.Admin, "forbidden", "Apenas a administração pode criar acessos.", 403);
        Rules.Require(role is Roles.Member or Roles.Admin, "role_review_required", "Crie o acesso como membro. Outros perfis são atribuídos em Acessos, com aprovação independente.");
        Rules.Password(password);
        var member = new User { Name = Rules.Text(name, 2, 100, "Nome"), Login = Rules.Login(login), HouseId = HouseId, Role = role, Permissions = Access.ForRole(role) };
        member.PasswordHash = hasher.HashPassword(member, password);
        await db.Transaction(async (s, c) =>
        {
            var modules=new ModuleStore(db);
            await modules.Set<HouseGate>().UpdateOneAsync(s,x=>x.Id==HouseId,Builders<HouseGate>.Update.Inc(x=>x.Sequence,1),cancellationToken:c);
            var actorLock=await db.Users.UpdateOneAsync(s,x=>x.Id==actor.Id&&x.HouseId==HouseId&&x.Active&&x.AuthRevision==actor.AuthRevision,Builders<User>.Update.Inc(x=>x.OperationRevision,1),cancellationToken:c);
            Rules.Require(actorLock.MatchedCount==1,"access_changed","Seu acesso foi alterado.",401);
            if(role==Roles.Admin)
                Rules.Require(await db.Users.CountDocumentsAsync(s,x=>x.HouseId==HouseId&&x.Role==Roles.Admin,cancellationToken:c)==1,"initial_admin_only","A configuração inicial permite apenas o segundo administrador. Use a revisão de acesso para os demais.",409);
            await db.Users.InsertOneAsync(s, member, cancellationToken: c);
            await db.Audit.InsertOneAsync(s, new AuditEvent { HouseId = HouseId, ActorId = actor.Id, Action = role==Roles.Admin?"setup.second_technical_admin_created":"member.created", ResourceId = member.Id }, cancellationToken: c);
            return true;
        }, ct);
        return member;
    }
}
