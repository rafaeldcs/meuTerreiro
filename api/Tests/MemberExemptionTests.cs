using Terreiro.Domain;
using Xunit;
namespace Terreiro.Tests;

public class MemberExemptionTests
{
    private static MemberExemption Policy()=>new(){Id="policy",HouseId="h",MemberId="m",State="Approved",Reason="Função na casa",EffectiveFrom="2026-09",ApprovalId="a",ApprovedBy="reviewer",ApprovedAt=new DateTime(2026,9,22,12,0,0,DateTimeKind.Utc)};
    private static Due Due(string month="2026-09")=>new(){HouseId="h",MemberId="m",Competence=month,AmountCents=5000};
    [Theory][InlineData("2026-09")][InlineData("2026-10")][InlineData("2027-02")]
    public void OneApprovedPolicyCoversMultipleMonths(string month)=>Assert.True(MemberExemptionRules.Applies(Policy(),"h","m",month));
    [Fact] public void PriorMonthIsNotCovered()=>Assert.False(MemberExemptionRules.Applies(Policy(),"h","m","2026-08"));
    [Theory][InlineData("other","m")][InlineData("h","other")]
    public void DoesNotAffectAnotherMemberOrHouse(string h,string m)=>Assert.False(MemberExemptionRules.Applies(Policy(),h,m,"2026-09"));
    [Theory][InlineData("Pending")][InlineData("Rejected")][InlineData("Withdrawn")]
    public void OnlyApprovedPoliciesApply(string state){var p=Policy();p.State=state;Assert.False(MemberExemptionRules.Applies(p,"h","m","2026-09"));}
    [Fact] public void EndDateIsInclusive(){var p=Policy();p.EffectiveTo="2026-10";Assert.True(MemberExemptionRules.Applies(p,"h","m","2026-10"));Assert.False(MemberExemptionRules.Applies(p,"h","m","2026-11"));}
    [Fact] public void StopIsExclusive(){var p=Policy();p.EndedFrom="2026-11";Assert.True(MemberExemptionRules.Applies(p,"h","m","2026-10"));Assert.False(MemberExemptionRules.Applies(p,"h","m","2026-11"));}
    [Fact] public void ApplyLeavesCashAndPaidUnchanged(){var d=Due();MemberExemptionRules.Apply(d,Policy());Assert.Equal(0L,d.PaidCents);Assert.Equal(0L,d.BalanceCents);Assert.Equal(-5000L,d.AdjustmentCents);Assert.Equal("Pago · Isento",DueRules.DisplayState(d));Assert.Equal("policy",d.MemberExemptionId);}
    [Fact] public void ReasonIsPreservedOnDue(){var p=Policy();var d=Due();MemberExemptionRules.Apply(d,p);p.Reason="Outro motivo";Assert.Equal("Função na casa",d.ExemptionReason);}
    [Fact] public void CannotApplyPendingPolicy(){var p=Policy();p.State="Pending";Assert.Throws<RuleException>(()=>MemberExemptionRules.Apply(Due(),p));}
    [Fact] public void CannotHideActualPayments(){var d=Due();d.PaidCents=1000;Assert.Throws<RuleException>(()=>MemberExemptionRules.Apply(d,Policy()));Assert.Equal(1000L,d.PaidCents);}
    [Fact] public void CannotApplyTwice(){var d=Due();MemberExemptionRules.Apply(d,Policy());Assert.Throws<RuleException>(()=>MemberExemptionRules.Apply(d,Policy()));}
    [Fact] public void CannotApplyCancelled(){var d=Due();d.Cancelled=true;Assert.Throws<RuleException>(()=>MemberExemptionRules.Apply(d,Policy()));}
    [Fact] public void CanSelectNullWithNoPolicy()=>Assert.Null(MemberExemptionRules.Select([],"h","m","2026-09"));
    [Fact] public void OverlapFailsRatherThanChoosingSilently()=>Assert.Throws<RuleException>(()=>MemberExemptionRules.Select([Policy(),Policy()],"h","m","2026-09"));
    [Fact] public void OverlapIncludesPending(){var a=Policy();var b=Policy();b.State="Pending";Assert.True(MemberExemptionRules.Overlaps(a,b));}
    [Fact] public void RejectedDoesNotReserveDate(){var a=Policy();var b=Policy();b.State="Rejected";Assert.False(MemberExemptionRules.Overlaps(a,b));}
    [Fact] public void AdjacentPoliciesDoNotOverlap(){var a=Policy();a.EndedFrom="2026-11";var b=Policy();b.EffectiveFrom="2026-11";Assert.False(MemberExemptionRules.Overlaps(a,b));}
    [Fact] public void CancellingFutureStartCreatesEmptyRange(){var a=Policy();a.EndedFrom=a.EffectiveFrom;Assert.False(MemberExemptionRules.Overlaps(a,Policy()));}
    [Fact] public void PreviousDiscountIsRestoredWhenFutureBenefitEnds(){var d=Due("2026-11");d.AdjustmentCents=-500;var p=Policy();MemberExemptionRules.Apply(d,p);Assert.Equal(-4500L,d.MemberExemptionAdjustmentCents);MemberExemptionRules.RestoreFuture(d,p,"2026-11");Assert.Equal(-500L,d.AdjustmentCents);Assert.Equal(4500L,d.BalanceCents);Assert.False(d.Exempt);Assert.Null(d.MemberExemptionId);}
    [Fact] public void StopDoesNotRewritePastMonths(){var d=Due("2026-09");var p=Policy();MemberExemptionRules.Apply(d,p);Assert.Throws<RuleException>(()=>MemberExemptionRules.RestoreFuture(d,p,"2026-11"));Assert.True(d.Exempt);}
    [Fact] public void StopDoesNotRewriteIndividualExemptions(){var d=Due("2026-11");var p=Policy();MemberExemptionRules.Apply(d,p);d.MemberExemptionId=null;Assert.Throws<RuleException>(()=>MemberExemptionRules.RestoreFuture(d,p,"2026-11"));}
    [Fact] public void RetroactiveStartIsRejected()=>Assert.Throws<RuleException>(()=>MemberExemptionRules.ValidateStart("2026-08",null,"2026-09"));
    [Fact] public void CurrentMonthStartIsAllowed()=>MemberExemptionRules.ValidateStart("2026-09",null,"2026-09");
    [Fact] public void InvalidEndIsRejected()=>Assert.Throws<RuleException>(()=>MemberExemptionRules.ValidateStart("2026-09","2026-08","2026-09"));
    [Fact] public void CannotEndInCurrentMonth()=>Assert.Throws<RuleException>(()=>MemberExemptionRules.ValidateEnd(Policy(),"2026-09","2026-09"));
    [Fact] public void CanEndNextMonth()=>MemberExemptionRules.ValidateEnd(Policy(),"2026-10","2026-09");
    [Fact] public void DuplicateStopIsRejected(){var p=Policy();p.EndedFrom="2026-10";Assert.Throws<RuleException>(()=>MemberExemptionRules.ValidateEnd(p,"2026-11","2026-09"));}
    [Fact] public void PaymentExceptionIsExplicit(){var d=Due();d.PaidCents=500;Assert.Equal("HasPayment",MemberExemptionRules.SkipReason(d,false));}
    [Fact] public void ClosedPeriodExceptionIsExplicit()=>Assert.Equal("PeriodClosed",MemberExemptionRules.SkipReason(Due(),true));
    [Fact] public void EligibleUnpaidDueHasNoException()=>Assert.Null(MemberExemptionRules.SkipReason(Due(),false));
    [Fact] public void NewYearMonthIsCorrect()=>Assert.Equal("2027-01",MemberExemptionRules.NextMonth("2026-12"));
}
