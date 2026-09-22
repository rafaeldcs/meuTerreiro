using System.Text.Json;
using Terreiro.Domain;
using Terreiro.Service;
using Xunit;
namespace Terreiro.Tests;
public class AdministrationTests
{
    [Theory][InlineData(1)][InlineData(10000)][InlineData(BusinessRules.MaxCents)]
    public void MoneyIsExactAndPositive(long amount)=>Assert.Equal(amount,BusinessRules.Money(amount));
    [Theory][InlineData(0)][InlineData(-1)][InlineData(long.MinValue)][InlineData(long.MaxValue)]
    public void InvalidMoneyIsRejected(long amount)=>Assert.Throws<RuleException>(()=>BusinessRules.Money(amount));
    [Fact]public void OptionalZeroIsExplicit()=>Assert.Equal(0L,BusinessRules.Money(0,true));
    [Theory][InlineData("2026-00")][InlineData("2026-13")][InlineData("26-09")][InlineData("1999-01")][InlineData("2101-01")]
    public void InvalidCompetenceIsRejected(string value)=>Assert.Throws<RuleException>(()=>BusinessRules.Month(value));
    [Fact]public void CompetenceIsIndependentFromPaymentDate()=>Assert.Equal("2026-09",BusinessRules.Month("2026-09"));
    [Theory][InlineData("2026-02-29")][InlineData("2026-02-30")][InlineData("21/09/2026")]
    public void InvalidDateIsRejected(string value)=>Assert.Throws<RuleException>(()=>BusinessRules.Date(value));
    [Fact]public void LeapDayIsAccepted()=>Assert.Equal("2028-02-29",BusinessRules.Date("2028-02-29"));
    [Fact]public void AllocationDoesNotExceedReceipt()
    {
        var r=new Receipt{AmountCents=15000};var d=new Due{AmountCents=5000};
        for(var i=0;i<3;i++){BusinessRules.Allocate(r,5000,d);r.AllocatedCents+=5000;}
        Assert.Equal(0L,r.AvailableCents);Assert.Throws<RuleException>(()=>BusinessRules.Allocate(r,1,d));
    }
    [Fact]public void RefundedMoneyIsNotAvailable()=>Assert.Throws<RuleException>(()=>BusinessRules.Allocate(new Receipt{AmountCents=100,RefundedCents=100},1,null));
    [Fact]public void ExemptDueCannotReceiveAllocation()=>Assert.Throws<RuleException>(()=>BusinessRules.Allocate(new Receipt{AmountCents=10000},100,new Due{AmountCents=10000,Exempt=true}));
    [Fact]public void PaidDueCannotReceiveMore()=>Assert.Throws<RuleException>(()=>BusinessRules.Allocate(new Receipt{AmountCents=10000},100,new Due{AmountCents=10000,PaidCents=10000}));
    [Fact]public void PartialDueRetainsBalance(){var x=new Due{AmountCents=10000,PaidCents=2500};Assert.Equal(7500L,x.BalanceCents);Assert.Equal("Partial",x.State);}
    [Fact]public void SelfApprovalIsRejected()=>Assert.Throws<RuleException>(()=>BusinessRules.Independent("one","one"));
    [Fact]public void IndependentApprovalAccepted()=>BusinessRules.Independent("one","two");
    [Fact]public void StaleRevisionRejected()=>Assert.Throws<RuleException>(()=>BusinessRules.Revision(3,2));
    [Fact]public void StockReserveUsesAvailableNotPhysical()=>Assert.Throws<RuleException>(()=>BusinessRules.Reserve(new StockLot{OnHandMilli=2000,ReservedMilli=1000},1500));
    [Fact]public void ExpiredLotCannotBeReserved()=>Assert.Throws<RuleException>(()=>BusinessRules.Reserve(new StockLot{OnHandMilli=2000,ExpiresOn="2000-01-01"},1000));
    [Fact]public void DocumentStartsInQuarantine()=>Assert.Equal("Quarantine",new Evidence().ScanState);
    [Fact]public void TechnicalAdminDoesNotGainFinance()=>Assert.DoesNotContain("finance.write",Access.ForRole(Roles.Admin));
    [Fact]public void ReadOnlyReviewerCannotPay()=>Assert.DoesNotContain("finance.write",Access.ForRole("Reviewer"));
    [Fact]public void PayloadChangeInvalidatesApproval(){var x=new Approval{PayloadJson="{}",PayloadHash=Rules.Hash("{}")};BusinessRules.ApprovalPayload(x);x.PayloadJson="{\"amount\":100}";Assert.Throws<RuleException>(()=>BusinessRules.ApprovalPayload(x));}
    [Theory][InlineData("=1+1")][InlineData("+1")][InlineData("-1")][InlineData("@SUM(A1)")]
    public void CsvFormulaIsEscaped(string value)=>Assert.StartsWith("\"'",BusinessRules.Csv(value));
    [Fact]public void CsvQuotedComma(){var rows=ReconciliationService.CsvRows("reference,date,amount,description\nabc,2026-09-21,1.00,\"A,B\"\n");Assert.Equal("A,B",rows[1][3]);}
    [Fact]public void CsvEscapedQuote(){var rows=ReconciliationService.CsvRows("a,b\n\"hello \"\"world\"\"\",x");Assert.Equal("hello \"world\"",rows[1][0]);}
    [Fact]public void CsvUnclosedQuoteIsRejected()=>Assert.Throws<RuleException>(()=>ReconciliationService.CsvRows("a,\"b"));
    [Theory][InlineData("{\"n\":\"100\"}")][InlineData("{\"n\":0.5}")][InlineData("{\"n\":true}")]
    public void JsonNumbersAreStrict(string value){using var d=JsonDocument.Parse(value);Assert.Throws<RuleException>(()=>Fields.Number(d.RootElement,"n"));}
    [Fact]public void JsonIntegerPreserved(){using var d=JsonDocument.Parse("{\"n\":12345}");Assert.Equal(12345L,Fields.Number(d.RootElement,"n"));}
}
