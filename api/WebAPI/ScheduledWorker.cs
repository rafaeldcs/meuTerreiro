using System.Text.Json;
using MongoDB.Driver;
using Terreiro.Data;
using Terreiro.Domain;
using Terreiro.Service;
namespace Terreiro.WebAPI;

// Durable, idempotent internal jobs. This is part of the application, not a messaging integration.
public sealed class ScheduledWorker(ModuleStore m,FinanceService finance,AuthService auth,ILogger<ScheduledWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            try{await Tick(stoppingToken);}
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogWarning("Administrative scheduler failed ({Type})",ex.GetType().Name);}
            try{await Task.Delay(TimeSpan.FromMinutes(1),stoppingToken);}catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
        }
    }
    private static string Operation(string value)=>new Guid(Convert.FromHexString(Rules.Hash(value)[..32])).ToString();
    private async Task Tick(CancellationToken ct)
    {
        var house=auth.HouseId;var today=BusinessRules.Today();var month=today[..7];
        var rules=await m.Set<ContributionRule>().Find(x=>x.HouseId==house&&x.Active).Limit(500).ToListAsync(ct);
        foreach(var rule in rules.Where(x=>string.CompareOrdinal(x.EffectiveFrom,month)<=0&&(x.EffectiveTo==null||string.CompareOrdinal(x.EffectiveTo,month)>=0)))
        {
            var actor=await m.Root.Users.Find(x=>x.Id==rule.CreatedBy&&x.HouseId==house&&x.Active).FirstOrDefaultAsync(ct);
            if(actor==null||!Access.Has(actor,"finance.write"))continue;
            await m.Execute(actor,"scheduler.dues",Operation(rule.Id+":"+rule.Revision+":"+month),new{rule.Id,rule.Revision,month},(s,c)=>finance.Generate(s,actor,month,c),ct);
        }
        var limit=DateTime.UtcNow.AddDays(7);var series=await m.Set<CleaningSeries>().Find(x=>x.HouseId==house&&x.Active&&x.NextStartsAt<=limit).Limit(100).ToListAsync(ct);
        foreach(var seriesItem in series)
        {
            var actor=await m.Root.Users.Find(x=>x.Id==seriesItem.CoordinatorId&&x.HouseId==house&&x.Active).FirstOrDefaultAsync(ct);if(actor==null||!Roles.Coordinates(actor))continue;
            try{await GenerateCleaning(actor,seriesItem,ct);}
            catch(RuleException ex)
            {
                await m.Root.Transaction(async(s,c)=>{await m.Notify(s,house,actor.Id,"series-attention:"+seriesItem.Id+":"+seriesItem.NextStartsAt.ToString("O"),"Revisar geração de limpeza",ex.Message,"/recorrencias","Cleaning",c);return true;},ct);
            }
        }
        // One daily batch per house. A stored receipt is not needed to decide administrative reminders.
        var administrative=await m.Root.Users.Find(x=>x.HouseId==house&&x.Active).Limit(500).ToListAsync(ct);
        foreach(var user in administrative)
        {
            if(Access.Has(user,"finance.read"))
            {
                var expenses=await m.Set<Expense>().Find(x=>x.HouseId==house&&x.State=="Approved").ToListAsync(ct);var approaching=expenses.Count(x=>x.AmountCents>x.PaidCents&&string.CompareOrdinal(x.DueDate,DateOnly.Parse(today).AddDays(3).ToString("yyyy-MM-dd"))<=0);
                if(approaching>0)await Notice(user,"payables:"+today,"Contas a acompanhar",$"Há {approaching} compromissos próximos ou vencidos. Confira a situação atual.","/despesas","Finance",ct);
            }
            if(Access.Has(user,"stock.read"))
            {
                var items=await m.Set<Item>().Find(x=>x.HouseId==house&&x.Active).ToListAsync(ct);var lots=await m.Set<StockLot>().Find(x=>x.HouseId==house).ToListAsync(ct);
                var low=items.Count(x=>lots.Where(y=>y.ItemId==x.Id).Sum(y=>y.AvailableMilli)<x.MinimumMilli);var soon=lots.Count(x=>x.OnHandMilli>0&&x.ExpiresOn!=null&&string.CompareOrdinal(x.ExpiresOn,DateOnly.Parse(today).AddDays(30).ToString("yyyy-MM-dd"))<=0);
                if(low+soon>0)await Notice(user,"stock:"+today,"Revisar materiais",$"Estoque: {low} itens abaixo do mínimo e {soon} lotes com validade próxima ou vencida.","/estoque","Stock",ct);
            }
        }
        var allDues=await m.Set<Due>().Find(x=>x.HouseId==house&&!x.Cancelled&&!x.Exempt).ToListAsync(ct);
        foreach(var due in allDues.Where(x=>x.BalanceCents>0&&string.CompareOrdinal(x.DueDate,DateOnly.Parse(today).AddDays(3).ToString("yyyy-MM-dd"))<=0))
        {
            var user=administrative.FirstOrDefault(x=>x.Id==due.MemberId);if(user==null)continue;
            var since=DateTime.UtcNow.AddDays(-3);var pending=await m.Set<Evidence>().Find(x=>x.HouseId==house&&x.MemberId==user.Id&&x.DueIds.Contains(due.Id)&&x.CreatedAt>=since&&x.ReceiptId==null).AnyAsync(ct);
            if(pending)continue;
            // Weekly after due, once in the three-day pre-due window. Rechecked again by the push dispatcher.
            var late=DateOnly.Parse(today).DayNumber-DateOnly.Parse(due.DueDate).DayNumber;if(late>=0&&late%7!=0)continue;
            await Notice(user,"due-reminder:"+due.Id+":"+(late<0?"before":today),"Mensalidade a acompanhar","Confira sua mensalidade e eventuais pedidos de informação no aplicativo.","/mensalidades","DueReminder",ct,due.Id);
        }
        var now=DateTime.UtcNow;var plans=await m.Root.Cleanings.Find(x=>x.HouseId==house&&x.Status=="Published"&&!x.NeedsScheduleReview&&x.StartsAt>now&&x.StartsAt<=now.AddDays(1)).Limit(100).ToListAsync(ct);
        foreach(var plan in plans)
        foreach(var assignment in plan.Assignments.Where(x=>!x.Dispensed))
        {
            var user=administrative.FirstOrDefault(x=>x.Id==assignment.UserId);if(user==null)continue;
            await m.Root.Transaction(async(s,c)=>
            {
                var current=await m.Root.Cleanings.Find(s,x=>x.Id==plan.Id&&x.HouseId==house&&x.Status=="Published"&&x.PublicationVersion==plan.PublicationVersion).FirstOrDefaultAsync(c);if(current==null)return false;
                var key="cleaning-reminder:"+plan.Id+":"+plan.PublicationVersion;await m.Notify(s,house,user.Id,key,"Lembrete de limpeza","Confira o horário, suas tarefas e sua resposta atual.","/limpezas/"+plan.Id,"Cleaning",c);
                var id=Rules.Hash(house+"|"+key+"|"+user.Id);await m.Root.Notifications.UpdateOneAsync(s,x=>x.Id==id,Builders<Notification>.Update.Set(x=>x.CleaningId,plan.Id).Set(x=>x.PublicationVersion,plan.PublicationVersion).Set(x=>x.SuppressWhenCancelled,true).Set(x=>x.ExpiresAt,plan.StartsAt),cancellationToken:c);return true;
            },ct);
        }
    }
    private Task Notice(User user,string key,string title,string body,string path,string category,CancellationToken ct,string? referenceId=null)=>m.Root.Transaction(async(s,c)=>{await m.Notify(s,user.HouseId,user.Id,key,title,body,path,category,c,referenceId);return true;},ct);
    private Task<object> GenerateCleaning(User actor,CleaningSeries observed,CancellationToken ct)=>m.Execute<object>(actor,"scheduler.cleaning",Operation(observed.Id+":"+observed.NextStartsAt.ToString("O")),new{observed.Id,observed.NextStartsAt},async(s,c)=>
    {
        var rule=await m.Get<CleaningSeries>(s,actor.HouseId,observed.Id,c);Rules.Require(rule.Active&&rule.NextStartsAt==observed.NextStartsAt,"series_changed","Série alterada.",409);
        while(rule.NextStartsAt<=DateTime.UtcNow)rule.NextStartsAt=rule.NextStartsAt.AddDays(rule.IntervalWeeks*7);
        if(rule.NextStartsAt>rule.Until){rule.Active=false;await m.Save(s,rule,c);return new{rule.Id,rule.Active};}
        var members=await m.Root.Users.Find(s,x=>x.HouseId==actor.HouseId&&x.Active&&x.IsMember&&(rule.Mode=="General"||rule.MemberIds.Contains(x.Id))).ToListAsync(c);
        Rules.Require(members.Count>0&&members.Count<=500,"members_invalid","A série precisa de membros ativos elegíveis.",409);
        var ids=members.Select(x=>x.Id).ToArray();var start=rule.NextStartsAt;var end=start.AddMinutes(rule.DurationMinutes);
        await m.Root.Users.UpdateManyAsync(s,x=>x.HouseId==actor.HouseId&&ids.Contains(x.Id),Builders<User>.Update.Inc(x=>x.ScheduleRevision,1),cancellationToken:c);
        Rules.Require(!await m.Root.Cleanings.Find(s,x=>x.HouseId==actor.HouseId&&x.Status=="Published"&&x.StartsAt<end&&x.EndsAt>start&&x.Assignments.Any(a=>ids.Contains(a.UserId)&&!a.Dispensed)).AnyAsync(c),"schedule_conflict","A ocorrência da série conflita com outra escala. Revise a equipe.",409);
        var plan=new Cleaning{Id=Rules.Hash(rule.Id+"|"+start.ToString("O"))[..32],HouseId=actor.HouseId,CreatedBy=actor.Id,CoordinatorId=actor.Id,SeriesId=rule.Id,Title=rule.Title,Area=rule.Area,Mode=rule.Mode,Target=rule.Target,StartsAt=start,EndsAt=end,Assignments=members.Select(x=>new Assignment{UserId=x.Id,Name=x.Name}).ToList(),Tasks=rule.Tasks.Select(x=>new CleaningTask{Title=x}).ToList()};
        await m.Root.Cleanings.InsertOneAsync(s,plan,cancellationToken:c);rule.NextStartsAt=start.AddDays(rule.IntervalWeeks*7);if(rule.NextStartsAt>rule.Until)rule.Active=false;await m.Save(s,rule,c);
        foreach(var user in members)await m.Notify(s,actor.HouseId,user.Id,"series-published:"+plan.Id,"Nova escala de limpeza","Uma ocorrência da escala recorrente foi publicada. Confirme no aplicativo.","/limpezas/"+plan.Id,"Cleaning",c);
        return new{plan.Id,rule.NextStartsAt};
    },ct);
}
