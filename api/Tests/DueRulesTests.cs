using Terreiro.Domain;
using Xunit;
namespace Terreiro.Tests;

public class DueRulesTests
{
    private static Due Unpaid()=>new(){HouseId="h",MemberId="m",AmountCents=5000};
    [Fact] public void FullExemptionComputesOnlyTheCurrentAdjustedValue()
    {
        var due=Unpaid();due.AdjustmentCents=-1000;
        Assert.Equal(-4000L,DueRules.FullExemptionDelta(due));Assert.Equal(0L,due.PaidCents);
    }
    [Fact] public void ExemptDisplayDoesNotMutateAccounting()
    {
        var due=Unpaid();due.Exempt=true;due.AdjustmentCents=-5000;
        Assert.Equal("Pago · Isento",DueRules.DisplayState(due));Assert.Equal("Exempt",due.State);
        Assert.Equal(0L,due.PaidCents);Assert.Equal(0L,due.BalanceCents);
    }
    [Fact] public void CannotExemptAnAlreadyPaidDue(){var due=Unpaid();due.PaidCents=5000;Assert.Throws<RuleException>(()=>DueRules.FullExemptionDelta(due));}
    [Fact] public void CannotExemptAPartialPayment(){var due=Unpaid();due.PaidCents=1000;Assert.Throws<RuleException>(()=>DueRules.FullExemptionDelta(due));}
    [Fact] public void CannotExemptTwice(){var due=Unpaid();due.Exempt=true;due.AdjustmentCents=-5000;Assert.Throws<RuleException>(()=>DueRules.FullExemptionDelta(due));}
    [Fact] public void CannotExemptCancelledDue(){var due=Unpaid();due.Cancelled=true;Assert.Throws<RuleException>(()=>DueRules.FullExemptionDelta(due));}
    [Fact] public void ZeroBalanceIsNotANewExemption(){var due=Unpaid();due.AdjustmentCents=-5000;Assert.Throws<RuleException>(()=>DueRules.FullExemptionDelta(due));}
    [Fact] public void ExemptionAndCancellationAreExclusive()=>Assert.Throws<RuleException>(()=>DueRules.ValidateAdjustment(Unpaid(),-5000,true,true));
    [Fact] public void ExemptionMustEliminateTheAdjustedValue()=>Assert.Throws<RuleException>(()=>DueRules.ValidateAdjustment(Unpaid(),-1000,true,false));
    [Fact] public void AdjustmentCannotErasePayment(){var due=Unpaid();due.PaidCents=1000;Assert.Throws<RuleException>(()=>DueRules.ValidateAdjustment(due,-5000,true,false));}
    [Fact] public void FullExemptionAdjustmentIsAllowed()=>DueRules.ValidateAdjustment(Unpaid(),-5000,true,false);
    [Fact] public void LegacyBadExemptionIsMarkedForReview(){var due=Unpaid();due.Exempt=true;Assert.Equal("Isenção a revisar",DueRules.DisplayState(due));}
    [Fact] public void OwnerReadsTheirDue()=>Assert.True(DueRules.CanRead(new User{Id="m",HouseId="h"},Unpaid()));
    [Fact] public void AnotherMemberCannotReadDue()=>Assert.False(DueRules.CanRead(new User{Id="other",HouseId="h"},Unpaid()));
    [Fact] public void FinanceCanReadWithinHouse()=>Assert.True(DueRules.CanRead(new User{Id="staff",HouseId="h",Permissions=["finance.read"]},Unpaid()));
    [Fact] public void FinanceCannotReadAnotherHouse()=>Assert.False(DueRules.CanRead(new User{Id="staff",HouseId="other",Permissions=["finance.read"]},Unpaid()));
    [Fact] public void AllHistoryIsAValidFilter()=>DueRules.ValidateFilters(null,null,"all","");
    [Fact] public void AllYearsInOneMonthIsValid()=>DueRules.ValidateFilters(null,9,"exempt","");
    [Theory][InlineData(0)][InlineData(13)] public void InvalidMonthIsRejected(int month)=>Assert.Throws<RuleException>(()=>DueRules.ValidateFilters(2026,month,"all",""));
    [Theory][InlineData(1999)][InlineData(2101)] public void InvalidYearIsRejected(int year)=>Assert.Throws<RuleException>(()=>DueRules.ValidateFilters(year,null,"all",""));
    [Fact] public void UnknownStateIsRejected()=>Assert.Throws<RuleException>(()=>DueRules.ValidateFilters(2026,9,"invented",""));
    [Fact] public void LongQueryIsRejected()=>Assert.Throws<RuleException>(()=>DueRules.ValidateFilters(2026,9,"all",new string('a',101)));
}
