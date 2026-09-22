using System.Globalization;
using System.Text.Json;
using MongoDB.Driver;
using Terreiro.Data;
using Terreiro.Domain;
namespace Terreiro.Service;

public sealed record Command(string OperationId, int Revision, JsonElement Data);
public static class Fields
{
    public static string Text(JsonElement j,string name,string fallback="") => j.ValueKind==JsonValueKind.Object && j.TryGetProperty(name,out var p) && p.ValueKind==JsonValueKind.String ? p.GetString() ?? fallback : fallback;
    public static string? Optional(JsonElement j,string name) => string.IsNullOrWhiteSpace(Text(j,name)) ? null : Text(j,name).Trim();
    public static long Number(JsonElement j,string name,long fallback=0)
    {
        if(j.ValueKind!=JsonValueKind.Object || !j.TryGetProperty(name,out var p) || p.ValueKind==JsonValueKind.Null) return fallback;
        Rules.Require(p.ValueKind==JsonValueKind.Number && p.TryGetInt64(out _), "number_invalid", "O campo "+name+" exige um número inteiro exato; não envie texto nem frações.");
        return p.GetInt64();
    }
    public static bool Bool(JsonElement j,string name,bool fallback=false) => j.ValueKind==JsonValueKind.Object && j.TryGetProperty(name,out var p) && p.ValueKind is JsonValueKind.True or JsonValueKind.False ? p.GetBoolean() : fallback;
    public static JsonElement[] Rows(JsonElement j,string name) => j.ValueKind==JsonValueKind.Object && j.TryGetProperty(name,out var p) && p.ValueKind==JsonValueKind.Array ? p.EnumerateArray().Take(501).ToArray() : [];
    public static string[] Ids(JsonElement j,string name) => Rows(j,name).Where(x=>x.ValueKind==JsonValueKind.String).Select(x=>x.GetString()!).Distinct().ToArray();
    public static string Required(JsonElement j,string name,int max=160) => Rules.Text(Text(j,name),1,max,name);
}

