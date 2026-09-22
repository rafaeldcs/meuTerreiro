using System.Globalization;
using System.Text.Json;
using MongoDB.Driver;
using Terreiro.Data;
using Terreiro.Domain;
using Terreiro.Service;
namespace Terreiro.WebAPI;

public sealed class PixService(ModuleStore m,EfiPixClient client,FinanceService finance)
{
    public object Configuration()=>new{enabled=client.Enabled,provider=client.Enabled?"Efi":null,environment="Sandbox",recipient=client.Enabled?client.Recipient:null,realMoneyEnabled=false};
    public Task<PaymentIntent> Create(User actor,Command input,CancellationToken ct)
    {
        Rules.Require(client.Enabled,"pix_unconfigured","Integração Pix não configurada neste ambiente. Use envio de comprovante e conferência pela tesouraria.",409);
        return m.Execute(actor,"pix.intent",input.OperationId,input,async(s,c)=>
        {
            var ids=Fields.Ids(input.Data,"dueIds");Rules.Require(ids.Length is >0 and <=24,"dues_invalid","Selecione até 24 mensalidades.");var amount=0L;
            foreach(var id in ids){var due=await m.Get<Due>(s,actor.HouseId,id,c);Rules.Require(due.MemberId==actor.Id&&due.BalanceCents>0,"due_unavailable","Mensalidade não disponível para este pagamento.",409);amount=checked(amount+due.BalanceCents);}
            var account=await m.Get<Account>(s,actor.HouseId,client.AccountId,c);Rules.Require(account.Active&&account.Kind=="Bank","account_invalid","Conta recebedora não configurada corretamente.",409);
            var intent=new PaymentIntent{HouseId=actor.HouseId,MemberId=actor.Id,DueIds=ids.ToList(),AmountCents=BusinessRules.Money(amount),AccountId=account.Id,ProviderEnvironment=client.Environment,Txid="t"+Rules.Hash(actor.HouseId+"|"+actor.Id+"|"+input.OperationId)[..31]};await m.Insert(s,intent,c);return intent;
        },ct);
    }
    public async Task Synchronize(PaymentIntent observed,CancellationToken ct)
    {
        var charge=await client.GetCharge(observed.Txid,ct)??await client.CreateCharge(observed.Txid,observed.AmountCents,ct);
        Rules.Require(Fields.Text(charge,"txid")==observed.Txid,"pix_reference","Referência financeira divergente.",409);
        await m.Set<PaymentIntent>().UpdateOneAsync(x=>x.Id==observed.Id&&x.HouseId==observed.HouseId,Builders<PaymentIntent>.Update.Set(x=>x.CopyPaste,Fields.Optional(charge,"pixCopiaECola")).Set(x=>x.ErrorCode,null).Set(x=>x.State,"AwaitingPayment"),cancellationToken:ct);
        foreach(var entry in Fields.Rows(charge,"pix"))
        {
            var reference=BusinessRules.Reference(Fields.Text(entry,"endToEndId"));var pix=await client.GetPix(reference,ct);
            Rules.Require(Fields.Text(pix,"endToEndId")==reference&&Fields.Text(pix,"txid")==observed.Txid,"pix_reference","Transação não corresponde à cobrança.",409);
            var amount=ParseAmount(Fields.Text(pix,"valor"));Rules.Require(DateTimeOffset.TryParse(Fields.Text(pix,"horario"),out var occurred),"pix_date","Data financeira inválida.",409);var date=TimeZoneInfo.ConvertTime(occurred,TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).ToString("yyyy-MM-dd");
            var refunded=Fields.Rows(pix,"devolucoes").Where(x=>Fields.Text(x,"status")=="DEVOLVIDO").Sum(x=>ParseAmount(Fields.Text(x,"valor")));Rules.Require(refunded<=amount,"pix_refund","Devoluções excedem o recebimento informado.",409);
            var observationId=Rules.Hash(observed.HouseId+"|"+observed.AccountId+"|"+reference);var bank=new BankObservation{Id=observationId,HouseId=observed.HouseId,AccountId=observed.AccountId,IntentId=observed.Id,FinancialReference=reference,AmountCents=amount,OccurredOn=date,ProviderRefundedCents=refunded};
            await m.Set<BankObservation>().UpdateOneAsync(x=>x.Id==observationId,Builders<BankObservation>.Update.SetOnInsert(x=>x.HouseId,bank.HouseId).SetOnInsert(x=>x.AccountId,bank.AccountId).SetOnInsert(x=>x.IntentId,bank.IntentId).SetOnInsert(x=>x.FinancialReference,reference).SetOnInsert(x=>x.AmountCents,amount).SetOnInsert(x=>x.OccurredOn,date).SetOnInsert(x=>x.State,"Pending").SetOnInsert(x=>x.CreatedAt,bank.CreatedAt).Set(x=>x.ProviderRefundedCents,refunded).Set(x=>x.LastCheckedAt,DateTime.UtcNow),new UpdateOptions{IsUpsert=true},ct);
            try{await Apply(observed,bank,ct);}
            catch(RuleException ex)
            {
                await m.Set<BankObservation>().UpdateOneAsync(x=>x.Id==bank.Id,Builders<BankObservation>.Update.Set(x=>x.State,"NeedsReview").Set(x=>x.Note,ex.Message),cancellationToken:ct);
                await m.Set<PaymentIntent>().UpdateOneAsync(x=>x.Id==observed.Id,Builders<PaymentIntent>.Update.Set(x=>x.State,"NeedsReview").Set(x=>x.ErrorCode,ex.Code),cancellationToken:ct);
            }
        }
    }
    private async Task Apply(PaymentIntent observed,BankObservation observation,CancellationToken ct)
    {
        await m.Root.Transaction(async(s,c)=>
        {
            var h=observed.HouseId;await m.Set<HouseGate>().UpdateOneAsync(s,x=>x.Id==h,Builders<HouseGate>.Update.Inc(x=>x.Sequence,1),cancellationToken:c);
            var intent=await m.Get<PaymentIntent>(s,h,observed.Id,c);Rules.Require(intent.AccountId==client.AccountId&&intent.ProviderEnvironment==client.Environment,"pix_configuration","A conta ou ambiente da integração mudou. Exige revisão.",409);
            var bank=await m.Get<BankObservation>(s,h,observation.Id,c);var account=await m.Get<Account>(s,h,intent.AccountId,c);var actor=new User{Id="integration:efi:sandbox",HouseId=h,Role="Integration",Active=true};
            var receipt=await m.Set<Receipt>().Find(s,x=>x.HouseId==h&&x.AccountId==account.Id&&x.FinancialReference==bank.FinancialReference).FirstOrDefaultAsync(c);
            if(receipt==null)
            {
                await m.OpenPeriod(s,h,bank.OccurredOn,c);
                receipt=new Receipt{HouseId=h,AccountId=account.Id,AmountCents=bank.AmountCents,MemberId=intent.MemberId,OccurredOn=bank.OccurredOn,FinancialReference=bank.FinancialReference,Verification="ProviderSandbox",VerifiedBy=actor.Id,VerificationNote="Consulta autenticada ao ambiente de homologação Efí; valor simulado, não dinheiro real."};
                await m.Insert(s,receipt,c);await finance.Move(s,actor,account,receipt.AmountCents,"Receipt","Recebimento Pix SIMULADO de homologação",receipt.Id,receipt.OccurredOn,receipt.FinancialReference,null,null,c);
            }
            Rules.Require(receipt.AmountCents==bank.AmountCents&&receipt.OccurredOn==bank.OccurredOn,"pix_existing_mismatch","A referência já existe com dados divergentes. Revisão necessária.",409);
            bank.ReceiptId=receipt.Id;
            // Reconcile confirmed refunds using independently approved refund execution; never conceal the discrepancy.
            if(bank.ProviderRefundedCents!=receipt.RefundedCents)
            {
                bank.State="RefundNeedsReconciliation";bank.Note="O provedor confirmou devolução. Registre a execução autorizada e confira o extrato antes de fechar o período.";intent.State="NeedsReview";
            }
            else
            {
                var beforeAllocated=receipt.AllocatedCents;
                foreach(var id in intent.DueIds)
                {
                    var due=await m.Get<Due>(s,h,id,c);Rules.Require(due.MemberId==intent.MemberId,"pix_owner","Beneficiário alterado.",409);var amount=Math.Min(receipt.AvailableCents,due.BalanceCents);if(amount>0)await finance.Allocate(s,actor,receipt,due.Id,null,amount,c);
                }
                if(receipt.AllocatedCents!=beforeAllocated)await m.Save(s,receipt,c);
                bank.State="Reconciled";bank.Note="Recebimento conciliado no ambiente Sandbox.";intent.State="Received";
            }
            if(!intent.ReceiptIds.Contains(receipt.Id))intent.ReceiptIds.Add(receipt.Id);intent.ReceiptId=receipt.Id;await m.Save(s,intent,c);await m.Save(s,bank,c);
            await m.Notify(s,h,intent.MemberId,"pix-result:"+receipt.Id+":"+receipt.Revision,"Atualização do pagamento de teste","Consulte o resultado e a destinação no aplicativo. Ambiente de homologação.","/pagamentos","Finance",c);
            await m.Root.Audit.InsertOneAsync(s,new AuditEvent{HouseId=h,ActorId=actor.Id,Action="pix.sandbox.reconciled",ResourceId=bank.Id},cancellationToken:c);return true;
        },ct);
    }
    public static long ParseAmount(string text)
    {
        Rules.Require(System.Text.RegularExpressions.Regex.IsMatch(text,@"^[0-9]{1,11}\.[0-9]{2}$")&&decimal.TryParse(text,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out _),"pix_amount","Valor financeiro inválido.",409);return BusinessRules.Money(checked((long)(decimal.Parse(text,CultureInfo.InvariantCulture)*100m)));
    }
}

