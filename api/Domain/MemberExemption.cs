using System.Globalization;
namespace Terreiro.Domain;

// A recurring administrative benefit belongs to a registered member, never to an access role.
// Approved rows remain historical: stopping sets an exclusive upper bound, not deletion.
public sealed class MemberExemption : Entity
{
    public string MemberId { get; set; } = "";
    public string Reason { get; set; } = "";
    public string EffectiveFrom { get; set; } = "";
    public string? EffectiveTo { get; set; } // Inclusive, optional.
    public string State { get; set; } = "Pending";
    public string RequestedBy { get; set; } = "";
    public string ApprovalId { get; set; } = "";
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? EndedFrom { get; set; } // Exclusive, approved stopping month.
    public string? EndReason { get; set; }
    public string? EndApprovalId { get; set; }
    public string? EndedBy { get; set; }
    public DateTime? EndedAt { get; set; }
    public int AppliedExistingCount { get; set; }
    public int RestoredFutureCount { get; set; }
    public List<MemberExemptionException> Exceptions { get; set; } = [];
}
public sealed class MemberExemptionException
{
    public string DueId { get; set; } = "";
    public string Competence { get; set; } = "";
    public string Code { get; set; } = "";
}
public static class MemberExemptionRules
{
    public static string NextMonth(string month) => DateOnly.ParseExact(BusinessRules.Month(month)+"-01","yyyy-MM-dd",CultureInfo.InvariantCulture).AddMonths(1).ToString("yyyy-MM",CultureInfo.InvariantCulture);
    public static bool Within(MemberExemption rule, string month) =>
        string.CompareOrdinal(month,rule.EffectiveFrom)>=0 &&
        (rule.EffectiveTo==null || string.CompareOrdinal(month,rule.EffectiveTo)<=0) &&
        (rule.EndedFrom==null || string.CompareOrdinal(month,rule.EndedFrom)<0);
    public static bool Applies(MemberExemption rule, string houseId, string memberId, string month) =>
        rule.HouseId==houseId && rule.MemberId==memberId && rule.State=="Approved" && Within(rule,month);
    public static string LastMonth(MemberExemption rule)
    {
        var last=rule.EffectiveTo??"2100-12";
        if(rule.EndedFrom!=null)
        {
            var stopped=DateOnly.ParseExact(rule.EndedFrom+"-01","yyyy-MM-dd",CultureInfo.InvariantCulture).AddMonths(-1).ToString("yyyy-MM",CultureInfo.InvariantCulture);
            if(string.CompareOrdinal(stopped,last)<0) last=stopped;
        }
        return last;
    }
    public static bool Overlaps(MemberExemption a,MemberExemption b) =>
        a.HouseId==b.HouseId && a.MemberId==b.MemberId &&
        (a.State is "Pending" or "Approved") && (b.State is "Pending" or "Approved") &&
        string.CompareOrdinal(a.EffectiveFrom,LastMonth(a))<=0 &&
        string.CompareOrdinal(b.EffectiveFrom,LastMonth(b))<=0 &&
        string.CompareOrdinal(a.EffectiveFrom,LastMonth(b))<=0 &&
        string.CompareOrdinal(b.EffectiveFrom,LastMonth(a))<=0;
    public static void ValidateStart(string from,string? until,string current)
    {
        BusinessRules.Month(from); BusinessRules.Month(current);
        Rules.Require(string.CompareOrdinal(from,current)>=0,"exemption_retroactive","A isenção do cadastro começa no mês corrente ou posterior. Mensalidades anteriores exigem ajuste individual aprovado.");
        if(until!=null) Rules.Require(string.CompareOrdinal(BusinessRules.Month(until),from)>=0,"dates_invalid","O fim da isenção deve ser igual ou posterior ao início.");
    }
    public static void ValidateEnd(MemberExemption rule,string from,string current)
    {
        BusinessRules.Month(from);BusinessRules.Month(current);
        Rules.Require(rule.State=="Approved" && rule.EndedFrom==null,"exemption_state","Isenção não está aprovada ou já possui encerramento.",409);
        Rules.Require(string.CompareOrdinal(from,NextMonth(current))>=0,"exemption_end_retroactive","A cobrança volta a partir do próximo mês ou de um mês posterior, preservando o mês corrente e o histórico.");
        Rules.Require(string.CompareOrdinal(from,rule.EffectiveFrom)>=0 && string.CompareOrdinal(from,LastMonth(rule))<=0,"exemption_end_range","Escolha uma competência dentro da vigência da isenção. Para benefício com fim definido, a cobrança já volta no mês seguinte ao término.");
    }
    public static MemberExemption? Select(IEnumerable<MemberExemption> rules,string house,string member,string month)
    {
        BusinessRules.Month(month);
        var eligible=rules.Where(x=>Applies(x,house,member,month)).Take(2).ToArray();
        Rules.Require(eligible.Length<=1,"exemption_overlap","Há isenções sobrepostas no cadastro. Revise antes de gerar mensalidades.",409);
        return eligible.SingleOrDefault();
    }
    public static string? SkipReason(Due due,bool periodClosed)
    {
        if(due.Cancelled)return "Cancelled";
        if(due.Exempt)return "AlreadyExempt";
        if(due.PaidCents>0)return "HasPayment";
        if(due.BalanceCents<=0)return "AlreadySettled";
        return periodClosed?"PeriodClosed":null;
    }
    public static void Apply(Due due,MemberExemption rule)
    {
        Rules.Require(Applies(rule,due.HouseId,due.MemberId,due.Competence),"exemption_scope","A isenção não pertence a este médium, casa ou competência.",409);
        Rules.Require(rule.ApprovedBy!=null && rule.ApprovedAt!=null && rule.ApprovalId.Length>0,"exemption_unapproved","A isenção deve possuir aprovação identificada.",409);
        var delta=DueRules.FullExemptionDelta(due);
        due.AdjustmentCents=checked(due.AdjustmentCents+delta);
        due.MemberExemptionAdjustmentCents=delta;due.MemberExemptionId=rule.Id;
        due.Exempt=true;due.ExemptionReason=rule.Reason;due.ExemptionApprovalId=rule.ApprovalId;
        due.ExemptionApprovedBy=rule.ApprovedBy;due.ExemptionApprovedAt=rule.ApprovedAt;
        // PaidCents and receipts remain unchanged. No money was received.
    }
    public static void RestoreFuture(Due due,MemberExemption rule,string endedFrom)
    {
        Rules.Require(due.HouseId==rule.HouseId&&due.MemberId==rule.MemberId&&due.MemberExemptionId==rule.Id&&string.CompareOrdinal(due.Competence,endedFrom)>=0,
            "exemption_scope","A mensalidade não pertence ao encerramento desta isenção.",409);
        Rules.Require(due.Exempt&&!due.Cancelled&&due.PaidCents==0&&due.BalanceCents==0&&due.MemberExemptionAdjustmentCents<0,
            "exemption_due_changed","Uma mensalidade futura mudou. Revise-a antes de encerrar a isenção.",409);
        due.AdjustmentCents=checked(due.AdjustmentCents-due.MemberExemptionAdjustmentCents);
        due.MemberExemptionAdjustmentCents=0;due.MemberExemptionId=null;due.Exempt=false;
        due.ExemptionReason=null;due.ExemptionApprovalId=null;due.ExemptionApprovedBy=null;due.ExemptionApprovedAt=null;
    }
}