public sealed partial class FinanceService(ModuleStore m)
{
    private MongoStore Db => m.Root;
    public async Task<object> Command(User actor,string action,Command input,CancellationToken ct)
    {
        Access.Demand(actor,action is "approval.review" or "closing.review" or "cash.review" ? "finance.approve" : "finance.write");
        return await m.Execute<object>(actor,"finance."+action,input.OperationId,input,async(s,c)=>
        {
            var d=input.Data; var h=actor.HouseId;
            switch(action)
            {
                case "account.create":
                {
                    var kind=Fields.Text(d,"kind","Bank"); Rules.Require(kind is "Bank" or "Cash","kind_invalid","Tipo de conta inválido.");
                    var account=new Account{HouseId=h,Name=Rules.Text(Fields.Text(d,"name"),2,100,"Nome"),Kind=kind};
                    await m.Insert(s,account,c); return account;
                }
                case "fund.create":
                {
                    var fund=new Fund{HouseId=h,Name=Rules.Text(Fields.Text(d,"name"),2,100,"Campanha"),Purpose=Rules.Text(Fields.Text(d,"purpose"),3,500,"Finalidade"),GoalCents=BusinessRules.Money(Fields.Number(d,"goalCents"),true),Restricted=Fields.Bool(d,"restricted",true)};
                    await m.Insert(s,fund,c); return fund;
                }
                case "rule.create":
                {
                    var from=BusinessRules.Month(Fields.Text(d,"effectiveFrom")); var until=Fields.Optional(d,"effectiveTo");
                    if(until!=null) Rules.Require(string.CompareOrdinal(BusinessRules.Month(until),from)>=0,"dates_invalid","Vigência final anterior à inicial.");
                    var day=(int)Fields.Number(d,"dueDay",10); Rules.Require(day is >=1 and <=28,"day_invalid","Use um vencimento entre os dias 1 e 28.");
                    var rule=new ContributionRule{CreatedBy=actor.Id,HouseId=h,Name=Rules.Text(Fields.Text(d,"name","Mensalidade"),2,100,"Nome"),AmountCents=BusinessRules.Money(Fields.Number(d,"amountCents")),DueDay=day,EffectiveFrom=from,EffectiveTo=until,AllActiveMembers=Fields.Bool(d,"allActiveMembers",true),MemberIds=Fields.Ids(d,"memberIds").ToList()};
                    Rules.Require(rule.AllActiveMembers || rule.MemberIds.Count>0,"members_required","Escolha membros ou todos os ativos.");
                    await ValidateMembers(s,h,rule.MemberIds,c); await m.Insert(s,rule,c); return rule;
                }
                case "member.exemption.request": return await RequestMemberExemption(s,actor,input,c);
                case "member.exemption.end": return await RequestEndMemberExemption(s,actor,input,c);
                case "member.exemption.withdraw": return await WithdrawMemberExemption(s,actor,input,c);
                case "due.exemption.request":
                {
                    var due = await m.Get<Due>(s,h,Fields.Required(d,"dueId"),c);
                    BusinessRules.Revision(due.Revision,input.Revision);
                    await m.OpenPeriod(s,h,due.Competence+"-01",c);
                    var reason = Rules.Text(Fields.Text(d,"reason"),5,500,"Motivo da isenção");
                    var delta = DueRules.FullExemptionDelta(due); // Always calculated from the authorized persisted record.
                    var pending = await m.Set<Approval>().Find(s,x=>x.HouseId==h && x.Type=="DueAdjustment" && x.TargetId==due.Id && x.TargetRevision==due.Revision && x.State=="Pending").FirstOrDefaultAsync(c);
                    Rules.Require(pending==null,"approval_pending","Já existe um ajuste aguardando aprovação para esta versão da mensalidade.",409);
                    return await RequestApproval(s,actor,"DueAdjustment",due.Id,due.Revision,
                        JsonSerializer.SerializeToElement(new{deltaCents=delta,exempt=true,cancelled=false}),reason,c);
                }
                case "dues.generate": return await Generate(s,actor,BusinessRules.Month(Fields.Text(d,"period")),c);
                case "receipt.confirm": return await ConfirmReceipt(s,actor,d,c);
                case "receipt.allocate":
                {
                    var receipt=await m.Get<Receipt>(s,h,Fields.Required(d,"receiptId"),c); BusinessRules.Revision(receipt.Revision,input.Revision);
                    var rows=Fields.Rows(d,"allocations"); Rules.Require(rows.Length is >0 and <=100,"allocations_invalid","Informe de 1 a 100 destinações.");
                    await m.OpenPeriod(s,h,BusinessRules.Today(),c);
                    foreach(var row in rows) await Allocate(s,actor,receipt,Fields.Optional(row,"dueId"),Fields.Optional(row,"fundId"),BusinessRules.Money(Fields.Number(row,"amountCents")),c);
                    await m.Save(s,receipt,c); return receipt;
                }
                case "transfer":
                {
                    var from=await m.Get<Account>(s,h,Fields.Required(d,"fromId"),c); var to=await m.Get<Account>(s,h,Fields.Required(d,"toId"),c);
                    Rules.Require(from.Id!=to.Id,"same_account","Selecione duas contas diferentes.");
                    var amount=BusinessRules.Money(Fields.Number(d,"amountCents")); var date=BusinessRules.Date(Fields.Text(d,"occurredOn"));BusinessRules.NotFuture(date);
                    var reference=BusinessRules.Reference(Fields.Text(d,"financialReference"));
                    await CashCheck(s,actor,from,Fields.Optional(d,"fromCashSessionId"),c); await CashCheck(s,actor,to,Fields.Optional(d,"toCashSessionId"),c);
                    var source=Guid.NewGuid().ToString("N");
                    await Move(s,actor,from,-amount,"Transfer","Transferência interna",source,date,reference,null,null,c);
                    await Move(s,actor,to,amount,"Transfer","Transferência interna",source,date,reference,null,null,c);
                    return new{id=source};
                }
                case "expense.create":
                {
                    var kind=Fields.Text(d,"kind","Expense");Rules.Require(kind is "Expense" or "Reimbursement" or "Advance","kind_invalid","Tipo de despesa inválido.");
                    var expense=new Expense{HouseId=h,Title=Rules.Text(Fields.Text(d,"title"),3,160,"Descrição"),Kind=kind,Category=Rules.Text(Fields.Text(d,"category","Outros"),2,80,"Categoria"),AmountCents=BusinessRules.Money(Fields.Number(d,"amountCents")),DueDate=BusinessRules.Date(Fields.Text(d,"dueDate")),RequestedBy=actor.Id,Beneficiary=Rules.Text(Fields.Text(d,"beneficiary"),2,200,"Favorecido"),SupplierId=Fields.Optional(d,"supplierId"),EvidenceId=Fields.Optional(d,"evidenceId"),FundId=Fields.Optional(d,"fundId"),EventId=Fields.Optional(d,"eventId")};
                    if(expense.SupplierId!=null) await m.Get<Supplier>(s,h,expense.SupplierId,c);
                    if(expense.FundId!=null) await m.Get<Fund>(s,h,expense.FundId,c);
                    if(expense.EventId!=null) await m.Get<HouseEvent>(s,h,expense.EventId,c);
                    await SafeEvidence(s,actor,expense.EvidenceId,c);
                    var approval=await RequestApproval(s,actor,"Expense",expense.Id,1,JsonSerializer.SerializeToElement(new{}),"Aprovar despesa: "+expense.Title,c);
                    expense.ApprovalId=approval.Id; await m.Insert(s,expense,c); return expense;
                }
                case "expense.pay":
                {
                    var expense=await m.Get<Expense>(s,h,Fields.Required(d,"expenseId"),c);BusinessRules.Revision(expense.Revision,input.Revision);
                    Rules.Require(expense.State is "Approved" or "Partial","not_approved","A despesa precisa estar aprovada.",409);
                    var amount=BusinessRules.Money(Fields.Number(d,"amountCents"));Rules.Require(amount<=expense.AmountCents-expense.PaidCents,"overpayment","Pagamento excede o saldo da despesa.",409);
                    var account=await m.Get<Account>(s,h,Fields.Required(d,"accountId"),c);await CashCheck(s,actor,account,Fields.Optional(d,"cashSessionId"),c);
                    var day=BusinessRules.Date(Fields.Text(d,"occurredOn"));BusinessRules.NotFuture(day);var reference=BusinessRules.Reference(Fields.Text(d,"financialReference"));
                    Rules.Require(Fields.Bool(d,"confirmedExecution"),"execution_required","Confirme a execução efetiva do pagamento, não somente sua aprovação.");
                    var evidenceId=Fields.Optional(d,"evidenceId");await SafeEvidence(s,actor,evidenceId,c);
                    var payment=new ExpensePayment{HouseId=h,ExpenseId=expense.Id,AccountId=account.Id,AmountCents=amount,FinancialReference=reference,OccurredOn=day,CashSessionId=Fields.Optional(d,"cashSessionId"),EvidenceId=evidenceId};
                    await Move(s,actor,account,-amount,expense.Kind=="Advance"?"Advance":"Expense",expense.Category,payment.Id,day,reference,expense.FundId,expense.EventId,c);
                    expense.PaidCents+=amount;expense.State=expense.PaidCents==expense.AmountCents?"Paid":"Partial";await m.Save(s,expense,c);await m.Insert(s,payment,c);
                    await m.Notify(s,h,expense.RequestedBy,"expense-paid:"+payment.Id,"Pagamento registrado","Consulte o pagamento no aplicativo.","/despesas","Finance",c);return expense;
                }
                case "approval.request":
                {
                    var type=Fields.Text(d,"type");var targetId=Fields.Required(d,"targetId");
                    Rules.Require(type is "DueAdjustment" or "Refund" or "ReopenPeriod" or "OpeningBalance" or "CashAdjustment" or "FundTransfer" or "AdvanceSettlement","approval_type","Tipo de aprovação inválido.");
                    Rules.Require(d.TryGetProperty("payload",out var payload) && payload.ValueKind==JsonValueKind.Object,"payload_invalid","Dados da solicitação ausentes.");
                    await ValidateApprovalRequest(s,actor,type,targetId,input.Revision,payload,c);
                    return await RequestApproval(s,actor,type,targetId,input.Revision,payload,Rules.Text(Fields.Text(d,"reason"),5,500,"Motivo"),c);
                }
                case "approval.review": return await ReviewApproval(s,actor,Fields.Required(d,"approvalId"),input.Revision,Fields.Bool(d,"approve"),Rules.Text(Fields.Text(d,"note"),3,500,"Parecer"),c);
                case "refund.execute":
                {
                    var refund=await m.Get<Refund>(s,h,Fields.Required(d,"refundId"),c);BusinessRules.Revision(refund.Revision,input.Revision);
                    Rules.Require(refund.State=="Approved","refund_state","A devolução não está autorizada ou já foi executada.",409);
                    Rules.Require(Fields.Bool(d,"confirmedExecution"),"execution_required","Confirme que a devolução foi efetivamente executada.");
                    var receipt=await m.Get<Receipt>(s,h,refund.ReceiptId,c);var account=await m.Get<Account>(s,h,receipt.AccountId,c);
                    var date=BusinessRules.Date(Fields.Text(d,"occurredOn"));BusinessRules.NotFuture(date); var reference=BusinessRules.Reference(Fields.Text(d,"financialReference"));
                    await CashCheck(s,actor,account,Fields.Optional(d,"cashSessionId"),c);
                    Rules.Require(refund.AmountCents<=receipt.AmountCents-receipt.RefundedCents,"refund_amount","Saldo devolvível insuficiente.",409);
                    var needed=Math.Max(0,refund.AmountCents-receipt.AvailableCents);
                    var allocations=await m.Set<Allocation>().Find(s,x=>x.HouseId==h && x.ReceiptId==receipt.Id).SortByDescending(x=>x.CreatedAt).ToListAsync(c);
                    foreach(var a in allocations)
                    {
                        if(needed==0) break;var reverse=Math.Min(needed,a.AmountCents-a.ReversedCents);if(reverse==0)continue;
                        if(a.DueId!=null){var due=await m.Get<Due>(s,h,a.DueId,c);due.PaidCents-=reverse;await m.Save(s,due,c);}
                        if(a.FundId!=null){var fund=await m.Get<Fund>(s,h,a.FundId,c);Rules.Require(fund.BalanceCents>=reverse,"fund_used","Recursos vinculados já utilizados; regularize a disponibilidade antes da devolução.",409);fund.BalanceCents-=reverse;await m.Save(s,fund,c);}
                        a.ReversedCents+=reverse;receipt.AllocatedCents-=reverse;needed-=reverse;await m.Save(s,a,c);
                    }
                    Rules.Require(needed==0,"allocation_mismatch","Não foi possível recompor a destinação.",409);
                    await Move(s,actor,account,-refund.AmountCents,"Refund","Devolução",refund.Id,date,reference,null,null,c);
                    receipt.RefundedCents+=refund.AmountCents;await m.Save(s,receipt,c);
                    refund.State="Executed";refund.FinancialReference=reference;refund.EvidenceId=Fields.Optional(d,"evidenceId");await SafeEvidence(s,actor,refund.EvidenceId,c);await m.Save(s,refund,c);return refund;
                }
                case "cash.open":
                {
                    var account=await m.Get<Account>(s,h,Fields.Required(d,"accountId"),c);Rules.Require(account.Kind=="Cash","not_cash","Selecione um caixa físico.");
                    Rules.Require(!await m.Set<CashSession>().Find(s,x=>x.HouseId==h && x.AccountId==account.Id && x.State=="Open").AnyAsync(c),"cash_open","Já existe uma sessão de caixa aberta.",409);
                    Rules.Require(Fields.Number(d,"countedCents")==account.BalanceCents,"opening_difference","A contagem de abertura diverge do saldo. Registre uma solicitação de ajuste, sem ocultar a diferença.",409);
                    var session=new CashSession{HouseId=h,AccountId=account.Id,OperatorId=actor.Id,OpeningCents=account.BalanceCents};await m.Insert(s,session,c);return session;
                }
                case "cash.close":
                {
                    var session=await m.Get<CashSession>(s,h,Fields.Required(d,"sessionId"),c);BusinessRules.Revision(session.Revision,input.Revision);
                    Rules.Require(session.OperatorId==actor.Id && session.State=="Open","cash_owner","Somente o operador pode encerrar este caixa aberto.",403);
                    var account=await m.Get<Account>(s,h,session.AccountId,c);session.ExpectedCents=account.BalanceCents;session.CountedCents=BusinessRules.Money(Fields.Number(d,"countedCents"),true);session.DifferenceCents=session.CountedCents-session.ExpectedCents;session.Note=Rules.Text(Fields.Text(d,"note"),3,500,"Observação");session.State="PendingReview";await m.Save(s,session,c);return session;
                }
                case "cash.review":
                {
                    var session=await m.Get<CashSession>(s,h,Fields.Required(d,"sessionId"),c);BusinessRules.Revision(session.Revision,input.Revision);BusinessRules.Independent(session.OperatorId,actor.Id);
                    Rules.Require(session.State=="PendingReview","cash_state","Caixa não está aguardando revisão.",409);session.ReviewedBy=actor.Id;session.State=session.DifferenceCents==0?"Closed":"ClosedWithDifference";await m.Save(s,session,c);return session;
                }
                case "closing.prepare":
                {
                    var period=BusinessRules.Month(Fields.Text(d,"period"));Rules.Require(string.CompareOrdinal(period,BusinessRules.Today()[..7])<=0,"future_closing","Não encerre competência futura.");
                    Rules.Require(!await m.Set<CashSession>().Find(s,x=>x.HouseId==h && x.State=="Open").AnyAsync(c),"cash_open","Encerre os caixas abertos antes do fechamento.",409);
                    var previous=await m.Set<Closing>().Find(s,x=>x.HouseId==h && x.Period==period).FirstOrDefaultAsync(c);
                    Rules.Require(previous==null || previous.State=="Reopened","already_closed","O período já está fechado ou em revisão.",409);
                    Rules.Require(!await m.Set<BankObservation>().Find(s,b=>b.HouseId==h&&b.State!="Reconciled").AnyAsync(c),"bank_review_pending","Resolva as divergências de recebimentos/devoluções do provedor antes do fechamento.",409);
                    Rules.Require(!await m.Set<CashSession>().Find(s,b=>b.HouseId==h&&(b.State=="Open"||b.State=="PendingReview")).AnyAsync(c),"cash_review_pending","Encerre e revise os caixas antes do fechamento.",409);
                    var entries=await m.Set<LedgerEntry>().Find(s,Builders<LedgerEntry>.Filter.Eq(x=>x.HouseId,h)&Builders<LedgerEntry>.Filter.Gte(x=>x.OccurredOn,period+"-01")&Builders<LedgerEntry>.Filter.Lte(x=>x.OccurredOn,period+"-31")).ToListAsync(c);
                    var snapshot=JsonSerializer.Serialize(new{accounts=await m.Set<Account>().Find(s,x=>x.HouseId==h).ToListAsync(c),funds=await m.Set<Fund>().Find(s,x=>x.HouseId==h).ToListAsync(c),movements=entries});
                    var closing=previous??new Closing{HouseId=h,Period=period};if(previous!=null)closing.PreviousSnapshots.Add(closing.SnapshotJson);
                    closing.PreparedBy=actor.Id;closing.ReviewedBy=null;closing.State="PendingReview";closing.Note=Rules.Text(Fields.Text(d,"note"),3,500,"Pendências/observações");closing.SnapshotJson=snapshot;
                    if(previous==null)await m.Insert(s,closing,c);else await m.Save(s,closing,c);return closing;
                }
                case "closing.review":
                {
                    var closing=await m.Get<Closing>(s,h,Fields.Required(d,"closingId"),c);BusinessRules.Revision(closing.Revision,input.Revision);BusinessRules.Independent(closing.PreparedBy,actor.Id);
                    Rules.Require(closing.State=="PendingReview","closing_state","Fechamento não está aguardando revisão.",409);closing.State="Closed";closing.ReviewedBy=actor.Id;await m.Save(s,closing,c);return closing;
                }
                default: throw new RuleException("action_unknown","Operação financeira desconhecida.",404);
            }
        },ct);
    }
    public async Task<object> Generate(IClientSessionHandle s,User actor,string period,CancellationToken ct)
    {
        var h=actor.HouseId;await m.OpenPeriod(s,h,period+"-01",ct);
        var members=await Db.Users.Find(s,x=>x.HouseId==h && x.Active && x.IsMember).ToListAsync(ct);
        var rules=await m.Set<ContributionRule>().Find(s,x=>x.HouseId==h && x.Active).ToListAsync(ct);int count=0;
        var exemptions=await m.Set<MemberExemption>().Find(s,x=>x.HouseId==h && x.State=="Approved").ToListAsync(ct);
        foreach(var member in members)
        {
            if(await m.Set<Due>().Find(s,x=>x.HouseId==h && x.MemberId==member.Id && x.Competence==period).AnyAsync(ct)) continue;
            var rule=rules.Where(r=>string.CompareOrdinal(r.EffectiveFrom,period)<=0 && (r.EffectiveTo==null||string.CompareOrdinal(r.EffectiveTo,period)>=0) && (r.AllActiveMembers||r.MemberIds.Contains(member.Id))).OrderByDescending(r=>r.EffectiveFrom).ThenBy(r=>r.AllActiveMembers).ThenByDescending(r=>r.CreatedAt).FirstOrDefault();
            if(rule==null)continue;
            var due=new Due{Id=Rules.Hash(h+"|due|"+member.Id+"|"+period)[..32],HouseId=h,MemberId=member.Id,RuleId=rule.Id,Competence=period,AmountCents=rule.AmountCents,DueDate=period+"-"+rule.DueDay.ToString("00",CultureInfo.InvariantCulture)};
            var exemption=MemberExemptionRules.Select(exemptions,h,member.Id,period);
            if(exemption!=null) MemberExemptionRules.Apply(due,exemption);
            await m.Insert(s,due,ct);count++;
            await m.Notify(s,h,member.Id,"due:"+due.Id,due.Exempt?"Mensalidade regularizada":"Mensalidade disponível","Consulte os detalhes no aplicativo.","/mensalidades","Finance",ct);
        }
        return new{generated=count,period};
    }
    public async Task<Receipt> ConfirmReceipt(IClientSessionHandle s,User actor,JsonElement d,CancellationToken ct)
    {
        var h=actor.HouseId;var account=await m.Get<Account>(s,h,Fields.Required(d,"accountId"),ct);
        var date=BusinessRules.Date(Fields.Text(d,"occurredOn"));BusinessRules.NotFuture(date);var amount=BusinessRules.Money(Fields.Number(d,"amountCents"));var reference=BusinessRules.Reference(Fields.Text(d,"financialReference"));
        Rules.Require(Fields.Bool(d,"confirmedCredit"),"credit_confirmation","Confira o crédito no banco ou o dinheiro recebido antes de registrar.");
        var memberId=Fields.Optional(d,"memberId");if(memberId!=null)await ValidateMembers(s,h,[memberId],ct);
        var evidenceId=Fields.Optional(d,"evidenceId");Evidence? evidence=null;
        if(evidenceId!=null){evidence=await m.Get<Evidence>(s,h,evidenceId,ct);Rules.Require(evidence.Purpose=="Receipt","purpose_invalid","Arquivo não é um envio de comprovante.");Rules.Require(evidence.ReceiptId==null,"evidence_used","Este envio já foi vinculado a um recebimento.",409);}
        var receipt=new Receipt{HouseId=h,AccountId=account.Id,AmountCents=amount,FinancialReference=reference,Verification=account.Kind=="Cash"?"Cash":"Treasury",VerifiedBy=actor.Id,MemberId=memberId,PayerName=Rules.Text(Fields.Text(d,"payerName","Não identificado"),2,100,"Pagador"),OccurredOn=date,VerificationNote=Rules.Text(Fields.Text(d,"verificationNote"),8,500,"Origem da conferência"),CashSessionId=Fields.Optional(d,"cashSessionId"),EvidenceId=evidenceId};
        await CashCheck(s,actor,account,receipt.CashSessionId,ct);
        await Move(s,actor,account,amount,"Receipt","Recebimento",receipt.Id,date,reference,null,null,ct);
        await m.Insert(s,receipt,ct);
        if(evidence!=null){evidence.ReceiptId=receipt.Id;evidence.ReviewState="CreditConfirmed";await m.Save(s,evidence,ct);}
        return receipt;
    }
    public async Task Allocate(IClientSessionHandle s,User actor,Receipt receipt,string? dueId,string? fundId,long amount,CancellationToken ct)
    {
        await m.OpenPeriod(s,actor.HouseId,BusinessRules.Today(),ct);
        Rules.Require(dueId==null||fundId==null,"allocation_target","Escolha mensalidade ou doação, não as duas na mesma destinação.");
        var due=dueId==null?null:await m.Get<Due>(s,actor.HouseId,dueId,ct);BusinessRules.Allocate(receipt,amount,due);
        if(due!=null){due.PaidCents+=amount;await m.Save(s,due,ct);}
        if(fundId!=null){var fund=await m.Get<Fund>(s,actor.HouseId,fundId,ct);Rules.Require(fund.Active,"fund_inactive","Campanha encerrada.",409);fund.BalanceCents=checked(fund.BalanceCents+amount);await m.Save(s,fund,ct);}
        var allocation=new Allocation{HouseId=actor.HouseId,ReceiptId=receipt.Id,DueId=dueId,FundId=fundId,MemberId=due?.MemberId??receipt.MemberId,Kind=due==null?"Donation":"Due",AmountCents=amount,CreatedBy=actor.Id};
        receipt.AllocatedCents+=amount;await m.Insert(s,allocation,ct);
        if(allocation.MemberId!=null)await m.Notify(s,actor.HouseId,allocation.MemberId,"allocation:"+allocation.Id,"Recebimento destinado","Seu recibo está disponível no aplicativo.","/recibos","Finance",ct);
    }
    public async Task<LedgerEntry> Move(IClientSessionHandle s,User actor,Account account,long signed,string kind,string description,string source,string date,string? financialReference,string? fundId,string? eventId,CancellationToken ct)
    {
        await m.OpenPeriod(s,actor.HouseId,date,ct);Rules.Require(account.Active,"account_inactive","Conta inativa.",409);
        Rules.Require(signed!=0 && signed>=-BusinessRules.MaxCents && signed<=BusinessRules.MaxCents,"amount_invalid","Valor inválido.");
        if(financialReference!=null)Rules.Require(!await m.Set<LedgerEntry>().Find(s,x=>x.HouseId==actor.HouseId && x.AccountId==account.Id && x.FinancialReference==financialReference).AnyAsync(ct),"duplicate_financial_reference","A referência financeira já foi registrada nesta conta.",409);
        var next=checked(account.BalanceCents+signed);Rules.Require(next>=0,"insufficient_cash","Saldo insuficiente na conta. Confira os saldos e lançamentos.",409);
        if(fundId!=null && signed<0){var fund=await m.Get<Fund>(s,actor.HouseId,fundId,ct);Rules.Require(fund.BalanceCents>=-signed,"insufficient_fund","Saldo insuficiente da campanha vinculada.",409);fund.BalanceCents+=signed;await m.Save(s,fund,ct);}
        if(signed<0 && fundId==null && kind is not "Transfer" and not "Refund")
        {
            var all=await m.Set<Account>().Find(s,x=>x.HouseId==actor.HouseId && x.Active).ToListAsync(ct);
            var restricted=await m.Set<Fund>().Find(s,x=>x.HouseId==actor.HouseId && x.Restricted).ToListAsync(ct);
            Rules.Require(all.Sum(x=>x.BalanceCents)+signed>=restricted.Sum(x=>x.BalanceCents),"restricted_cash","A operação usaria recursos reservados a campanhas.",409);
        }
        account.BalanceCents=next;await m.Save(s,account,ct);
        var entry=new LedgerEntry{HouseId=actor.HouseId,AccountId=account.Id,SignedCents=signed,Kind=kind,Category=description,Description=description,SourceId=source,OccurredOn=date,FinancialReference=financialReference,FundId=fundId,EventId=eventId,ActorId=actor.Id};
        await m.Insert(s,entry,ct);return entry;
    }
    public async Task<Approval> RequestApproval(IClientSessionHandle s,User actor,string type,string targetId,int revision,JsonElement payload,string reason,CancellationToken ct)
    {
        var json=payload.GetRawText();var approval=new Approval{HouseId=actor.HouseId,Type=type,TargetId=targetId,TargetRevision=revision,RequestedBy=actor.Id,PayloadJson=json,PayloadHash=Rules.Hash(json),Reason=reason};
        await m.Insert(s,approval,ct);
        var reviewers=await Db.Users.Find(s,x=>x.HouseId==actor.HouseId && x.Active && x.Permissions.Contains("finance.approve") && x.Id!=actor.Id).ToListAsync(ct);
        foreach(var user in reviewers)await m.Notify(s,actor.HouseId,user.Id,"approval:"+approval.Id,"Aprovação pendente","Há uma solicitação aguardando sua revisão.","/aprovacoes","Finance",ct);
        return approval;
    }
    private async Task ValidateApprovalRequest(IClientSessionHandle s,User actor,string type,string id,int version,JsonElement payload,CancellationToken ct)
    {
        Entity target=type switch
        {
            "DueAdjustment"=>await m.Get<Due>(s,actor.HouseId,id,ct),
            "Refund"=>await m.Get<Receipt>(s,actor.HouseId,id,ct),
            "ReopenPeriod"=>await m.Get<Closing>(s,actor.HouseId,id,ct),
            "OpeningBalance" or "CashAdjustment"=>await m.Get<Account>(s,actor.HouseId,id,ct),
            "FundTransfer"=>await m.Get<Fund>(s,actor.HouseId,id,ct),
            "AdvanceSettlement"=>await m.Get<Expense>(s,actor.HouseId,id,ct),
            _=>throw new RuleException("approval_type","Tipo inválido.")
        };
        BusinessRules.Revision(target.Revision,version);
        if(target is Due due)
        {
            await m.OpenPeriod(s,actor.HouseId,due.Competence+"-01",ct);
            DueRules.ValidateAdjustment(due,Fields.Number(payload,"deltaCents"),Fields.Bool(payload,"exempt"),Fields.Bool(payload,"cancelled"));
        }
        if(type is "Refund" or "OpeningBalance" or "FundTransfer")BusinessRules.Money(Fields.Number(payload,"amountCents"));
        if(type=="AdvanceSettlement"){var evidenceId=Fields.Required(payload,"evidenceId");await SafeEvidence(s,actor,evidenceId,ct);}
    }
    private async Task<Approval> ReviewApproval(IClientSessionHandle s,User actor,string id,int revision,bool approve,string note,CancellationToken ct)
    {
        var a=await m.Get<Approval>(s,actor.HouseId,id,ct);BusinessRules.Revision(a.Revision,revision);BusinessRules.Independent(a.RequestedBy,actor.Id);BusinessRules.ApprovalPayload(a);Rules.Require(a.State=="Pending","approval_state","Solicitação já revisada.",409);
        if(approve)
        {
            var p=JsonDocument.Parse(a.PayloadJson).RootElement;var h=actor.HouseId;
            switch(a.Type)
            {
                case "Expense":
                {
                    var x=await m.Get<Expense>(s,h,a.TargetId,ct);BusinessRules.Revision(x.Revision,a.TargetRevision);Rules.Require(x.State=="PendingApproval","expense_state","Despesa fora da etapa de aprovação.",409);
                    x.State="Approved";x.ApprovedBy=actor.Id;await m.Save(s,x,ct);break;
                }
                case "MemberExemption":
                case "EndMemberExemption":
                {
                    await ReviewMemberExemption(s,actor,a,p,ct);break;
                }
                case "DueAdjustment":
                {
                    var x=await m.Get<Due>(s,h,a.TargetId,ct);BusinessRules.Revision(x.Revision,a.TargetRevision);await m.OpenPeriod(s,h,x.Competence+"-01",ct);
                    var delta=Fields.Number(p,"deltaCents");var exempt=Fields.Bool(p,"exempt");var cancelled=Fields.Bool(p,"cancelled");
                    DueRules.ValidateAdjustment(x,delta,exempt,cancelled);
                    if(exempt || cancelled || delta<0) BusinessRules.Independent(x.MemberId,actor.Id);
                    var reason=exempt?Rules.Text(a.Reason,5,500,"Motivo da isenção"):null;
                    x.AdjustmentCents+=delta;x.Exempt=exempt;x.Cancelled=cancelled;
                    // An explicit individual decision supersedes the automatic link for this due only.
                    x.MemberExemptionId=null;x.MemberExemptionAdjustmentCents=0;
                    x.ExemptionReason=reason;x.ExemptionApprovalId=exempt?a.Id:null;
                    x.ExemptionApprovedBy=exempt?actor.Id:null;x.ExemptionApprovedAt=exempt?DateTime.UtcNow:null;
                    await m.Save(s,x,ct);
                    await m.Notify(s,h,x.MemberId,"due-adjusted:"+a.Id,"Mensalidade atualizada","Consulte os detalhes da mensalidade no aplicativo.","/mensalidades","Finance",ct);
                    break;
                }
                case "Refund":
                {
                    var x=await m.Get<Receipt>(s,h,a.TargetId,ct);BusinessRules.Revision(x.Revision,a.TargetRevision);var amount=BusinessRules.Money(Fields.Number(p,"amountCents"));
                    var pending=await m.Set<Refund>().Find(s,r=>r.HouseId==h && r.ReceiptId==x.Id && r.State=="Approved").ToListAsync(ct);
                    Rules.Require(amount+pending.Sum(r=>r.AmountCents)<=x.AmountCents-x.RefundedCents,"refund_amount","Valor devolvível insuficiente.",409);
                    await m.Insert(s,new Refund{HouseId=h,ReceiptId=x.Id,AmountCents=amount,ApprovalId=a.Id,Reason=a.Reason},ct);break;
                }
                case "ReopenPeriod":
                {
                    var x=await m.Get<Closing>(s,h,a.TargetId,ct);BusinessRules.Revision(x.Revision,a.TargetRevision);Rules.Require(x.State!="Reopened","closing_state","Período já reaberto.",409);x.State="Reopened";await m.Save(s,x,ct);break;
                }
                case "OpeningBalance":
                case "CashAdjustment":
                {
                    var x=await m.Get<Account>(s,h,a.TargetId,ct);BusinessRules.Revision(x.Revision,a.TargetRevision);
                    if(a.Type=="OpeningBalance")Rules.Require(!await m.Set<LedgerEntry>().Find(s,e=>e.HouseId==h && e.AccountId==x.Id).AnyAsync(ct),"opening_exists","Saldo inicial só pode ser lançado antes da primeira movimentação.",409);
                    else Rules.Require(x.Kind=="Cash","not_cash","Ajuste de contagem somente para caixa físico.");
                    var amount=Fields.Number(p,"amountCents");Rules.Require(amount!=0,"amount_invalid","Ajuste zero não altera o caixa.");var day=BusinessRules.Date(Fields.Text(p,"occurredOn"));BusinessRules.NotFuture(day);
                    await Move(s,actor,x,amount,a.Type=="OpeningBalance"?"Opening":"Adjustment",a.Reason,a.Id,day,null,null,null,ct);break;
                }
                case "FundTransfer":
                {
                    Rules.Require(Fields.Bool(p,"conditionsRespected"),"fund_conditions","Confirme o respeito às condições assumidas na destinação dos recursos.");
                    var from=await m.Get<Fund>(s,h,a.TargetId,ct);BusinessRules.Revision(from.Revision,a.TargetRevision);var to=await m.Get<Fund>(s,h,Fields.Required(p,"toFundId"),ct);Rules.Require(from.Id!=to.Id,"same_fund","Campanhas devem ser diferentes.");var amount=BusinessRules.Money(Fields.Number(p,"amountCents"));Rules.Require(from.BalanceCents>=amount,"fund_balance","Saldo vinculado insuficiente.",409);from.BalanceCents-=amount;to.BalanceCents=checked(to.BalanceCents+amount);await m.Save(s,from,ct);await m.Save(s,to,ct);break;
                }
                case "AdvanceSettlement":
                {
                    var x=await m.Get<Expense>(s,h,a.TargetId,ct);BusinessRules.Revision(x.Revision,a.TargetRevision);Rules.Require(x.Kind=="Advance","not_advance","A despesa não é um adiantamento.");
                    var spent=BusinessRules.Money(Fields.Number(p,"spentCents"),true);var returned=BusinessRules.Money(Fields.Number(p,"returnedCents"),true);
                    Rules.Require(spent+returned==x.PaidCents-x.SettledAdvanceCents-x.ReturnedAdvanceCents,"advance_difference","Prestação e devolução precisam explicar todo o saldo entregue.",409);
                    await SafeEvidence(s,actor,Fields.Required(p,"evidenceId"),ct);
                    if(returned>0){var account=await m.Get<Account>(s,h,Fields.Required(p,"returnAccountId"),ct);await CashCheck(s,actor,account,Fields.Optional(p,"cashSessionId"),ct);await Move(s,actor,account,returned,"AdvanceReturn","Devolução de adiantamento",a.Id,BusinessRules.Today(),BusinessRules.Reference(Fields.Text(p,"financialReference")),null,null,ct);if(x.FundId!=null){var fund=await m.Get<Fund>(s,h,x.FundId,ct);fund.BalanceCents+=returned;await m.Save(s,fund,ct);}}
                    x.SettledAdvanceCents+=spent;x.ReturnedAdvanceCents+=returned;await m.Save(s,x,ct);break;
                }
                default: throw new RuleException("approval_type","Esta solicitação deve ser revisada no módulo de origem.");
            }
        }
        else if(a.Type=="Expense"){var x=await m.Get<Expense>(s,actor.HouseId,a.TargetId,ct);BusinessRules.Revision(x.Revision,a.TargetRevision);x.State="Rejected";await m.Save(s,x,ct);}
        if(!approve && a.Type=="MemberExemption")
        {
            var rejected=await m.Get<MemberExemption>(s,actor.HouseId,a.TargetId,ct);
            BusinessRules.Revision(rejected.Revision,a.TargetRevision);rejected.State="Rejected";await m.Save(s,rejected,ct);
        }
        a.State=approve?"Approved":"Rejected";a.ReviewedBy=actor.Id;a.ReviewNote=note;a.ReviewedAt=DateTime.UtcNow;await m.Save(s,a,ct);
        await m.Notify(s,actor.HouseId,a.RequestedBy,"review:"+a.Id,"Solicitação revisada","Consulte o resultado da revisão no aplicativo.","/aprovacoes","Finance",ct);return a;
    }
    public async Task CashCheck(IClientSessionHandle s,User actor,Account account,string? sessionId,CancellationToken ct)
    {
        if(account.Kind!="Cash")return;Rules.Require(sessionId!=null,"cash_session_required","Selecione a sessão de caixa aberta.");
        var session=await m.Get<CashSession>(s,actor.HouseId,sessionId,ct);Rules.Require(session.AccountId==account.Id && session.State=="Open" && session.OperatorId==actor.Id,"cash_session_invalid","Caixa deve estar aberto sob sua responsabilidade.",409);
    }
    public async Task SafeEvidence(IClientSessionHandle s,User actor,string? id,CancellationToken ct)
    {
        if(id==null)return;var e=await m.Get<Evidence>(s,actor.HouseId,id,ct);Rules.Require(e.ScanState=="Clean" && e.PurgedAt==null,"evidence_unavailable","Documento ainda não liberado pela análise de segurança.",409);
    }
    public async Task ValidateMembers(IClientSessionHandle s,string house,IEnumerable<string> ids,CancellationToken ct)
    {
        var selected=ids.Distinct().ToArray();if(selected.Length==0)return;
        var count=await Db.Users.CountDocumentsAsync(s,x=>x.HouseId==house && x.Active && selected.Contains(x.Id),cancellationToken:ct);
        Rules.Require(count==selected.Length,"member_invalid","Um dos membros não está ativo nesta casa.");
    }
}
