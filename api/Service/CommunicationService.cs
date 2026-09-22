using MongoDB.Driver;
using Terreiro.Data;
using Terreiro.Domain;
namespace Terreiro.Service;
public sealed class CommunicationService(ModuleStore m)
{
    public async Task<NotificationPreference> Preferences(User actor,CancellationToken ct)
    {
        return await m.Set<NotificationPreference>().Find(x=>x.HouseId==actor.HouseId&&x.UserId==actor.Id).FirstOrDefaultAsync(ct)??new NotificationPreference{HouseId=actor.HouseId,UserId=actor.Id};
    }
    public Task<NotificationPreference> SavePreferences(User actor,Command input,CancellationToken ct)=>m.Execute(actor,"notification.preferences",input.OperationId,input,async(s,c)=>
    {
        var d=input.Data;var start=(int)Fields.Number(d,"quietStartHour",22);var end=(int)Fields.Number(d,"quietEndHour",7);Rules.Require(start is >=0 and <=23&&end is >=0 and <=23,"quiet_hours","Horário silencioso inválido.");
        var muted=Fields.Ids(d,"mutedCategories");Rules.Require(muted.All(x=>x is "Announcement" or "Donation" or "Event"),"categories_invalid","Somente categorias opcionais podem ser silenciadas individualmente.");
        var p=await m.Set<NotificationPreference>().Find(s,x=>x.HouseId==actor.HouseId&&x.UserId==actor.Id).FirstOrDefaultAsync(c);
        var isNew=p==null;p??=new NotificationPreference{HouseId=actor.HouseId,UserId=actor.Id};p.PushEnabled=Fields.Bool(d,"pushEnabled",true);p.QuietStartHour=start;p.QuietEndHour=end;p.MutedCategories=muted.ToList();
        if(isNew)await m.Insert(s,p,c);else await m.Save(s,p,c);return p;
    },ct);
}
