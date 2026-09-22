using System.Text.Json;
using MongoDB.Driver;
using Terreiro.Data;
using Terreiro.Domain;
namespace Terreiro.Service;

public sealed partial class FinanceService
{
    private async Task<User> ExemptionMember(IClientSessionHandle s,User actor,string id,CancellationToken ct)
    {
        var user=await Db.Users.Find(s,x=>x.Id==id&&x.HouseId==actor.HouseId&&x.Active&&x.IsMember).FirstOrDefaultAsync(ct);
        Rules.Require(user!=null,"member_invalid","Selecione um médium ativo e cadastrado nesta casa.",404);return user;
    }
    private async Task CheckExemptionOverlap(IClientSessionHandle s,MemberExemption policy,CancellationToken ct)
    {
        var all=await m.Set<MemberExemption>().Find(s,x=>x.HouseId==policy.HouseId&&x.MemberId==policy.MemberId&&x.Id!=policy.Id).ToListAsync(ct);
        Rules.Require(!all.Any(x=>MemberExemptionRules.Overlaps(policy,x)),"exemption_overlap","Já existe uma isenção aprovada ou pendente para esse médium no período. Consulte ou encerre a vigência anterior.",409);
    }
    private async Task<MemberExemption> RequestMemberExemption(IClientSessionHandle s,User actor,Command input,CancellationToken ct)
    {
        var member=await ExemptionMember(s,actor,Fields.Required(input.Data,"memberId"),ct);
        var from=Fields.Text(input.Data,"effectiveFrom",BusinessRules.Today()[..7]);var until=Fields.Optional(input.Data,"effectiveTo");
        MemberExemptionRules.ValidateStart(from,until,BusinessRules.Today()[..7]);
        var policy=new MemberExemption{HouseId=actor.HouseId,MemberId=member.Id,RequestedBy=actor.Id,
            EffectiveFrom=from,EffectiveTo=until,Reason=Rules.Text(Fields.Text(input.Data,"reason"),5,500,"Motivo da isenção")};
        await CheckExemptionOverlap(s,policy,ct);
        var payload=JsonSerializer.SerializeToElement(new{memberId=member.Id,memberName=member.Name,effectiveFrom=from,effectiveTo=until,recurring=true});
        var approval=await RequestApproval(s,actor,"MemberExemption",policy.Id,policy.Revision,payload,policy.Reason,ct);
        policy.ApprovalId=approval.Id;await m.Insert(s,policy,ct);return policy;
    }
    private async Task<Approval> RequestEndMemberExemption(IClientSessionHandle s,User actor,Command input,CancellationToken ct)
    {
        var policy=await m.Get<MemberExemption>(s,actor.HouseId,Fields.Required(input.Data,"exemptionId"),ct);
        BusinessRules.Revision(policy.Revision,input.Revision);
        var from=BusinessRules.Month(Fields.Text(input.Data,"endedFrom"));
        MemberExemptionRules.ValidateEnd(policy,from,BusinessRules.Today()[..7]);
        var pending=await m.Set<Approval>().Find(s,x=>x.HouseId==actor.HouseId&&x.Type=="EndMemberExemption"&&x.TargetId==policy.Id&&x.State=="Pending").AnyAsync(ct);
        Rules.Require(!pending,"approval_pending","Já existe um encerramento aguardando revisão.",409);
        return await RequestApproval(s,actor,"EndMemberExemption",policy.Id,policy.Revision,
            JsonSerializer.SerializeToElement(new{memberId=policy.MemberId,endedFrom=from}),Rules.Text(Fields.Text(input.Data,"reason"),5,500,"Motivo do encerramento"),ct);
    }
    private async Task<MemberExemption> WithdrawMemberExemption(IClientSessionHandle s,User actor,Command input,CancellationToken ct)
    {
        var policy=await m.Get<MemberExemption>(s,actor.HouseId,Fields.Required(input.Data,"exemptionId"),ct);
        BusinessRules.Revision(policy.Revision,input.Revision);
        Rules.Require(policy.State=="Pending"&&policy.RequestedBy==actor.Id,"exemption_pending","Somente o solicitante pode retirar uma isenção ainda pendente.",403);
        var approval=await m.Get<Approval>(s,actor.HouseId,policy.ApprovalId,ct);
        Rules.Require(approval.State=="Pending","approval_state","Solicitação já revisada.",409);
        approval.State="Withdrawn";approval.ReviewNote=Rules.Text(Fields.Text(input.Data,"reason"),5,500,"Motivo da retirada");
        approval.ReviewedBy=actor.Id;approval.ReviewedAt=DateTime.UtcNow;policy.State="Withdrawn";
        await m.Save(s,approval,ct);await m.Save(s,policy,ct);return policy;
    }
    private async Task ReviewMemberExemption(IClientSessionHandle s,User actor,Approval approval,JsonElement payload,CancellationToken ct)
    {
        var policy=await m.Get<MemberExemption>(s,actor.HouseId,approval.TargetId,ct);
        BusinessRules.Revision(policy.Revision,approval.TargetRevision);
        BusinessRules.Independent(policy.MemberId,actor.Id); // Reviewer cannot grant/alter their own benefit.
        var h=actor.HouseId;
        if(approval.Type=="MemberExemption")
        {
            Rules.Require(policy.State=="Pending"&&policy.ApprovalId==approval.Id,"exemption_state","Cadastro fora da etapa de aprovação.",409);
            // A request that crossed into a later month must be resubmitted, never silently backdated.
            MemberExemptionRules.ValidateStart(policy.EffectiveFrom,policy.EffectiveTo,BusinessRules.Today()[..7]);
            await ExemptionMember(s,actor,policy.MemberId,ct);await CheckExemptionOverlap(s,policy,ct);
            policy.State="Approved";policy.ApprovedBy=actor.Id;policy.ApprovedAt=DateTime.UtcNow;
            var dues=await m.Set<Due>().Find(s,x=>x.HouseId==h&&x.MemberId==policy.MemberId).ToListAsync(ct);
            var closed=await m.Set<Closing>().Find(s,x=>x.HouseId==h&&x.State!="Reopened").ToListAsync(ct);
            var closedPeriods=closed.Select(x=>x.Period).ToHashSet();
            foreach(var due in dues.Where(x=>MemberExemptionRules.Within(policy,x.Competence)))
            {
                var skip=MemberExemptionRules.SkipReason(due,closedPeriods.Contains(due.Competence));
                if(skip!=null)
                {
                    policy.Exceptions.Add(new(){DueId=due.Id,Competence=due.Competence,Code=skip});continue;
                }
                MemberExemptionRules.Apply(due,policy);await m.Save(s,due,ct);policy.AppliedExistingCount++;
            }
        }
        else
        {
            var from=Fields.Required(payload,"endedFrom");MemberExemptionRules.ValidateEnd(policy,from,BusinessRules.Today()[..7]);
            var linked=await m.Set<Due>().Find(s,x=>x.HouseId==h&&x.MemberId==policy.MemberId&&x.MemberExemptionId==policy.Id).ToListAsync(ct);
            foreach(var due in linked.Where(x=>string.CompareOrdinal(x.Competence,from)>=0))
            {
                // A closed future period blocks the entire stop transaction, rather than leaving inconsistent debts.
                await m.OpenPeriod(s,h,due.Competence+"-01",ct);
                MemberExemptionRules.RestoreFuture(due,policy,from);await m.Save(s,due,ct);policy.RestoredFutureCount++;
            }
            policy.EndedFrom=from;policy.EndReason=approval.Reason;policy.EndApprovalId=approval.Id;
            policy.EndedBy=actor.Id;policy.EndedAt=DateTime.UtcNow;
        }
        await m.Save(s,policy,ct);
        await m.Notify(s,h,policy.MemberId,"member-exemption:"+approval.Id,"Cadastro financeiro atualizado","Consulte a situação das suas mensalidades no aplicativo.","/mensalidades","Finance",ct);
        // Financial exceptions are reported to the requester, never broadcast to all members.
        if(policy.Exceptions.Any(x=>x.Code is "HasPayment" or "PeriodClosed"))
            await m.Notify(s,h,policy.RequestedBy,"member-exemption-review:"+approval.Id,"Mensalidades exigem conferência","Há mensalidades preservadas que precisam de revisão individual. Consulte o cadastro do médium.","/membros","Finance",ct);
    }
}