public sealed class PixWorker(ModuleStore m,EfiPixClient client,PixService service,ILogger<PixWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if(!client.Enabled)return;
        while(!ct.IsCancellationRequested)
        {
            try
            {
                var now=DateTime.UtcNow;var after=now.AddMonths(-6);var item=await m.Set<PaymentIntent>().FindOneAndUpdateAsync(x=>x.NextCheckAt<=now&&x.CreatedAt>=after&&(x.LeaseUntil==null||x.LeaseUntil<now),Builders<PaymentIntent>.Update.Set(x=>x.LeaseUntil,now.AddMinutes(2)),new FindOneAndUpdateOptions<PaymentIntent>{ReturnDocument=ReturnDocument.After},ct);
                if(item==null){await Task.Delay(5000,ct);continue;}
                try{await service.Synchronize(item,ct);}catch(Exception ex)when(ex is not OperationCanceledException||!ct.IsCancellationRequested){logger.LogWarning("Pix sandbox verification pending ({Type})",ex.GetType().Name);await m.Set<PaymentIntent>().UpdateOneAsync(x=>x.Id==item.Id,Builders<PaymentIntent>.Update.Set(x=>x.ErrorCode,"provider_unavailable"),cancellationToken:ct);}
                await m.Set<PaymentIntent>().UpdateOneAsync(x=>x.Id==item.Id&&x.LeaseUntil==item.LeaseUntil,Builders<PaymentIntent>.Update.Set(x=>x.NextCheckAt,DateTime.UtcNow.AddSeconds(item.CreatedAt<DateTime.UtcNow.AddDays(-1)?3600:30)).Set(x=>x.LeaseUntil,null),cancellationToken:ct);
            }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogWarning("Pix worker failed ({Type})",ex.GetType().Name);await Task.Delay(10000,ct);}
        }
    }
}
