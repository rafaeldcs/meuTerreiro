using System.Text.Json;
using MongoDB.Driver;
using Terreiro.Data;
using Terreiro.Domain;
namespace Terreiro.Service;
public sealed class AdvancedCleaningService(ModuleStore m,FinanceService finance)
{
    public async Task<object> Command(User actor,string action,Command input,CancellationToken ct)
    {
        return await m.Execute<object>(actor,"cleaning."+action,input.OperationId,input,async(s,c)=>
        {
            var d=input.Data;var h=actor.HouseId;
            if(action=="series.create")
            {
                Rules.Require(Roles.Coordinates(actor),"forbidden","Acesso da coordenação.",403);
                var mode=Fields.Text(d,"mode","Team");var start=Time(d,"nextStartsAt");var until=Time(d,"until");var duration=(int)Fields.Number(d,"durationMinutes",120);var weeks=(int)Fields.Number(d,"intervalWeeks",1);var target=(int)Fields.Number(d,"target",4);
                Rules.Plan(mode,target,start,start.AddMinutes(duration),DateTime.UtcNow);Rules.Require(until>start&&until<=start.AddYears(1)&&weeks is >=1 and <=12,"series_period","Defina uma série de até um ano, a cada 1 a 12 semanas.");
                var ids=Fields.Ids(d,"memberIds");await finance.ValidateMembers(s,h,ids,c);Rules.Require(mode!="Team"||ids.Length>0,"members_required","Selecione a equipe.");
                var tasks=Fields.Rows(d,"tasks").Select(x=>Rules.Text(x.GetString()??"",2,200,"Tarefa")).ToList();Rules.Require(tasks.Count is >0 and <=30,"tasks_required","Informe de 1 a 30 tarefas.");
                var series=new CleaningSeries{HouseId=h,Title=Rules.Text(Fields.Text(d,"title"),3,120,"Título"),Area=Rules.Text(Fields.Text(d,"area"),2,120,"Área"),CoordinatorId=actor.Id,Mode=mode,Target=mode=="Team"?target:null,IntervalWeeks=weeks,NextStartsAt=start,DurationMinutes=duration,Until=until,MemberIds=ids.ToList(),Tasks=tasks};await m.Insert(s,series,c);return series;
            }
            if(action=="series.stop")
            {
                var series=await m.Get<CleaningSeries>(s,h,Fields.Required(d,"seriesId"),c);Rules.Require(Roles.Coordinates(actor)&&(actor.Role==Roles.Admin||actor.Id==series.CoordinatorId),"forbidden","Coordenação da série necessária.",403);BusinessRules.Revision(series.Revision,input.Revision);series.Active=false;await m.Save(s,series,c);return series;
            }
            if(action is "swap.accept" or "swap.review")
            {
                var swap=await m.Get<CleaningSwap>(s,h,Fields.Required(d,"swapId"),c);BusinessRules.Revision(swap.Revision,input.Revision);var cleaning=await Load(s,h,swap.CleaningId,c);Rules.Require(cleaning.Status=="Published"&&cleaning.PublicationVersion==swap.PublicationVersion&&cleaning.StartsAt>DateTime.UtcNow,"schedule_changed","A escala mudou ou já começou; a troca precisa ser revista.",409);
                if(action=="swap.accept")
                {
                    Rules.Require(actor.Id==swap.SubstituteId&&swap.State=="AwaitingAcceptance","swap_actor","Apenas o substituto pode responder esta solicitação.",403);swap.State=Fields.Bool(d,"accept")?"AwaitingApproval":"Declined";
                }
                else
                {
                    Manage(actor,cleaning);Rules.Require(swap.State=="AwaitingApproval","swap_state","A troca ainda depende do aceite do substituto.",409);
                    if(Fields.Bool(d,"approve"))
                    {
                        var substitute=await m.Root.Users.Find(s,x=>x.HouseId==h&&x.Id==swap.SubstituteId&&x.Active&&x.IsMember).FirstOrDefaultAsync(c);Rules.Require(substitute!=null,"substitute_invalid","Substituto não está ativo.",409);
                        Rules.Require(!cleaning.Assignments.Any(x=>x.UserId==substitute.Id),"duplicate_assignment","O substituto já está nesta escala.",409);
                        var original=cleaning.Assignments.SingleOrDefault(x=>x.UserId==swap.OriginalId);Rules.Require(original!=null&&!original.Dispensed,"original_missing","Designação original não está ativa.",409);
                        await LockMembers(s,h,[substitute.Id],c);await NoConflict(s,h,cleaning.Id,cleaning.StartsAt,cleaning.EndsAt,[substitute.Id],c);
                        cleaning.PublicationHistory.Add(JsonSerializer.Serialize(new{cleaning.PublicationVersion,cleaning.Revision,cleaning.Assignments}));cleaning.Assignments.Remove(original);cleaning.Assignments.Add(new Assignment{UserId=substitute.Id,Name=substitute.Name,Response="Confirmed",RespondedAt=DateTime.UtcNow});
                        await Save(s,cleaning,c);swap.State="Approved";swap.ApprovedBy=actor.Id;
                    }
                    else swap.State="Rejected";
                }
                await m.Save(s,swap,c);foreach(var user in new[]{swap.OriginalId,swap.SubstituteId,cleaning.CoordinatorId}.Distinct())await Notify(s,actor,cleaning,user,"swap:"+swap.Id+":"+swap.Revision,"Atualização de substituição",c);return swap;
            }
            var x=await Load(s,h,Fields.Required(d,"cleaningId"),c);Rules.Require(x.Status=="Published","cleaning_closed","Atividade encerrada ou cancelada.",409);BusinessRules.Revision(x.Revision,input.Revision);
            if(action=="swap.request")
            {
                var original=x.Assignments.SingleOrDefault(a=>a.UserId==actor.Id&&!a.Dispensed);Rules.Require(original!=null,"not_assigned","Você não está designado para esta limpeza.",403);Rules.Require(x.StartsAt>DateTime.UtcNow,"already_started","Solicite a troca antes do início da limpeza.",409);
                var substituteId=Fields.Required(d,"substituteId");Rules.Require(substituteId!=actor.Id&&!x.Assignments.Any(a=>a.UserId==substituteId),"substitute_invalid","Escolha outro membro que não esteja nesta escala.");await finance.ValidateMembers(s,h,[substituteId],c);
                Rules.Require(!await m.Set<CleaningSwap>().Find(s,a=>a.HouseId==h&&a.CleaningId==x.Id&&a.OriginalId==actor.Id&&(a.State=="AwaitingAcceptance"||a.State=="AwaitingApproval")).AnyAsync(c),"swap_pending","Já existe uma troca pendente.",409);
                var swap=new CleaningSwap{HouseId=h,CleaningId=x.Id,PublicationVersion=x.PublicationVersion,OriginalId=actor.Id,SubstituteId=substituteId};original.Response="Unavailable";await Save(s,x,c);await m.Insert(s,swap,c);await Notify(s,actor,x,substituteId,"swap:"+swap.Id,"Pedido de substituição na limpeza",c);await Notify(s,actor,x,x.CoordinatorId,"swap-coordinator:"+swap.Id,"Troca pendente de aceite",c);return swap;
            }
            if(action=="task.report")
            {
                var task=x.Tasks.SingleOrDefault(t=>t.Id==Fields.Text(d,"taskId"));Rules.Require(task!=null&&task.AssignedTo==actor.Id,"task_owner","Esta tarefa não está atribuída a você.",403);Rules.Require(DateTime.UtcNow>=x.StartsAt,"not_started","Aguarde o início da atividade.",409);task.ReportedDone=true;await Save(s,x,c);return new{x.Id,x.Revision};
            }
            Manage(actor,x);
            switch(action)
            {
                case "reschedule":
                {
                    var start=Time(d,"startsAt");var end=Time(d,"endsAt");Rules.Plan(x.Mode,x.Target,start,end,DateTime.UtcNow);
                    var selected=x.Assignments.Where(a=>!a.Dispensed).Select(a=>a.UserId).ToArray();await LockMembers(s,h,selected,c);await NoConflict(s,h,x.Id,start,end,selected,c);
                    x.PublicationHistory.Add(JsonSerializer.Serialize(new{x.PublicationVersion,x.StartsAt,x.EndsAt,x.Assignments}));x.StartsAt=start;x.EndsAt=end;x.NeedsScheduleReview=false;x.PublicationVersion++;foreach(var a in x.Assignments){a.Response="Pending";a.RespondedAt=null;}
                    await m.Root.Notifications.UpdateManyAsync(s,n=>n.HouseId==h&&n.CleaningId==x.Id&&n.SuppressWhenCancelled,Builders<Notification>.Update.Set(n=>n.PushState,"Suppressed"),cancellationToken:c);break;
                }
                case "dispense":
                {
                    var a=x.Assignments.SingleOrDefault(a=>a.UserId==Fields.Text(d,"memberId"));Rules.Require(a!=null,"member_missing","Participante não encontrado.",404);Rules.Text(Fields.Text(d,"reason"),3,200,"Motivo administrativo (sem dados íntimos)");a.Dispensed=Fields.Bool(d,"dispensed",true);break;
                }
                case "task.assign":
                {
                    var task=x.Tasks.SingleOrDefault(t=>t.Id==Fields.Text(d,"taskId"));Rules.Require(task!=null,"task_missing","Tarefa não encontrada.",404);var member=Fields.Required(d,"memberId");Rules.Require(x.Assignments.Any(a=>a.UserId==member&&!a.Dispensed),"member_missing","Selecione um participante ativo da escala.");task.AssignedTo=member;break;
                }
                case "link.event":
                {
                    var id=Fields.Optional(d,"eventId");if(id!=null){var e=await m.Get<HouseEvent>(s,h,id,c);Rules.Require(e.State=="Planned","event_closed","Evento encerrado.",409);}x.EventId=id;break;
                }
                case "close.pending":
                {
                    Rules.Require(DateTime.UtcNow>=x.StartsAt,"not_started","A atividade ainda não começou.",409);x.Status="ClosedWithPending";x.ClosingReason=Rules.Text(Fields.Text(d,"reason"),8,500,"Pendências e responsável pelo tratamento");break;
                }
                default:throw new RuleException("action_unknown","Operação de limpeza desconhecida.",404);
            }
            await Save(s,x,c);foreach(var a in x.Assignments)await Notify(s,actor,x,a.UserId,"changed:"+x.Id+":"+x.Revision,"Escala de limpeza atualizada",c);return new{x.Id,x.Revision};
        },ct);
    }
    private async Task<Cleaning> Load(IClientSessionHandle s,string house,string id,CancellationToken ct){var x=await m.Root.Cleanings.Find(s,a=>a.Id==id&&a.HouseId==house).FirstOrDefaultAsync(ct);Rules.Require(x!=null,"not_found","Limpeza não encontrada.",404);return x;}
    private static void Manage(User user,Cleaning c)=>Rules.Require(Roles.Coordinates(user)&&(user.Role==Roles.Admin||c.CoordinatorId==user.Id),"forbidden","A operação exige a coordenação responsável.",403);
    private async Task Save(IClientSessionHandle s,Cleaning x,CancellationToken ct){var version=x.Revision;x.Revision++;var result=await m.Root.Cleanings.ReplaceOneAsync(s,a=>a.Id==x.Id&&a.HouseId==x.HouseId&&a.Revision==version,x,cancellationToken:ct);Rules.Require(result.ModifiedCount==1,"conflict","A escala foi alterada.",409);}
    private Task LockMembers(IClientSessionHandle s,string h,string[] ids,CancellationToken ct)=>m.Root.Users.UpdateManyAsync(s,x=>x.HouseId==h&&ids.Contains(x.Id),Builders<User>.Update.Inc(x=>x.ScheduleRevision,1),cancellationToken:ct);
    private async Task NoConflict(IClientSessionHandle s,string house,string id,DateTime start,DateTime end,string[] members,CancellationToken ct){var conflict=await m.Root.Cleanings.Find(s,x=>x.HouseId==house&&x.Id!=id&&x.Status=="Published"&&x.StartsAt<end&&x.EndsAt>start&&x.Assignments.Any(a=>members.Contains(a.UserId)&&!a.Dispensed)).AnyAsync(ct);Rules.Require(!conflict,"schedule_conflict","Há um conflito de horário com outra limpeza.",409);}
    private Task Notify(IClientSessionHandle s,User actor,Cleaning x,string user,string key,string title,CancellationToken ct)=>m.Notify(s,actor.HouseId,user,key,title,"Confira a situação atual e as respostas pendentes no aplicativo.","/limpezas/"+x.Id,"Cleaning",ct);
    private static DateTime Time(JsonElement d,string key){Rules.Require(DateTimeOffset.TryParse(Fields.Text(d,key),out var value),"time_invalid","Horário inválido.");return value.UtcDateTime;}
}
