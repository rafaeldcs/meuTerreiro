using System.Text.Json;
using MongoDB.Driver;
using Terreiro.Data;
using Terreiro.Domain;
namespace Terreiro.Service;

public sealed class OperationsService(ModuleStore m,FinanceService finance)
{
    public async Task<object> Command(User actor,string action,Command input,CancellationToken ct)
    {
        var permission=action.StartsWith("stock.")||action.StartsWith("purchase.")?"stock.write":action.StartsWith("budget.")||action.StartsWith("supplier.")?"finance.write":"office.write";
        if(action=="stock.review")permission="stock.approve";
        if(action is "purchase.review" or "budget.approve" or "supplier.review")permission="finance.approve";
        if(action is not "donation.promise" and not "task.complete" and not "announcement.ack" and not "donation.receive")Access.Demand(actor,permission);
        return await m.Execute<object>(actor,"operations."+action,input.OperationId,input,async(s,c)=>
        {
            var d=input.Data;var h=actor.HouseId;
            switch(action)
            {
                case "stock.item.create":
                {
                    var item=new Item{HouseId=h,Name=Rules.Text(Fields.Text(d,"name"),2,100,"Item"),Unit=Rules.Text(Fields.Text(d,"unit","un"),1,15,"Unidade"),Category=Rules.Text(Fields.Text(d,"category","Limpeza"),2,80,"Categoria"),MinimumMilli=BusinessRules.Quantity(Fields.Number(d,"minimumMilli"),true),Reusable=Fields.Bool(d,"reusable")};await m.Insert(s,item,c);return item;
                }
                case "stock.receive":
                {
                    var item=await m.Get<Item>(s,h,Fields.Required(d,"itemId"),c);var quantity=BusinessRules.Quantity(Fields.Number(d,"quantityMilli"));
                    var reason=Rules.Text(Fields.Text(d,"reason"),5,500,"Origem e motivo");
                    return await Receive(s,actor,item.Id,quantity,Fields.Text(d,"lot","Sem lote"),Fields.Text(d,"location","Almoxarifado"),Fields.Optional(d,"expiresOn"),"Manual",input.OperationId,reason,c);
                }
                case "stock.reserve":
                {
                    var lot=await m.Get<StockLot>(s,h,Fields.Required(d,"lotId"),c);BusinessRules.Revision(lot.Revision,input.Revision);var quantity=BusinessRules.Quantity(Fields.Number(d,"quantityMilli"));BusinessRules.Reserve(lot,quantity);
                    var activity=Fields.Required(d,"activityId");var kind=Fields.Text(d,"activityKind","Cleaning");await CheckActivity(s,actor,kind,activity,c);
                    var reservation=new Reservation{HouseId=h,LotId=lot.Id,ItemId=lot.ItemId,ActivityId=activity,ActivityKind=kind,QuantityMilli=quantity};
                    lot.ReservedMilli+=quantity;await m.Save(s,lot,c);await m.Insert(s,reservation,c);await StockLog(s,actor,lot,"Reserved",quantity,reservation.Id,"Reserva para atividade",c);return reservation;
                }
                case "stock.issue":
                {
                    var r=await m.Get<Reservation>(s,h,Fields.Required(d,"reservationId"),c);BusinessRules.Revision(r.Revision,input.Revision);Rules.Require(r.State=="Reserved","reservation_state","Reserva não está disponível para retirada.",409);
                    await CheckActivity(s,actor,r.ActivityKind,r.ActivityId,c);var lot=await m.Get<StockLot>(s,h,r.LotId,c);var remaining=r.QuantityMilli-r.IssuedMilli;
                    var quantity=BusinessRules.Quantity(Fields.Number(d,"quantityMilli"));Rules.Require(quantity<=remaining && lot.OnHandMilli>=quantity && lot.ReservedMilli>=quantity,"stock_balance","Retirada excede a reserva ou o estoque.",409);
                    Rules.Require(lot.ExpiresOn==null||string.CompareOrdinal(lot.ExpiresOn,BusinessRules.Today())>=0,"expired_stock","Lote vencido.",409);
                    lot.OnHandMilli-=quantity;lot.ReservedMilli-=quantity;r.IssuedMilli+=quantity;if(r.IssuedMilli==r.QuantityMilli)r.State="Issued";
                    await m.Save(s,lot,c);await m.Save(s,r,c);await StockLog(s,actor,lot,"Issued",quantity,r.Id,"Material entregue à atividade",c);return r;
                }
                case "stock.settle":
                {
                    var r=await m.Get<Reservation>(s,h,Fields.Required(d,"reservationId"),c);BusinessRules.Revision(r.Revision,input.Revision);Rules.Require(r.State is "Reserved" or "Issued" or "CancelledWithIssued","reservation_state","Retirada encerrada.",409);
                    var consumed=BusinessRules.Quantity(Fields.Number(d,"consumedMilli"),true);var returned=BusinessRules.Quantity(Fields.Number(d,"returnedMilli"),true);var lost=BusinessRules.Quantity(Fields.Number(d,"lostMilli"),true);
                    var unsettled=r.IssuedMilli-r.ConsumedMilli-r.ReturnedMilli-r.LostMilli;Rules.Require(consumed+returned+lost>0 && consumed+returned+lost<=unsettled,"settlement_balance","Consumo, perda e devolução excedem o material retirado ainda não prestado.",409);
                    var lot=await m.Get<StockLot>(s,h,r.LotId,c);var item=await m.Get<Item>(s,h,r.ItemId,c);Rules.Require(!item.Reusable||consumed==0,"reusable_consumption","Utensílio reutilizável deve ser devolvido ou ter perda identificada.");
                    var note=Rules.Text(Fields.Text(d,"reason"),3,500,"Conferência dos materiais");
                    if(returned>0){lot.OnHandMilli+=returned;await m.Save(s,lot,c);await StockLog(s,actor,lot,"Returned",returned,r.Id,note,c);}
                    if(consumed>0)await StockLog(s,actor,lot,"Consumed",consumed,r.Id,note,c);
                    if(lost>0)await StockLog(s,actor,lot,"Lost",lost,r.Id,note,c);
                    r.ConsumedMilli+=consumed;r.ReturnedMilli+=returned;r.LostMilli+=lost;
                    if(r.IssuedMilli==r.ConsumedMilli+r.ReturnedMilli+r.LostMilli && (r.IssuedMilli==r.QuantityMilli||r.State=="CancelledWithIssued"))r.State="Settled";
                    await m.Save(s,r,c);return r;
                }
                case "stock.release":
                {
                    var r=await m.Get<Reservation>(s,h,Fields.Required(d,"reservationId"),c);BusinessRules.Revision(r.Revision,input.Revision);Rules.Require(r.State is "Reserved" or "Issued","reservation_state","Reserva já liberada.",409);
                    var lot=await m.Get<StockLot>(s,h,r.LotId,c);var remaining=r.QuantityMilli-r.IssuedMilli;lot.ReservedMilli-=remaining;Rules.Require(lot.ReservedMilli>=0,"stock_integrity","Reserva inconsistente.",409);
                    await m.Save(s,lot,c);r.State=r.IssuedMilli==r.ConsumedMilli+r.ReturnedMilli+r.LostMilli?"Released":"CancelledWithIssued";await m.Save(s,r,c);await StockLog(s,actor,lot,"Released",remaining,r.Id,Rules.Text(Fields.Text(d,"reason"),3,500,"Motivo"),c);return r;
                }
                case "stock.adjust.request":
                {
                    var lot=await m.Get<StockLot>(s,h,Fields.Required(d,"lotId"),c);BusinessRules.Revision(lot.Revision,input.Revision);var counted=BusinessRules.Quantity(Fields.Number(d,"countedMilli"),true);Rules.Require(counted>=lot.ReservedMilli,"reserved_stock","Contagem abaixo da reserva. Resolva as reservas antes do ajuste.",409);
                    return await Approval(s,actor,"StockCount",lot.Id,lot.Revision,JsonSerializer.SerializeToElement(new{countedMilli=counted}),Rules.Text(Fields.Text(d,"reason"),5,500,"Justificativa"),"stock.approve",c);
                }
                case "stock.review":
                {
                    var a=await LoadApproval(s,actor,d,input.Revision,"StockCount",c);var lot=await m.Get<StockLot>(s,h,a.TargetId,c);BusinessRules.Revision(lot.Revision,a.TargetRevision);
                    if(Fields.Bool(d,"approve")){var p=JsonDocument.Parse(a.PayloadJson).RootElement;var count=BusinessRules.Quantity(Fields.Number(p,"countedMilli"),true);Rules.Require(count>=lot.ReservedMilli,"reserved_stock","Contagem inferior às reservas.",409);var delta=count-lot.OnHandMilli;lot.OnHandMilli=count;await m.Save(s,lot,c);await StockLog(s,actor,lot,"Adjustment",delta,a.Id,a.Reason,c);}
                    return await FinishApproval(s,actor,a,d,c);
                }
                case "supplier.create":
                {
                    var supplier=new Supplier{HouseId=h,Name=Rules.Text(Fields.Text(d,"name"),2,120,"Fornecedor"),Contact=Rules.Text(Fields.Text(d,"contact","Não informado"),2,200,"Contato"),Document=Rules.Text(Fields.Text(d,"document","Não informado"),2,40,"Documento")};await m.Insert(s,supplier,c);return supplier;
                }
                case "supplier.bank.request":
                {
                    var supplier=await m.Get<Supplier>(s,h,Fields.Required(d,"supplierId"),c);BusinessRules.Revision(supplier.Revision,input.Revision);var details=Rules.Text(Fields.Text(d,"paymentDetails"),5,300,"Dados de pagamento");return await Approval(s,actor,"SupplierBank",supplier.Id,supplier.Revision,JsonSerializer.SerializeToElement(new{paymentDetails=details}),Rules.Text(Fields.Text(d,"reason"),8,500,"Canal utilizado para verificar a alteração"),"finance.approve",c);
                }
                case "supplier.review":
                {
                    var a=await LoadApproval(s,actor,d,input.Revision,"SupplierBank",c);var supplier=await m.Get<Supplier>(s,h,a.TargetId,c);BusinessRules.Revision(supplier.Revision,a.TargetRevision);
                    if(Fields.Bool(d,"approve")){supplier.PaymentDetails=Fields.Text(JsonDocument.Parse(a.PayloadJson).RootElement,"paymentDetails");await m.Save(s,supplier,c);}return await FinishApproval(s,actor,a,d,c);
                }
                case "purchase.create":
                {
                    var supplier=await m.Get<Supplier>(s,h,Fields.Required(d,"supplierId"),c);Rules.Require(supplier.Active,"supplier_inactive","Fornecedor inativo.");
                    var rows=Fields.Rows(d,"lines");Rules.Require(rows.Length is >0 and <=50,"lines_invalid","Informe de 1 a 50 itens.");
                    var purchase=new Purchase{HouseId=h,Title=Rules.Text(Fields.Text(d,"title"),3,160,"Descrição"),SupplierId=supplier.Id,RequestedBy=actor.Id,DueDate=BusinessRules.Date(Fields.Text(d,"dueDate")),FreightCents=BusinessRules.Money(Fields.Number(d,"freightCents"),true),FundId=Fields.Optional(d,"fundId"),EventId=Fields.Optional(d,"eventId")};
                    if(purchase.FundId!=null)await m.Get<Fund>(s,h,purchase.FundId,c);if(purchase.EventId!=null)await m.Get<HouseEvent>(s,h,purchase.EventId,c);
                    foreach(var row in rows){var item=await m.Get<Item>(s,h,Fields.Required(row,"itemId"),c);purchase.Lines.Add(new PurchaseLine{ItemId=item.Id,QuantityMilli=BusinessRules.Quantity(Fields.Number(row,"quantityMilli")),TotalCents=BusinessRules.Money(Fields.Number(row,"totalCents"))});}
                    BusinessRules.Money(checked(purchase.FreightCents+purchase.Lines.Sum(x=>x.TotalCents)));await m.Insert(s,purchase,c);return purchase;
                }
                case "purchase.submit":
                {
                    var purchase=await m.Get<Purchase>(s,h,Fields.Required(d,"purchaseId"),c);BusinessRules.Revision(purchase.Revision,input.Revision);Rules.Require(purchase.State=="Draft","purchase_state","Pedido não está em rascunho.",409);
                    purchase.State="PendingApproval";await m.Save(s,purchase,c);return await Approval(s,actor,"Purchase",purchase.Id,purchase.Revision,JsonSerializer.SerializeToElement(new{}),"Aprovar compra: "+purchase.Title,"finance.approve",c);
                }
                case "purchase.review":
                {
                    var a=await LoadApproval(s,actor,d,input.Revision,"Purchase",c);var purchase=await m.Get<Purchase>(s,h,a.TargetId,c);BusinessRules.Revision(purchase.Revision,a.TargetRevision);Rules.Require(purchase.State=="PendingApproval","purchase_state","Pedido não aguarda aprovação.",409);
                    if(Fields.Bool(d,"approve"))
                    {
                        var supplier=await m.Get<Supplier>(s,h,purchase.SupplierId,c);BusinessRules.Independent(purchase.RequestedBy,actor.Id);
                        var expense=new Expense{HouseId=h,Title=purchase.Title,Category="Compras",AmountCents=purchase.FreightCents+purchase.Lines.Sum(x=>x.TotalCents),DueDate=purchase.DueDate,RequestedBy=purchase.RequestedBy,Beneficiary=supplier.Name,SupplierId=supplier.Id,FundId=purchase.FundId,EventId=purchase.EventId,PurchaseId=purchase.Id,State="Approved",ApprovedBy=actor.Id,ApprovalId=a.Id};
                        await m.Insert(s,expense,c);purchase.ExpenseId=expense.Id;purchase.ApprovedBy=actor.Id;purchase.State="Approved";
                    }else purchase.State="Rejected";
                    await m.Save(s,purchase,c);return await FinishApproval(s,actor,a,d,c);
                }
                case "purchase.receive":
                {
                    var purchase=await m.Get<Purchase>(s,h,Fields.Required(d,"purchaseId"),c);BusinessRules.Revision(purchase.Revision,input.Revision);Rules.Require(purchase.State is "Approved" or "PartiallyReceived","purchase_state","Pedido precisa estar aprovado e ainda ter material a receber.",409);
                    var line=purchase.Lines.FirstOrDefault(x=>x.Id==Fields.Text(d,"lineId"));Rules.Require(line!=null,"line_invalid","Item do pedido não encontrado.",404);var quantity=BusinessRules.Quantity(Fields.Number(d,"quantityMilli"));Rules.Require(quantity<=line.QuantityMilli-line.ReceivedMilli,"purchase_quantity","Recebimento excede a quantidade comprada.",409);
                    await Receive(s,actor,line.ItemId,quantity,Fields.Text(d,"lot","Sem lote"),Fields.Text(d,"location","Almoxarifado"),Fields.Optional(d,"expiresOn"),"Purchase",purchase.Id,"Recebimento de compra aprovada",c);line.ReceivedMilli+=quantity;purchase.State=purchase.Lines.All(x=>x.ReceivedMilli==x.QuantityMilli)?"Received":"PartiallyReceived";await m.Save(s,purchase,c);return purchase;
                }
                case "donation.promise":
                {
                    var kind=Fields.Text(d,"kind","Material");Rules.Require(kind is "Money" or "Material" or "Asset" or "Service","donation_kind","Tipo de doação inválido.");
                    var donation=new Donation{HouseId=h,Kind=kind,Description=Rules.Text(Fields.Text(d,"description"),3,300,"Descrição"),DonorId=actor.Id,DonorName=actor.Name,FundId=Fields.Optional(d,"fundId"),ItemId=Fields.Optional(d,"itemId"),QuantityMilli=BusinessRules.Quantity(Fields.Number(d,"quantityMilli"),true),EstimatedCents=BusinessRules.Money(Fields.Number(d,"estimatedCents"),true)};
                    if(kind=="Material"){Rules.Require(donation.ItemId!=null,"item_required","Selecione o material doado.");await m.Get<Item>(s,h,donation.ItemId,c);BusinessRules.Quantity(donation.QuantityMilli);}
                    if(donation.FundId!=null)await m.Get<Fund>(s,h,donation.FundId,c);await m.Insert(s,donation,c);return donation;
                }
                case "donation.receive":
                {
                    var donation=await m.Get<Donation>(s,h,Fields.Required(d,"donationId"),c);BusinessRules.Revision(donation.Revision,input.Revision);Rules.Require(donation.State is "Promised" or "PartiallyReceived","donation_state","Doação já recebida.",409);
                    if(donation.Kind=="Money")
                    {
                        Access.Demand(actor,"finance.write");var receipt=await m.Get<Receipt>(s,h,Fields.Required(d,"receiptId"),c);var amount=BusinessRules.Money(Fields.Number(d,"amountCents"));await finance.Allocate(s,actor,receipt,null,donation.FundId,amount,c);await m.Save(s,receipt,c);donation.ReceiptId=receipt.Id;donation.State="Received";
                    }
                    else if(donation.Kind=="Material")
                    {
                        Access.Demand(actor,"stock.write");var quantity=BusinessRules.Quantity(Fields.Number(d,"quantityMilli"));await Receive(s,actor,donation.ItemId!,quantity,Fields.Text(d,"lot","Doação"),Fields.Text(d,"location","Almoxarifado"),Fields.Optional(d,"expiresOn"),"Donation",donation.Id,"Material doado, sem entrada de dinheiro",c);donation.ReceivedMilli+=quantity;donation.State=donation.ReceivedMilli>=donation.QuantityMilli?"Received":"PartiallyReceived";
                    }
                    else if(donation.Kind=="Asset")
                    {
                        Access.Demand(actor,"office.write");
                        var asset=new Asset{HouseId=h,Name=donation.Description,Code="DOA-"+donation.Id[..8],Ownership="House",Location=Fields.Required(d,"location"),EstimatedCents=donation.EstimatedCents};await m.Insert(s,asset,c);donation.State="Received";
                    }
                    else { Access.Demand(actor,"office.write"); donation.State="Received"; }
                    await m.Save(s,donation,c);if(donation.DonorId!=null)await m.Notify(s,h,donation.DonorId,"donation:"+donation.Id+":"+donation.Revision,"Doação recebida","A casa registrou o recebimento. Obrigado pela contribuição.","/doacoes","Donation",c);return donation;
                }
                case "event.create":
                {
                    var start=ParseTime(d,"startsAt");var end=ParseTime(d,"endsAt");Rules.Require(end>start,"time_invalid","Horário final deve ser posterior ao inicial.");var ids=Fields.Ids(d,"participantIds").ToList();await finance.ValidateMembers(s,h,ids,c);
                    var item=new HouseEvent{HouseId=h,Title=Rules.Text(Fields.Text(d,"title"),3,160,"Evento"),Description=Rules.Text(Fields.Text(d,"description"),3,1000,"Descrição"),Location=Rules.Text(Fields.Text(d,"location"),2,160,"Local"),StartsAt=start,EndsAt=end,CoordinatorId=actor.Id,ParticipantIds=ids,BudgetCents=BusinessRules.Money(Fields.Number(d,"budgetCents"),true)};await m.Insert(s,item,c);await EventNotify(s,actor,item,"created",c);return item;
                }
                case "event.update":
                {
                    var item=await m.Get<HouseEvent>(s,h,Fields.Required(d,"eventId"),c);BusinessRules.Revision(item.Revision,input.Revision);Rules.Require(item.State=="Planned","event_state","Evento encerrado ou cancelado.",409);
                    var next=Fields.Text(d,"state","Planned");Rules.Require(next is "Planned" or "Cancelled" or "Completed","event_state","Estado inválido.");
                    var linked=await m.Root.Cleanings.Find(s,x=>x.HouseId==h && x.EventId==item.Id && x.Status=="Published").ToListAsync(c);
                    if(next=="Planned")
                    {
                        var start=ParseTime(d,"startsAt");var end=ParseTime(d,"endsAt");Rules.Require(end>start,"time_invalid","Intervalo inválido.");Rules.Require(linked.Count==0 || Fields.Bool(d,"reviewLinkedCleanings"),"cleaning_review","Confirme a revisão das limpezas vinculadas antes de alterar o evento.");item.StartsAt=start;item.EndsAt=end;
                        foreach(var cleaning in linked){cleaning.PublicationHistory.Add(JsonSerializer.Serialize(new{cleaning.PublicationVersion,cleaning.StartsAt,cleaning.EndsAt,cleaning.Assignments}));cleaning.PublicationVersion++;cleaning.NeedsScheduleReview=true;cleaning.Revision++;foreach(var a in cleaning.Assignments)a.Response="Pending";await m.Root.Cleanings.ReplaceOneAsync(s,x=>x.Id==cleaning.Id && x.HouseId==h,cleaning,cancellationToken:c);}
                    }
                    if(next=="Cancelled")Rules.Require(linked.Count==0,"linked_cleanings","Cancele ou desvincule as limpezas ativas antes de cancelar o evento; preservamos o que já aconteceu.",409);
                    item.State=next;await m.Save(s,item,c);await EventNotify(s,actor,item,"updated:"+item.Revision,c);return item;
                }
                case "asset.create":
                {
                    var ownership=Fields.Text(d,"ownership","House");Rules.Require(ownership is "House" or "Borrowed","ownership","Titularidade inválida.");
                    var asset=new Asset{HouseId=h,Name=Rules.Text(Fields.Text(d,"name"),2,120,"Bem"),Code=Rules.Text(Fields.Text(d,"code"),2,50,"Identificação"),Location=Fields.Required(d,"location"),Ownership=ownership,EstimatedCents=BusinessRules.Money(Fields.Number(d,"estimatedCents"),true)};await m.Insert(s,asset,c);return asset;
                }
                case "asset.move":
                {
                    var asset=await m.Get<Asset>(s,h,Fields.Required(d,"assetId"),c);BusinessRules.Revision(asset.Revision,input.Revision);var kind=Fields.Text(d,"kind");
                    Rules.Text(Fields.Text(d,"reason"),3,500,"Motivo");
                    if(kind=="Loan"){Rules.Require(asset.State=="Available","asset_state","Bem indisponível.",409);var user=Fields.Required(d,"memberId");await finance.ValidateMembers(s,h,[user],c);asset.State="Loaned";asset.LoanedTo=user;asset.ReturnDue=BusinessRules.Date(Fields.Text(d,"returnDue"));}
                    else if(kind=="Return"){Rules.Require(asset.State=="Loaned","asset_state","Bem não está emprestado.",409);asset.State="Available";asset.LoanedTo=null;asset.ReturnDue=null;asset.Condition=Fields.Required(d,"condition");asset.Location=Fields.Required(d,"location");}
                    else if(kind=="Relocate"){Rules.Require(asset.State=="Available","asset_state","Registre primeiro a devolução do bem.",409);asset.Location=Fields.Required(d,"location");}
                    else throw new RuleException("action_invalid","Movimentação inválida.");await m.Save(s,asset,c);return asset;
                }
                case "maintenance.create":
                {
                    var item=new Maintenance{HouseId=h,AssetId=Fields.Optional(d,"assetId"),Title=Rules.Text(Fields.Text(d,"title"),3,160,"Manutenção"),DueDate=BusinessRules.Date(Fields.Text(d,"dueDate")),ResponsibleId=Fields.Required(d,"responsibleId")};if(item.AssetId!=null)await m.Get<Asset>(s,h,item.AssetId,c);await finance.ValidateMembers(s,h,[item.ResponsibleId],c);await m.Insert(s,item,c);return item;
                }
                case "maintenance.complete":
                {
                    var item=await m.Get<Maintenance>(s,h,Fields.Required(d,"maintenanceId"),c);BusinessRules.Revision(item.Revision,input.Revision);Rules.Require(item.State=="Open","maintenance_state","Manutenção já encerrada.",409);item.State="Completed";item.Note=Rules.Text(Fields.Text(d,"note"),5,1000,"Serviço realizado");item.ExpenseId=Fields.Optional(d,"expenseId");if(item.ExpenseId!=null)await m.Get<Expense>(s,h,item.ExpenseId,c);await m.Save(s,item,c);return item;
                }
                case "task.create":
                {
                    var item=new AdministrativeTask{HouseId=h,Title=Rules.Text(Fields.Text(d,"title"),3,160,"Tarefa"),Description=Rules.Text(Fields.Text(d,"description"),3,1000,"Descrição"),DueDate=BusinessRules.Date(Fields.Text(d,"dueDate")),ResponsibleId=Fields.Required(d,"responsibleId"),EventId=Fields.Optional(d,"eventId")};await finance.ValidateMembers(s,h,[item.ResponsibleId],c);if(item.EventId!=null)await m.Get<HouseEvent>(s,h,item.EventId,c);await m.Insert(s,item,c);await m.Notify(s,h,item.ResponsibleId,"task:"+item.Id,"Nova tarefa","Uma tarefa foi atribuída a você.","/tarefas","Office",c);return item;
                }
                case "task.complete":
                {
                    var item=await m.Get<AdministrativeTask>(s,h,Fields.Required(d,"taskId"),c);BusinessRules.Revision(item.Revision,input.Revision);Rules.Require(item.ResponsibleId==actor.Id||Access.Has(actor,"office.write"),"forbidden","Esta tarefa pertence a outra pessoa.",403);Rules.Require(item.State=="Open","task_state","Tarefa já encerrada.",409);item.State="Completed";await m.Save(s,item,c);return item;
                }
                case "document.create":
                {
                    var evidence=await m.Get<Evidence>(s,h,Fields.Required(d,"evidenceId"),c);Rules.Require(evidence.Purpose=="Document" && evidence.ScanState=="Clean","document_security","Envie um documento e aguarde a liberação de segurança.",409);var review=Fields.Optional(d,"reviewDate");if(review!=null)BusinessRules.Date(review);
                    var item=new DocumentRecord{HouseId=h,Title=Rules.Text(Fields.Text(d,"title"),3,160,"Documento"),Category=Rules.Text(Fields.Text(d,"category","Institucional"),2,80,"Categoria"),EvidenceId=evidence.Id,ResponsibleId=Fields.Required(d,"responsibleId"),ReviewDate=review};await finance.ValidateMembers(s,h,[item.ResponsibleId],c);await m.Insert(s,item,c);return item;
                }
                case "budget.create":
                {
                    var budget=new Budget{HouseId=h,Period=BusinessRules.Month(Fields.Text(d,"period")),Category=Rules.Text(Fields.Text(d,"category"),2,80,"Categoria"),PlannedIncomeCents=BusinessRules.Money(Fields.Number(d,"plannedIncomeCents"),true),PlannedExpenseCents=BusinessRules.Money(Fields.Number(d,"plannedExpenseCents"),true),CreatedBy=actor.Id};await m.Insert(s,budget,c);return budget;
                }
                case "budget.approve":
                {
                    var budget=await m.Get<Budget>(s,h,Fields.Required(d,"budgetId"),c);BusinessRules.Revision(budget.Revision,input.Revision);BusinessRules.Independent(budget.CreatedBy,actor.Id);Rules.Require(budget.State=="Draft","budget_state","Orçamento já revisado.",409);budget.ApprovedBy=actor.Id;budget.State="Approved";await m.Save(s,budget,c);return budget;
                }
                case "announcement.publish":
                {
                    Access.Demand(actor,"communications.publish");var ids=Fields.Ids(d,"recipientIds");if(Fields.Bool(d,"allMembers"))ids=(await m.Root.Users.Find(s,x=>x.HouseId==h && x.Active && x.IsMember).ToListAsync(c)).Select(x=>x.Id).ToArray();
                    Rules.Require(ids.Length is >0 and <=500,"recipients","Escolha um público de 1 a 500 membros.");await finance.ValidateMembers(s,h,ids,c);
                    var a=new Announcement{HouseId=h,Title=Rules.Text(Fields.Text(d,"title"),3,120,"Título"),Body=Rules.Text(Fields.Text(d,"body"),5,4000,"Mensagem"),AuthorId=actor.Id,RecipientIds=ids.ToList(),RequireAcknowledgement=Fields.Bool(d,"requireAcknowledgement")};await m.Insert(s,a,c);
                    foreach(var id in ids)await m.Notify(s,h,id,"announcement:"+a.Id,"Novo comunicado","Há um comunicado da casa no aplicativo.","/comunicados","Announcement",c);return a;
                }
                case "announcement.ack":
                {
                    var a=await m.Get<Announcement>(s,h,Fields.Required(d,"announcementId"),c);Rules.Require(a.RecipientIds.Contains(actor.Id),"not_found","Comunicado não encontrado.",404);Rules.Require(a.State=="Published"&&a.RequireAcknowledgement && a.PublicationVersion==Fields.Number(d,"version"),"announcement_version","Confirme a versão atual do comunicado.",409);
                    var id=Rules.Hash(h+"|ack|"+a.Id+"|"+a.PublicationVersion+"|"+actor.Id);var existing=await m.Set<Acknowledgement>().Find(s,x=>x.Id==id).FirstOrDefaultAsync(c);if(existing!=null)return existing;
                    var ack=new Acknowledgement{Id=id,HouseId=h,AnnouncementId=a.Id,UserId=actor.Id,PublicationVersion=a.PublicationVersion};await m.Insert(s,ack,c);return ack;
                }
                default:throw new RuleException("action_unknown","Operação administrativa desconhecida.",404);
            }
        },ct);
    }
    public async Task<StockLot> Receive(IClientSessionHandle s,User actor,string itemId,long quantity,string batch,string location,string? expires,string kind,string source,string reason,CancellationToken ct)
    {
        var item=await m.Get<Item>(s,actor.HouseId,itemId,ct);Rules.Require(item.Active,"item_inactive","Item inativo.");BusinessRules.Quantity(quantity);batch=Rules.Text(batch,1,100,"Lote");location=Rules.Text(location,1,100,"Local");if(expires!=null)BusinessRules.Date(expires);
        var id=Rules.Hash(actor.HouseId+"|lot|"+itemId+"|"+batch+"|"+location+"|"+expires)[..32];var lot=await m.Set<StockLot>().Find(s,x=>x.Id==id && x.HouseId==actor.HouseId).FirstOrDefaultAsync(ct);
        if(lot==null){lot=new StockLot{Id=id,HouseId=actor.HouseId,ItemId=itemId,Lot=batch,Location=location,ExpiresOn=expires,OnHandMilli=quantity};await m.Insert(s,lot,ct);}else{lot.OnHandMilli=checked(lot.OnHandMilli+quantity);await m.Save(s,lot,ct);}
        await StockLog(s,actor,lot,"Received"+kind,quantity,source,reason,ct);return lot;
    }
    private Task StockLog(IClientSessionHandle s,User actor,StockLot lot,string kind,long quantity,string source,string reason,CancellationToken ct) =>
        m.Insert(s,new StockMovement{HouseId=actor.HouseId,ItemId=lot.ItemId,LotId=lot.Id,Kind=kind,QuantityMilli=quantity,SourceId=source,Reason=reason,ActorId=actor.Id},ct);
    private async Task CheckActivity(IClientSessionHandle s,User actor,string kind,string id,CancellationToken ct)
    {
        if(kind=="Cleaning"){var cleaning=await m.Root.Cleanings.Find(s,x=>x.Id==id && x.HouseId==actor.HouseId).FirstOrDefaultAsync(ct);Rules.Require(cleaning!=null && cleaning.Status=="Published","activity_closed","Limpeza não está ativa.",409);}
        else if(kind=="Event"){var e=await m.Get<HouseEvent>(s,actor.HouseId,id,ct);Rules.Require(e.State=="Planned","activity_closed","Evento não está ativo.",409);}
        else if(kind!="Routine")throw new RuleException("activity_kind","Tipo de atividade inválido.");
    }
    private async Task<Approval> Approval(IClientSessionHandle s,User actor,string type,string target,int version,JsonElement payload,string reason,string permission,CancellationToken ct)
    {
        var json=payload.GetRawText();var a=new Approval{HouseId=actor.HouseId,Type=type,TargetId=target,TargetRevision=version,PayloadJson=json,PayloadHash=Rules.Hash(json),RequestedBy=actor.Id,Reason=reason};await m.Insert(s,a,ct);
        var users=await m.Root.Users.Find(s,x=>x.HouseId==actor.HouseId && x.Active && x.Permissions.Contains(permission) && x.Id!=actor.Id).ToListAsync(ct);foreach(var u in users)await m.Notify(s,actor.HouseId,u.Id,"ops-approval:"+a.Id,"Revisão pendente","Uma solicitação aguarda sua revisão.","/aprovacoes","Operations",ct);return a;
    }
    private async Task<Approval> LoadApproval(IClientSessionHandle s,User actor,JsonElement d,int revision,string type,CancellationToken ct)
    {
        var a=await m.Get<Approval>(s,actor.HouseId,Fields.Required(d,"approvalId"),ct);BusinessRules.Revision(a.Revision,revision);BusinessRules.Independent(a.RequestedBy,actor.Id);BusinessRules.ApprovalPayload(a);Rules.Require(a.Type==type && a.State=="Pending","approval_state","Solicitação incompatível ou já revisada.",409);return a;
    }
    private async Task<Approval> FinishApproval(IClientSessionHandle s,User actor,Approval a,JsonElement d,CancellationToken ct)
    {
        a.State=Fields.Bool(d,"approve")?"Approved":"Rejected";a.ReviewedBy=actor.Id;a.ReviewedAt=DateTime.UtcNow;a.ReviewNote=Rules.Text(Fields.Text(d,"note"),3,500,"Parecer");await m.Save(s,a,ct);await m.Notify(s,actor.HouseId,a.RequestedBy,"ops-reviewed:"+a.Id,"Solicitação revisada","Confira o parecer no aplicativo.","/aprovacoes","Operations",ct);return a;
    }
    private static DateTime ParseTime(JsonElement d,string name)
    {
        Rules.Require(DateTimeOffset.TryParse(Fields.Text(d,name),out var time),"time_invalid","Data/hora inválida.");return time.UtcDateTime;
    }
    private async Task EventNotify(IClientSessionHandle s,User actor,HouseEvent item,string suffix,CancellationToken ct)
    {
        foreach(var id in item.ParticipantIds)await m.Notify(s,actor.HouseId,id,"event:"+item.Id+":"+suffix,"Atualização de evento","Confira as informações atualizadas do evento.","/eventos","Event",ct);
    }
}
