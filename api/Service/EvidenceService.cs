using System.Security.Cryptography;
using System.Text.Json;
using MongoDB.Driver;
using Terreiro.Data;
using Terreiro.Domain;
namespace Terreiro.Service;

public sealed class EvidenceService(ModuleStore m,string directory)
{
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public const int MaxBytes=10*1024*1024;
    public async Task<Evidence> Upload(User actor,string operationId,string filename,string purpose,string memberId,string[] dueIds,long? declaredCents,string? declaredDate,byte[] bytes,CancellationToken ct)
    {
        Rules.Require(bytes.Length is >0 and <=MaxBytes,"file_size","Envie um arquivo de até 10 MB.");
        Rules.Require(purpose is "Receipt" or "Expense" or "Document","purpose_invalid","Finalidade do arquivo inválida.");
        if(purpose=="Document")Access.Demand(actor,"office.write");
        if(purpose=="Expense")Access.Demand(actor,"finance.write");
        if(memberId!=actor.Id)Access.Demand(actor,"finance.write");
        Rules.Require(dueIds.Length<=50,"dues_limit","Selecione no máximo 50 mensalidades.");
        declaredDate=string.IsNullOrWhiteSpace(declaredDate)?null:declaredDate.Trim();
        if(declaredCents!=null)BusinessRules.Money(declaredCents.Value);if(declaredDate!=null)BusinessRules.Date(declaredDate);
        var contentType=Detect(bytes);var extension=Path.GetExtension(filename).ToLowerInvariant();
        Rules.Require(contentType switch{"application/pdf"=>extension==".pdf","image/png"=>extension==".png","image/jpeg"=>extension is ".jpg" or ".jpeg",_=>false},"file_type","Somente PDF, PNG ou JPEG com conteúdo e extensão compatíveis.");
        var hash=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var input=new{purpose,memberId,dueIds,declaredCents,declaredDate,hash};
        var key=Guid.NewGuid().ToString("N")+extension;System.IO.Directory.CreateDirectory(DirectoryPath);
        var fullPath=Path.Combine(DirectoryPath,key);await File.WriteAllBytesAsync(fullPath,bytes,ct);
        try
        {
            var result=await m.Execute<Evidence>(actor,"evidence.upload",operationId,input,async(s,c)=>
            {
                var now=DateTime.UtcNow.Date;
                Rules.Require(await m.Set<Evidence>().CountDocumentsAsync(s,x=>x.HouseId==actor.HouseId && x.UploadedBy==actor.Id && x.CreatedAt>=now,cancellationToken:c)<30,"upload_limit","Limite diário de arquivos atingido; procure a administração.",429);
                var member=await m.Root.Users.Find(s,x=>x.HouseId==actor.HouseId && x.Id==memberId && x.Active).FirstOrDefaultAsync(c);Rules.Require(member!=null,"member_invalid","Membro não encontrado.",404);
                foreach(var id in dueIds){var due=await m.Get<Due>(s,actor.HouseId,id,c);Rules.Require(due.MemberId==memberId,"due_owner","Selecione somente mensalidades do beneficiário informado.",403);}
                var duplicate=await m.Set<Evidence>().Find(s,x=>x.HouseId==actor.HouseId && x.Sha256==hash).FirstOrDefaultAsync(c);
                var record=new Evidence{HouseId=actor.HouseId,UploadedBy=actor.Id,MemberId=memberId,Purpose=purpose,DueIds=dueIds.ToList(),OriginalName=Path.GetFileName(filename)[..Math.Min(Path.GetFileName(filename).Length,180)],ContentType=contentType,ObjectKey=key,Sha256=hash,Length=bytes.Length,DeclaredCents=declaredCents,DeclaredDate=declaredDate,DuplicateOf=duplicate?.Id};
                await m.Insert(s,record,c);await m.Notify(s,actor.HouseId,memberId,"evidence:"+record.Id,"Arquivo recebido","O arquivo foi recebido. Isso não confirma um pagamento.","/comprovantes","Finance",c);return record;
            },ct);
            if(result.ObjectKey!=key)File.Delete(fullPath);return result;
        }
        catch{File.Delete(fullPath);throw;}
    }
    public async Task<Evidence> Authorized(User actor,string id,CancellationToken ct)
    {
        var e=await m.Get<Evidence>(actor.HouseId,id,ct);
        var authorized=e.Purpose=="Document"?Access.Has(actor,"office.read"):e.MemberId==actor.Id||Access.Has(actor,"finance.read");
        Rules.Require(authorized,"not_found","Arquivo não encontrado.",404);return e;
    }
    public async Task<(Evidence Evidence,byte[] Bytes)> Download(User actor,string id,CancellationToken ct)
    {
        var e=await Authorized(actor,id,ct);Rules.Require(e.ScanState=="Clean" && e.PurgedAt==null,"quarantine","Arquivo não liberado pela análise de segurança.",409);
        Rules.Require(Path.GetFileName(e.ObjectKey)==e.ObjectKey,"file_integrity","Identificador de armazenamento inválido.",409);
        var path=Path.Combine(DirectoryPath,e.ObjectKey);Rules.Require(File.Exists(path),"file_missing","Arquivo indisponível. A administração precisa verificar o armazenamento.",503);
        var bytes=await File.ReadAllBytesAsync(path,ct);Rules.Require(Convert.ToHexString(SHA256.HashData(bytes)).Equals(e.Sha256,StringComparison.OrdinalIgnoreCase),"file_integrity","A integridade do arquivo diverge do registro original.",409);
        await m.Root.Audit.InsertOneAsync(new AuditEvent{HouseId=actor.HouseId,ActorId=actor.Id,Action="evidence.download",ResourceId=e.Id},cancellationToken:ct);return(e,bytes);
    }
    public async Task<object> Review(User actor,string action,Command input,CancellationToken ct)
    {
        if(action!="reply")Access.Demand(actor,"finance.write");
        return await m.Execute<object>(actor,"evidence."+action,input.OperationId,input,async(s,c)=>
        {
            var d=input.Data;
            if(action=="open")
            {
                var e=await m.Get<Evidence>(s,actor.HouseId,Fields.Required(d,"evidenceId"),c);
                var reason=Fields.Text(d,"reasonCode");Rules.Require(new[]{"Unreadable","ValueMismatch","CreditNotFound","Scheduled","PossibleDuplicate","RecipientMismatch","Other"}.Contains(reason),"reason_invalid","Motivo de revisão inválido.");
                var review=new ReviewCase{HouseId=actor.HouseId,EvidenceId=e.Id,MemberId=e.MemberId,ReasonCode=reason};review.Messages.Add(new CaseMessage{AuthorId=actor.Id,Text=Rules.Text(Fields.Text(d,"message"),5,2000,"Mensagem ao membro")});
                await m.Insert(s,review,c);e.ReviewState="UnderReview";await m.Save(s,e,c);await m.Notify(s,actor.HouseId,e.MemberId,"review-open:"+review.Id,"Conferência do comprovante","A tesouraria precisa de uma informação. Consulte a solicitação no aplicativo.","/revisoes","Finance",c);return review;
            }
            var item=await m.Get<ReviewCase>(s,actor.HouseId,Fields.Required(d,"reviewId"),c);BusinessRules.Revision(item.Revision,input.Revision);
            Rules.Require(item.MemberId==actor.Id||Access.Has(actor,"finance.read"),"not_found","Revisão não encontrada.",404);Rules.Require(item.State!="Closed","review_closed","A revisão está encerrada.",409);
            if(action=="reply")
            {
                var internalNote=Fields.Bool(d,"internal");Rules.Require(!internalNote||Access.Has(actor,"finance.write"),"forbidden","Anotações internas são da tesouraria.",403);
                item.Messages.Add(new CaseMessage{AuthorId=actor.Id,Text=Rules.Text(Fields.Text(d,"message"),2,2000,"Mensagem"),Internal=internalNote});
                Rules.Require(item.Messages.Count<=200,"thread_limit","Limite de mensagens desta revisão atingido.");await m.Save(s,item,c);return new{item.Id,item.Revision,item.State,messages=item.Messages.Where(x=>Access.Has(actor,"finance.read")||!x.Internal).ToArray()};
            }
            Rules.Require(action=="resolve","action_unknown","Operação desconhecida.",404);item.State="Closed";item.Resolution=Rules.Text(Fields.Text(d,"resolution"),5,1000,"Conclusão");await m.Save(s,item,c);
            var evidence=await m.Get<Evidence>(s,actor.HouseId,item.EvidenceId,c);evidence.ReviewState=evidence.ReceiptId==null?"AwaitingVerification":"CreditConfirmed";await m.Save(s,evidence,c);
            await m.Notify(s,actor.HouseId,item.MemberId,"review-closed:"+item.Id,"Revisão encerrada","Consulte a conclusão no aplicativo. A situação financeira é registrada separadamente.","/revisoes","Finance",c);return item;
        },ct);
    }
    public static string Detect(byte[] b)
    {
        if(b.Length>=8 && b.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))return "image/png";
        if(b.Length>=3 && b[0]==255 && b[1]==216 && b[2]==255)return "image/jpeg";
        if(b.Length>=5 && System.Text.Encoding.ASCII.GetString(b,0,5)=="%PDF-")return "application/pdf";
        return "";
    }
}
