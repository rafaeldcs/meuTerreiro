using System.Text;
using System.Text.Json;
using MongoDB.Driver;
using Terreiro.Data;
using Terreiro.Domain;
using Terreiro.Service;
namespace Terreiro.WebAPI;

public static class ModuleEndpoints
{
    private static User Actor(HttpContext c)=>(User)c.Items["actor"]!;
    private static void Validate(Command c)
    {
        Rules.Require(c!=null&&Guid.TryParse(c.OperationId,out _)&&c.Data.ValueKind==JsonValueKind.Object,"command_invalid","Dados da operação inválidos.");
        Rules.Require(c.Revision>=0,"revision_invalid","Versão inválida.");
    }
    public static void MapAdministrativeModules(this WebApplication app)
    {
        app.MapGet("/api/pix/config",(PixService svc)=>Results.Ok(svc.Configuration()));
        app.MapPost("/api/pix/intents",async(Command input,HttpContext c,PixService svc,CancellationToken ct)=>{Validate(input);return Results.Ok(await svc.Create(Actor(c),input,ct));});
        app.MapGet("/api/notification-feed",async(int? page,string? filter,HttpContext c,ModuleStore m,CancellationToken ct)=>
        {
            var u=Actor(c);var f=Builders<Notification>.Filter.Eq(x=>x.HouseId,u.HouseId)&Builders<Notification>.Filter.Eq(x=>x.UserId,u.Id);
            f&=filter=="archived"?Builders<Notification>.Filter.Ne(x=>x.ArchivedAt,null):Builders<Notification>.Filter.Eq(x=>x.ArchivedAt,null);
            if(filter=="unread")f&=Builders<Notification>.Filter.Eq(x=>x.ReadAt,null);
            var index=Math.Clamp(page??1,1,100000);var items=await m.Root.Notifications.Find(f).SortByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id).Skip((index-1)*30).Limit(30).ToListAsync(ct);
            return Results.Ok(new{items=items.Select(n=>new{n.Id,n.Title,n.Body,n.Path,n.CreatedAt,n.ReadAt,n.OpenedAt,n.ArchivedAt,n.ExpiresAt}),total=await m.Root.Notifications.CountDocumentsAsync(f,cancellationToken:ct),page=index,pageSize=30});
        });
        app.MapPost("/api/notifications/{id}/open",async(string id,HttpContext c,ModuleStore m,CancellationToken ct)=>
        {
            var u=Actor(c);var f=Builders<Notification>.Filter.Eq(x=>x.Id,id)&Builders<Notification>.Filter.Eq(x=>x.HouseId,u.HouseId)&Builders<Notification>.Filter.Eq(x=>x.UserId,u.Id);
            await m.Root.Notifications.UpdateOneAsync(f&Builders<Notification>.Filter.Eq(x=>x.OpenedAt,null),Builders<Notification>.Update.Set(x=>x.OpenedAt,DateTime.UtcNow),cancellationToken:ct);
            await m.Root.Notifications.UpdateOneAsync(f&Builders<Notification>.Filter.Eq(x=>x.ReadAt,null),Builders<Notification>.Update.Set(x=>x.ReadAt,DateTime.UtcNow),cancellationToken:ct);return Results.NoContent();
        });
        app.MapPost("/api/notifications/{id}/archive",async(string id,HttpContext c,ModuleStore m,CancellationToken ct)=>
        {
            var u=Actor(c);await m.Root.Notifications.UpdateOneAsync(x=>x.Id==id&&x.HouseId==u.HouseId&&x.UserId==u.Id,Builders<Notification>.Update.Set(x=>x.ArchivedAt,DateTime.UtcNow),cancellationToken:ct);return Results.NoContent();
        });
        app.MapGet("/api/security/users",async(HttpContext c,ModuleStore m,CancellationToken ct)=>
        {
            var u=Actor(c);Access.Demand(u,"security.manage");var people=await m.Root.Users.Find(x=>x.HouseId==u.HouseId).SortBy(x=>x.Name).Limit(1000).ToListAsync(ct);
            return Results.Ok(people.Select(x=>new{x.Id,x.Name,x.Login,x.Role,x.Active,x.IsMember,x.AccessRevision}));
        });
        app.MapGet("/api/cleanings/{id}/candidates",async(string id,HttpContext c,ModuleStore m,CancellationToken ct)=>
        {
            var u=Actor(c);var plan=await m.Root.Cleanings.Find(x=>x.Id==id&&x.HouseId==u.HouseId).FirstOrDefaultAsync(ct);
            Rules.Require(plan!=null,"not_found","Limpeza não encontrada.",404);Rules.Require((Roles.Coordinates(u)&&(u.Role==Roles.Admin||plan.CoordinatorId==u.Id))||plan.Assignments.Any(a=>a.UserId==u.Id&&!a.Dispensed),"forbidden","Acesso restrito à atividade.",403);
            var assigned=plan.Assignments.Select(x=>x.UserId).ToArray();var people=await m.Root.Users.Find(x=>x.HouseId==u.HouseId&&x.Active&&x.IsMember&&!assigned.Contains(x.Id)).SortBy(x=>x.Name).Limit(1000).ToListAsync(ct);
            return Results.Ok(people.Select(x=>new{x.Id,x.Name}));
        });
        app.MapGet("/api/dashboard",async(HttpContext c,QueryService q,CancellationToken ct)=>Results.Ok(await q.Dashboard(Actor(c),ct)));
        app.MapGet("/api/lookups",async(HttpContext c,QueryService q,CancellationToken ct)=>Results.Ok(await q.Lookups(Actor(c),ct)));
        app.MapGet("/api/members/{id}/exemptions",async(string id,HttpContext c,QueryService svc,CancellationToken ct)=>
            Results.Ok(await svc.MemberExemptions(Actor(c),id,ct)));
        app.MapGet("/api/dues",async(int? page,int? pageSize,int? year,int? month,string? state,string? q,HttpContext c,QueryService svc,CancellationToken ct)=>
            Results.Ok(await svc.Dues(Actor(c),page??1,pageSize??20,year,month,state,q,ct)));
        app.MapGet("/api/dues/{id}",async(string id,HttpContext c,QueryService svc,CancellationToken ct)=>
            Results.Ok(await svc.DueDetails(Actor(c),id,ct)));
        app.MapGet("/api/records/{resource}",async(string resource,int? page,int? pageSize,HttpContext c,QueryService q,CancellationToken ct)=>Results.Ok(await q.List(Actor(c),resource,page??1,pageSize??30,ct)));
        app.MapPost("/api/finance/{action}",async(string action,Command input,HttpContext c,FinanceService svc,SecurityService sec,CancellationToken ct)=>
        {
            Validate(input);if(action is "approval.review" or "refund.execute" or "expense.pay" or "transfer" or "closing.review" or "cash.review")await sec.RequireRecent(Actor(c),c.Request.Cookies["terreiro_session"]!,ct);
            return Results.Ok(await svc.Command(Actor(c),action,input,ct));
        });
        app.MapPost("/api/operations/{action}",async(string action,Command input,HttpContext c,OperationsService svc,SecurityService sec,CancellationToken ct)=>
        {
            Validate(input);if(action.EndsWith(".review",StringComparison.Ordinal)||action=="budget.approve")await sec.RequireRecent(Actor(c),c.Request.Cookies["terreiro_session"]!,ct);
            return Results.Ok(await svc.Command(Actor(c),action,input,ct));
        });
        app.MapPost("/api/cleaning-actions/{action}",async(string action,Command input,HttpContext c,AdvancedCleaningService svc,CancellationToken ct)=>{Validate(input);return Results.Ok(await svc.Command(Actor(c),action,input,ct));});
        app.MapPost("/api/reconciliation/{action}",async(string action,Command input,HttpContext c,ReconciliationService svc,CancellationToken ct)=>{Validate(input);return Results.Ok(await svc.Command(Actor(c),action,input,ct));});
        app.MapGet("/api/reports/finance/{period}",async(string period,HttpContext c,QueryService q,CancellationToken ct)=>Results.Ok(await q.Report(Actor(c),period,ct)));
        app.MapGet("/api/reports/ledger/{period}.csv",async(string period,HttpContext c,QueryService q,CancellationToken ct)=>Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(await q.ExportLedger(Actor(c),period,ct))).ToArray(),"text/csv; charset=utf-8","movimentos-"+BusinessRules.Month(period)+".csv"));
        app.MapGet("/api/receipts/{id}",async(string id,HttpContext c,ModuleStore m,CancellationToken ct)=>
        {
            var u=Actor(c);var receipt=await m.Get<Receipt>(u.HouseId,id,ct);var admin=Access.Has(u,"finance.read");
            var assignments=await m.Set<Allocation>().Find(x=>x.HouseId==u.HouseId&&x.ReceiptId==id).ToListAsync(ct);var own=assignments.Where(x=>x.MemberId==u.Id).ToArray();
            Rules.Require(admin||own.Length>0,"forbidden","Recibo não disponível para esta conta.",403);
            return Results.Ok(new{receipt.Id,receipt.CreatedAt,receipt.OccurredOn,receipt.Verification,amountCents=admin?receipt.AmountCents:own.Sum(x=>x.AmountCents),reversedCents=admin?receipt.RefundedCents:own.Sum(x=>x.ReversedCents),allocations=(admin?assignments.ToArray():own).Select(x=>new{x.Id,x.DueId,x.Kind,x.AmountCents,x.ReversedCents})});
        });
        app.MapPost("/api/evidence/upload",async(HttpContext c,EvidenceService svc,CancellationToken ct)=>
        {
            var form=await c.Request.ReadFormAsync(ct);Rules.Require(form.Files.Count==1,"file_count","Envie um arquivo por operação.");var file=form.Files[0];Rules.Require(file.Length>0&&file.Length<=10*1024*1024,"file_size","O arquivo deve ter até 10 MB.",413);
            using var memory=new MemoryStream();await file.CopyToAsync(memory,ct);var ids=form["dueIds"].ToString().Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);var amountText=form["declaredCents"].ToString();long? amount=null;
            if(!string.IsNullOrWhiteSpace(amountText)){Rules.Require(long.TryParse(amountText,out var cents),"amount_invalid","Valor declarado inválido.");amount=cents;}
            var result=await svc.Upload(Actor(c),form["operationId"].ToString(),file.FileName,form["purpose"].ToString(),form["memberId"].ToString(),ids,amount,form["declaredDate"].ToString(),memory.ToArray(),ct);
            return Results.Ok(new{result.Id,result.OriginalName,result.ScanState,result.AnalysisState,result.ReviewState,result.CreatedAt});
        });
        app.MapGet("/api/evidence/{id}/file",async(string id,HttpContext c,EvidenceService svc,CancellationToken ct)=>
        {
            var(e,bytes)=await svc.Download(Actor(c),id,ct);c.Response.Headers["Content-Security-Policy"]="sandbox; default-src 'none'";return Results.File(bytes,e.ContentType,e.OriginalName);
        });
        app.MapGet("/api/evidence/{id}/analysis",async(string id,HttpContext c,EvidenceService svc,CancellationToken ct)=>
        {
            var e=await svc.Authorized(Actor(c),id,ct);return Results.Ok(new{e.Id,e.OriginalName,e.ScanState,e.AnalysisState,e.ReviewState,e.DeclaredCents,e.DeclaredDate,e.ExtractedAmount,e.ExtractedReference,e.PossibleScheduled,e.PageCount,e.AnalysisNote,untrustedText=e.ExtractedText,notProofOfPayment=true});
        });
        app.MapPost("/api/evidence/{id}/retry",async(string id,Command input,HttpContext c,EvidenceService svc,ModuleStore m,CancellationToken ct)=>
        {
            Validate(input);var u=Actor(c);var e=await svc.Authorized(u,id,ct);Rules.Require(e.ScanState=="Quarantine"&&e.Attempts>=6,"retry_not_ready","Aguarde as tentativas automáticas ou envie outro arquivo.",409);
            return Results.Ok(await m.Execute(u,"evidence.retry",input.OperationId,input,async(s,t)=>{var current=await m.Get<Evidence>(s,u.HouseId,id,t);BusinessRules.Revision(current.Revision,input.Revision);current.Attempts=0;current.LeaseUntil=null;await m.Save(s,current,t);return new{current.Id,current.ScanState};},ct));
        });
        app.MapPost("/api/reviews/{action}",async(string action,Command input,HttpContext c,EvidenceService svc,CancellationToken ct)=>{Validate(input);return Results.Ok(await svc.Review(Actor(c),action,input,ct));});
        app.MapGet("/api/preferences",async(HttpContext c,CommunicationService svc,CancellationToken ct)=>Results.Ok(await svc.Preferences(Actor(c),ct)));
        app.MapPost("/api/preferences",async(Command input,HttpContext c,CommunicationService svc,CancellationToken ct)=>{Validate(input);return Results.Ok(await svc.SavePreferences(Actor(c),input,ct));});
        app.MapPost("/api/security/{action}",async(string action,Command input,HttpContext c,SecurityService svc,CancellationToken ct)=>{Validate(input);await svc.RequireRecent(Actor(c),c.Request.Cookies["terreiro_session"]!,ct);return Results.Ok(await svc.Change(Actor(c),action,input,ct));});
        app.MapPost("/api/auth/reauthenticate",async(PasswordOnly input,HttpContext c,SecurityService svc,CancellationToken ct)=>{await svc.Reauthenticate(Actor(c),c.Request.Cookies["terreiro_session"]!,input.Password??"",ct);return Results.NoContent();}).RequireRateLimiting("login");
        app.MapPost("/api/auth/recovery-codes",async(PasswordOnly input,HttpContext c,SecurityService svc,CancellationToken ct)=>Results.Ok(new{codes=await svc.GenerateRecovery(Actor(c),input.Password??"",ct)})).RequireRateLimiting("login");
        app.MapPost("/api/auth/recover",async(RecoveryInput input,SecurityService svc,AuthService auth,CancellationToken ct)=>{await svc.Recover(auth.HouseId,input.Login??"",input.Code??"",input.NewPassword??"",ct);return Results.NoContent();}).RequireRateLimiting("login");
        app.MapGet("/api/audit",async(int? page,HttpContext c,ModuleStore m,CancellationToken ct)=>
        {
            var u=Actor(c);Access.Demand(u,"audit.read");var index=Math.Clamp(page??1,1,100000);var rows=await m.Root.Audit.Find(x=>x.HouseId==u.HouseId).SortByDescending(x=>x.At).Skip((index-1)*30).Limit(30).ToListAsync(ct);return Results.Ok(new{items=rows,page=index,pageSize=30,total=await m.Root.Audit.CountDocumentsAsync(x=>x.HouseId==u.HouseId,cancellationToken:ct)});
        });
    }
    public sealed record PasswordOnly(string? Password);
    public sealed record RecoveryInput(string? Login,string? Code,string? NewPassword);
}
