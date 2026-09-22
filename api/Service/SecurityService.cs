using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;
using Terreiro.Data;
using Terreiro.Domain;
namespace Terreiro.Service;
public sealed class SecurityService(ModuleStore m)
{
    private readonly PasswordHasher<User> hasher=new();
    public async Task<object> Change(User actor,string action,Command input,CancellationToken ct)
    {
        Access.Demand(actor,"security.manage");
        return await m.Execute<object>(actor,"security."+action,input.OperationId,input,async(s,c)=>
        {
            var d=input.Data;var h=actor.HouseId;
            if(action=="request")
            {
                var id=Fields.Required(d,"userId");var target=await m.Root.Users.Find(s,x=>x.Id==id&&x.HouseId==h).FirstOrDefaultAsync(c);Rules.Require(target!=null,"not_found","Usuário não encontrado.",404);
                var role=Fields.Text(d,"role");Rules.Require(Roles.All.Contains(role),"role_invalid","Perfil inválido.");
                var change=new AccessChange{HouseId=h,TargetUserId=id,RequestedBy=actor.Id,TargetAuthRevision=target.AccessRevision,NewRole=role,Active=Fields.Bool(d,"active",true),IsMember=Fields.Bool(d,"isMember",true),Reason=Rules.Text(Fields.Text(d,"reason"),8,500,"Motivo")};await m.Insert(s,change,c);return change;
            }
            Rules.Require(action=="review","action_unknown","Operação de acesso desconhecida.",404);var request=await m.Get<AccessChange>(s,h,Fields.Required(d,"changeId"),c);BusinessRules.Revision(request.Revision,input.Revision);BusinessRules.Independent(request.RequestedBy,actor.Id);BusinessRules.Independent(request.TargetUserId,actor.Id);Rules.Require(request.State=="Pending","change_state","Solicitação já revisada.",409);
            if(Fields.Bool(d,"approve"))
            {
                var target=await m.Root.Users.Find(s,x=>x.HouseId==h&&x.Id==request.TargetUserId).FirstOrDefaultAsync(c);Rules.Require(target!=null&&target.AccessRevision==request.TargetAuthRevision,"access_changed","O acesso do usuário foi alterado; faça nova solicitação.",409);
                if(target.Role==Roles.Admin && (!request.Active||request.NewRole!=Roles.Admin))Rules.Require(await m.Root.Users.CountDocumentsAsync(s,x=>x.HouseId==h&&x.Role==Roles.Admin&&x.Active&&x.Id!=target.Id,cancellationToken:c)>0,"last_admin","Mantenha ao menos um administrador ativo.",409);
                await m.Root.Users.UpdateOneAsync(s,x=>x.Id==target.Id&&x.HouseId==h,Builders<User>.Update.Set(x=>x.Role,request.NewRole).Set(x=>x.Permissions,Access.ForRole(request.NewRole)).Set(x=>x.Active,request.Active).Set(x=>x.IsMember,request.IsMember).Inc(x=>x.AuthRevision,1).Inc(x=>x.AccessRevision,1),cancellationToken:c);
                await m.Root.Sessions.DeleteManyAsync(s,x=>x.HouseId==h&&x.UserId==target.Id,cancellationToken:c);await m.Root.Devices.DeleteManyAsync(s,x=>x.HouseId==h&&x.UserId==target.Id,cancellationToken:c);
                if(!request.Active||!request.IsMember)
                {
                    var plans=await m.Root.Cleanings.Find(s,x=>x.HouseId==h&&x.Status=="Published"&&x.EndsAt>DateTime.UtcNow&&x.Assignments.Any(a=>a.UserId==target.Id)).ToListAsync(c);
                    foreach(var plan in plans){var a=plan.Assignments.Single(x=>x.UserId==target.Id);a.Response="NeedsReview";plan.Revision++;await m.Root.Cleanings.ReplaceOneAsync(s,x=>x.Id==plan.Id&&x.HouseId==h,plan,cancellationToken:c);await m.Notify(s,h,plan.CoordinatorId,"access-plan:"+request.Id+":"+plan.Id,"Revisar cobertura da limpeza","Um vínculo mudou. Revise a equipe sem alterar a participação passada.","/limpezas/"+plan.Id,"Cleaning",c);}
                }
                request.State="Approved";
            }else request.State="Rejected";
            request.ReviewedBy=actor.Id;await m.Save(s,request,c);return request;
        },ct);
    }
    public async Task<string[]> GenerateRecovery(User actor,string password,CancellationToken ct)
    {
        Rules.Require(password.Length<=128&&hasher.VerifyHashedPassword(actor,actor.PasswordHash,password)!=PasswordVerificationResult.Failed,"reauth_failed","Senha atual incorreta.",403);
        var codes=Enumerable.Range(0,8).Select(_=>Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()).ToArray();var hashes=codes.Select(x=>Rules.Hash(actor.Id+"|"+x)).ToList();
        await m.Root.Transaction(async(s,c)=>
        {
            var result=await m.Root.Users.UpdateOneAsync(s,x=>x.Id==actor.Id&&x.HouseId==actor.HouseId&&x.PasswordHash==actor.PasswordHash,Builders<User>.Update.Set(x=>x.RecoveryHashes,hashes),cancellationToken:c);Rules.Require(result.MatchedCount==1,"access_changed","Credenciais foram alteradas.",409);
            await m.Root.Audit.InsertOneAsync(s,new AuditEvent{HouseId=actor.HouseId,ActorId=actor.Id,Action="auth.recovery_codes_generated",ResourceId=actor.Id},cancellationToken:c);return true;
        },ct);
        // Never persist plaintext codes or serialize them into an idempotency record.
        return codes;
    }
    public async Task Recover(string house,string login,string code,string newPassword,CancellationToken ct)
    {
        Rules.Password(newPassword);Rules.Require(code.Length is >0 and <=100,"recovery_failed","Código ou usuário inválido.",401);var normalized=Rules.Login(login);
        var user=await m.Root.Users.Find(x=>x.HouseId==house&&x.Login==normalized&&x.Active).FirstOrDefaultAsync(ct);Rules.Require(user!=null,"recovery_failed","Código ou usuário inválido.",401);
        var hash=Rules.Hash(user.Id+"|"+code.Trim().ToLowerInvariant());Rules.Require(user.RecoveryHashes.Contains(hash),"recovery_failed","Código ou usuário inválido.",401);var passwordHash=hasher.HashPassword(user,newPassword);
        await m.Root.Transaction(async(s,c)=>
        {
            var result=await m.Root.Users.UpdateOneAsync(s,x=>x.Id==user.Id&&x.HouseId==house&&x.RecoveryHashes.Contains(hash),Builders<User>.Update.Set(x=>x.PasswordHash,passwordHash).Set(x=>x.MustChangePassword,false).Set(x=>x.LockedUntil,null).Set(x=>x.FailedLogins,0).Pull(x=>x.RecoveryHashes,hash).Inc(x=>x.AuthRevision,1),cancellationToken:c);Rules.Require(result.ModifiedCount==1,"recovery_failed","Código inválido ou já utilizado.",401);
            await m.Root.Sessions.DeleteManyAsync(s,x=>x.UserId==user.Id&&x.HouseId==house,cancellationToken:c);await m.Root.Devices.DeleteManyAsync(s,x=>x.UserId==user.Id&&x.HouseId==house,cancellationToken:c);
            await m.Root.Audit.InsertOneAsync(s,new AuditEvent{HouseId=house,ActorId=user.Id,Action="auth.recovered",ResourceId=user.Id},cancellationToken:c);return true;
        },ct);
    }
    public async Task Reauthenticate(User user,string token,string password,CancellationToken ct)
    {
        Rules.Require(password.Length<=128&&hasher.VerifyHashedPassword(user,user.PasswordHash,password)!=PasswordVerificationResult.Failed,"reauth_failed","Senha atual incorreta.",403);
        await m.Root.Sessions.UpdateOneAsync(x=>x.Id==Rules.Hash(token)&&x.HouseId==user.HouseId&&x.UserId==user.Id,Builders<LoginSession>.Update.Set(x=>x.ReauthenticatedAt,DateTime.UtcNow),cancellationToken:ct);
    }
    public async Task RequireRecent(User user,string token,CancellationToken ct)
    {
        var id=Rules.Hash(token);var after=DateTime.UtcNow.AddMinutes(-10);Rules.Require(await m.Root.Sessions.Find(x=>x.Id==id&&x.UserId==user.Id&&x.HouseId==user.HouseId&&x.ReauthenticatedAt>after).AnyAsync(ct),"reauth_required","Confirme sua senha para realizar esta operação sensível.",403);
    }
}
