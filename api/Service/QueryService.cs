using MongoDB.Driver;
using Terreiro.Data;
using Terreiro.Domain;
namespace Terreiro.Service;

public sealed class QueryService(ModuleStore m)
{
    private async Task<object> Page<T>(User actor,int page,int size,CancellationToken ct,FilterDefinition<T>? extra=null,Func<T,object>? project=null) where T:Entity
    {
        page=Math.Clamp(page,1,100000);size=Math.Clamp(size,1,100);var filter=Builders<T>.Filter.Eq(x=>x.HouseId,actor.HouseId);if(extra!=null)filter&=extra;
        var total=await m.Set<T>().CountDocumentsAsync(filter,cancellationToken:ct);var rows=await m.Set<T>().Find(filter).SortByDescending(x=>x.CreatedAt).Skip((page-1)*size).Limit(size).ToListAsync(ct);
        return new{items=rows.Select(x=>project==null?(object)x:project(x)),total,page,pageSize=size};
    }
    public async Task<object> List(User actor,string resource,int page,int size,CancellationToken ct)
    {
        var finance=Access.Has(actor,"finance.read");var stock=Access.Has(actor,"stock.read")||Access.Has(actor,"stock.write");var office=Access.Has(actor,"office.read");
        switch(resource)
        {
            case "bank-observations":Access.Demand(actor,"finance.read");return await Page<BankObservation>(actor,page,size,ct);
            case "access-changes":Access.Demand(actor,"security.manage");return await Page<AccessChange>(actor,page,size,ct);
            case "payment-intents":return await Page<PaymentIntent>(actor,page,size,ct,finance?null:Builders<PaymentIntent>.Filter.Eq(x=>x.MemberId,actor.Id));
            case "dues":return await Page<Due>(actor,page,size,ct,finance?null:Builders<Due>.Filter.Eq(x=>x.MemberId,actor.Id));
            case "member-exemptions":Rules.Require(Access.Finance(actor)||Access.Has(actor,"finance.write")||Access.Has(actor,"finance.approve"),"forbidden","Consulta restrita ao financeiro.",403);return await Page<MemberExemption>(actor,page,size,ct);
            case "rules":Access.Demand(actor,"finance.read");return await Page<ContributionRule>(actor,page,size,ct);
            case "accounts":Access.Demand(actor,"finance.read");return await Page<Account>(actor,page,size,ct);
            case "funds":return await Page<Fund>(actor,page,size,ct,project:x=>finance?(object)x:new{x.Id,x.Name,x.Purpose,x.GoalCents,x.Restricted,x.Active});
            case "receipts":Access.Demand(actor,"finance.read");return await Page<Receipt>(actor,page,size,ct);
            case "allocations":return await Page<Allocation>(actor,page,size,ct,finance?null:Builders<Allocation>.Filter.Eq(x=>x.MemberId,actor.Id));
            case "ledger":Access.Demand(actor,"finance.read");return await Page<LedgerEntry>(actor,page,size,ct);
            case "expenses":return await Page<Expense>(actor,page,size,ct,finance?null:Builders<Expense>.Filter.Eq(x=>x.RequestedBy,actor.Id));
            case "refunds":Access.Demand(actor,"finance.read");return await Page<Refund>(actor,page,size,ct);
            case "cash":Access.Demand(actor,"finance.read");return await Page<CashSession>(actor,page,size,ct);
            case "closings":Access.Demand(actor,"finance.read");return await Page<Closing>(actor,page,size,ct,project:x=>new{x.Id,x.Revision,x.Period,x.State,x.PreparedBy,x.ReviewedBy,x.Note,x.CreatedAt});
            case "statements":Access.Demand(actor,"finance.read");return await Page<StatementLine>(actor,page,size,ct);
            case "statement-batches":Access.Demand(actor,"finance.read");return await Page<StatementBatch>(actor,page,size,ct,project:x=>new{x.Id,x.AccountId,x.ImportedCount,x.SkippedCount,x.OpeningCents,x.ClosingCents,x.CreatedAt});
            case "evidence":return await Page<Evidence>(actor,page,size,ct,finance?Builders<Evidence>.Filter.Ne(x=>x.Purpose,"Document"):Builders<Evidence>.Filter.Eq(x=>x.MemberId,actor.Id)&Builders<Evidence>.Filter.Ne(x=>x.Purpose,"Document"),x=>new{x.Id,x.Revision,x.OriginalName,x.ContentType,x.Length,x.DeclaredCents,x.DeclaredDate,x.ScanState,x.AnalysisState,x.AnalysisNote,x.ReviewState,x.ReceiptId,x.CreatedAt,possibleDuplicate=x.DuplicateOf!=null,extractedAmount=x.ExtractedAmount,extractedReference=x.ExtractedReference,x.PossibleScheduled});
            case "document-files":Access.Demand(actor,"office.read");return await Page<Evidence>(actor,page,size,ct,Builders<Evidence>.Filter.Eq(x=>x.Purpose,"Document"),x=>new{x.Id,x.Revision,x.OriginalName,x.ScanState,x.AnalysisState,x.CreatedAt});
            case "reviews":return await Page<ReviewCase>(actor,page,size,ct,finance?null:Builders<ReviewCase>.Filter.Eq(x=>x.MemberId,actor.Id),x=>new{x.Id,x.Revision,x.EvidenceId,x.MemberId,x.ReasonCode,x.State,x.Resolution,messages=x.Messages.Where(y=>finance||!y.Internal),x.CreatedAt});
            case "approvals":
            {
                var filter=Builders<Approval>.Filter.Eq(x=>x.RequestedBy,actor.Id);
                if(Access.Has(actor,"finance.approve"))filter|=Builders<Approval>.Filter.Ne(x=>x.Type,"StockCount");
                if(Access.Has(actor,"stock.approve"))filter|=Builders<Approval>.Filter.Eq(x=>x.Type,"StockCount");
                return await Page<Approval>(actor,page,size,ct,filter);
            }
            case "items":return await Page<Item>(actor,page,size,ct,project:x=>stock?(object)x:new{x.Id,x.Name,x.Unit,x.Category,x.Active});
            case "lots":Rules.Require(stock,"forbidden","Estoque restrito à equipe responsável.",403);return await Page<StockLot>(actor,page,size,ct);
            case "stock-movements":Rules.Require(stock,"forbidden","Estoque restrito.",403);return await Page<StockMovement>(actor,page,size,ct);
            case "reservations":Rules.Require(stock,"forbidden","Reservas restritas ao estoque.",403);return await Page<Reservation>(actor,page,size,ct);
            case "purchases":Rules.Require(stock||finance,"forbidden","Compras restritas.",403);return await Page<Purchase>(actor,page,size,ct);
            case "suppliers":Rules.Require(stock||finance,"forbidden","Fornecedores restritos.",403);return await Page<Supplier>(actor,page,size,ct,project:x=>finance?(object)x:new{x.Id,x.Name,x.Active});
            case "donations":return await Page<Donation>(actor,page,size,ct,office||finance||stock?null:Builders<Donation>.Filter.Eq(x=>x.DonorId,actor.Id));
            case "events":return await Page<HouseEvent>(actor,page,size,ct,office?null:Builders<HouseEvent>.Filter.AnyEq(x=>x.ParticipantIds,actor.Id),x=>office?(object)x:new{x.Id,x.Revision,x.Title,x.Description,x.Location,x.StartsAt,x.EndsAt,x.State});
            case "assets":Access.Demand(actor,"office.read");return await Page<Asset>(actor,page,size,ct);
            case "maintenance":Access.Demand(actor,"office.read");return await Page<Maintenance>(actor,page,size,ct);
            case "tasks":return await Page<AdministrativeTask>(actor,page,size,ct,office?null:Builders<AdministrativeTask>.Filter.Eq(x=>x.ResponsibleId,actor.Id));
            case "documents":Access.Demand(actor,"office.read");return await Page<DocumentRecord>(actor,page,size,ct);
            case "budgets":Access.Demand(actor,"finance.read");return await Page<Budget>(actor,page,size,ct);
            case "announcements":return await Page<Announcement>(actor,page,size,ct,Access.Has(actor,"communications.publish")?null:Builders<Announcement>.Filter.AnyEq(x=>x.RecipientIds,actor.Id),x=>new{x.Id,x.Revision,x.Title,x.Body,x.State,x.PublicationVersion,x.RequireAcknowledgement,x.CreatedAt});
            case "acknowledgements":return await Page<Acknowledgement>(actor,page,size,ct,Builders<Acknowledgement>.Filter.Eq(x=>x.UserId,actor.Id));
            case "swaps":return await Page<CleaningSwap>(actor,page,size,ct,Roles.Coordinates(actor)?null:Builders<CleaningSwap>.Filter.Eq(x=>x.OriginalId,actor.Id)|Builders<CleaningSwap>.Filter.Eq(x=>x.SubstituteId,actor.Id));
            case "series":Rules.Require(Roles.Coordinates(actor),"forbidden","Acesso da coordenação.",403);return await Page<CleaningSeries>(actor,page,size,ct,actor.Role==Roles.Admin?null:Builders<CleaningSeries>.Filter.Eq(x=>x.CoordinatorId,actor.Id));
            default:throw new RuleException("resource_unknown","Área não encontrada.",404);
        }
    }
    // Dedicated list: filters run in MongoDB BEFORE pagination; no page-local search.
    public async Task<object> Dues(User actor,int page,int size,int? year,int? month,string? state,string? query,CancellationToken ct)
    {
        var status=string.IsNullOrWhiteSpace(state)?"all":state.Trim();var q=(query??"").Trim();
        DueRules.ValidateFilters(year,month,status,q);page=Math.Clamp(page,1,100000);size=Math.Clamp(size,1,100);
        var b=Builders<Due>.Filter;
        var scope=b.Eq(x=>x.HouseId,actor.HouseId);
        if(!Access.Finance(actor))scope &= b.Eq(x=>x.MemberId,actor.Id);
        var filter=scope;
        if(year.HasValue && month.HasValue)filter &= b.Eq(x=>x.Competence,$"{year.Value:D4}-{month.Value:D2}");
        else if(year.HasValue)filter &= b.Regex(x=>x.Competence,new MongoDB.Bson.BsonRegularExpression($"^{year.Value:D4}-"));
        else if(month.HasValue)filter &= b.Regex(x=>x.Competence,new MongoDB.Bson.BsonRegularExpression($"^[0-9]{{4}}-{month.Value:D2}$"));
        // Persistence uses default (PascalCase) BSON member names; BalanceCents is computed, not stored.
        var balance=new MongoDB.Bson.BsonDocument("$subtract",new MongoDB.Bson.BsonArray{
            new MongoDB.Bson.BsonDocument("$add",new MongoDB.Bson.BsonArray{"$AmountCents","$AdjustmentCents"}),"$PaidCents"});
        FilterDefinition<Due> zero=new MongoDB.Bson.BsonDocument("$expr",new MongoDB.Bson.BsonDocument("$eq",new MongoDB.Bson.BsonArray{balance,0}));
        FilterDefinition<Due> positive=new MongoDB.Bson.BsonDocument("$expr",new MongoDB.Bson.BsonDocument("$gt",new MongoDB.Bson.BsonArray{balance,0}));
        filter &= status switch
        {
            "paid" => b.Eq(x=>x.Cancelled,false)&zero&(b.Eq(x=>x.Exempt,false)|b.Eq(x=>x.PaidCents,0)),
            "exempt" => b.Eq(x=>x.Exempt,true)&b.Eq(x=>x.Cancelled,false),
            "pending" => b.Eq(x=>x.Cancelled,false)&positive,
            "partial" => b.Eq(x=>x.Cancelled,false)&b.Eq(x=>x.Exempt,false)&b.Gt(x=>x.PaidCents,0)&positive,
            "cancelled" => b.Eq(x=>x.Cancelled,true),
            _ => b.Empty
        };
        if(q.Length>0)
        {
            var regex=new MongoDB.Bson.BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(q),"i");
            var names=Builders<User>.Filter.Eq(x=>x.HouseId,actor.HouseId)&Builders<User>.Filter.Regex(x=>x.Name,regex);
            if(!Access.Finance(actor))names &= Builders<User>.Filter.Eq(x=>x.Id,actor.Id);
            var ids=await m.Root.Users.Find(names).Project(x=>x.Id).ToListAsync(ct);
            filter &= b.Regex(x=>x.Competence,regex)|b.In(x=>x.MemberId,ids);
        }
        var total=await m.Set<Due>().CountDocumentsAsync(filter,cancellationToken:ct);
        var rows=await m.Set<Due>().Find(filter).SortByDescending(x=>x.Competence).ThenBy(x=>x.MemberId).ThenBy(x=>x.Id).Skip((page-1)*size).Limit(size).ToListAsync(ct);
        var memberIds=rows.Select(x=>x.MemberId).Distinct().ToArray();
        var people=await m.Root.Users.Find(x=>x.HouseId==actor.HouseId&&memberIds.Contains(x.Id)).ToListAsync(ct);
        var namesById=people.ToDictionary(x=>x.Id,x=>x.Name);
        // Years are scoped to the authorized history, independent of the current page/filter.
        using var periods=await m.Set<Due>().DistinctAsync(x=>x.Competence,scope,cancellationToken:ct);
        var allPeriods=await periods.ToListAsync(ct);
        var years=allPeriods.Where(x=>x.Length==7).Select(x=>x[..4]).Distinct().OrderByDescending(x=>x).ToArray();
        return new{items=rows.Select(x=>new{
            x.Id,x.Revision,x.MemberId,memberName=namesById.GetValueOrDefault(x.MemberId,"Membro cadastrado"),
            x.Competence,x.DueDate,x.AmountCents,x.AdjustmentCents,x.PaidCents,x.BalanceCents,x.Exempt,x.Cancelled,x.State,
            displayState=DueRules.DisplayState(x)}),total,page,pageSize=size,years,currentPeriod=BusinessRules.Today()[..7]};
    }

    public Task<object> DueDetails(User actor,string id,CancellationToken ct) => m.Root.Transaction<object>(async(s,c)=>
    {
        var due=await m.Get<Due>(s,actor.HouseId,id,c);
        Rules.Require(DueRules.CanRead(actor,due),"forbidden","Mensalidade não disponível para esta conta.",403);
        var member=await m.Root.Users.Find(s,x=>x.HouseId==actor.HouseId&&x.Id==due.MemberId).FirstOrDefaultAsync(c);
        var allocations=await m.Set<Allocation>().Find(s,x=>x.HouseId==actor.HouseId&&x.DueId==due.Id).SortBy(x=>x.CreatedAt).ToListAsync(c);
        var receiptIds=allocations.Select(x=>x.ReceiptId).Distinct().ToArray();
        var receipts=await m.Set<Receipt>().Find(s,x=>x.HouseId==actor.HouseId&&receiptIds.Contains(x.Id)).ToListAsync(c);
        var byReceipt=receipts.ToDictionary(x=>x.Id);
        var approvals=await m.Set<Approval>().Find(s,x=>x.HouseId==actor.HouseId&&x.Type=="DueAdjustment"&&x.TargetId==due.Id).SortByDescending(x=>x.CreatedAt).ToListAsync(c);
        // Legacy adjustments already kept their reason in the approval; no migration or invented motive.
        Approval? legacy=null;
        if(due.Exempt && due.ExemptionReason==null)
            foreach(var a in approvals.Where(x=>x.State=="Approved").OrderByDescending(x=>x.ReviewedAt))
            {
                using var payload=System.Text.Json.JsonDocument.Parse(a.PayloadJson);
                if(Fields.Bool(payload.RootElement,"exempt")){legacy=a;break;}
            }
        var approvedBy=due.ExemptionApprovedBy??legacy?.ReviewedBy;
        var approver=approvedBy==null?null:await m.Root.Users.Find(s,x=>x.HouseId==actor.HouseId&&x.Id==approvedBy).FirstOrDefaultAsync(c);
        return new{
            due,memberName=member?.Name??"Membro cadastrado",displayState=DueRules.DisplayState(due),
            exemption=due.Exempt?new{origin=due.MemberExemptionId!=null?"Member":"Individual",memberExemptionId=due.MemberExemptionId,reason=due.ExemptionReason??legacy?.Reason,approvedAt=due.ExemptionApprovedAt??legacy?.ReviewedAt,
                approvalId=due.ExemptionApprovalId??legacy?.Id,approvedByName=Access.Finance(actor)?approver?.Name:null}:null,
            // Expose only the amount allocated to this due, never other members or the whole grouped payment.
            payments=allocations.Select(x=>{
                byReceipt.TryGetValue(x.ReceiptId,out var receipt);
                return new{x.Id,x.ReceiptId,x.CreatedAt,x.AmountCents,x.ReversedCents,
                    netCents=x.AmountCents-x.ReversedCents,occurredOn=receipt?.OccurredOn,
                    verification=receipt?.Verification,financialReference=Access.Finance(actor)?receipt?.FinancialReference:null,
                    sourceAvailable=receipt!=null};
            }),
            adjustments=approvals.Select(x=>new{x.Id,x.State,x.Reason,x.CreatedAt,x.ReviewedAt,
                current=x.TargetRevision==due.Revision&&x.State=="Pending"})
        };
    },ct);

    public Task<object> MemberExemptions(User actor,string memberId,CancellationToken ct) => m.Root.Transaction<object>(async(s,c)=>
    {
        var staff=Access.Finance(actor)||Access.Has(actor,"finance.write")||Access.Has(actor,"finance.approve");
        Rules.Require(staff||actor.Id==memberId,"forbidden","Isenções disponíveis apenas ao próprio médium e à equipe financeira autorizada.",403);
        var member=await m.Root.Users.Find(s,x=>x.HouseId==actor.HouseId&&x.Id==memberId).FirstOrDefaultAsync(c);
        Rules.Require(member!=null,"not_found","Médium não encontrado nesta casa.",404);
        var rules=await m.Set<MemberExemption>().Find(s,x=>x.HouseId==actor.HouseId&&x.MemberId==memberId).SortByDescending(x=>x.EffectiveFrom).ThenByDescending(x=>x.CreatedAt).ToListAsync(c);
        var ruleIds=rules.Select(x=>x.Id).ToArray();
        var endings=await m.Set<Approval>().Find(s,x=>x.HouseId==actor.HouseId&&x.Type=="EndMemberExemption"&&ruleIds.Contains(x.TargetId)).ToListAsync(c);
        var currentPeriod=BusinessRules.Today()[..7];
        var current=MemberExemptionRules.Select(rules,actor.HouseId,memberId,currentPeriod);
        return new{memberId=member.Id,memberName=member.Name,isMember=member.IsMember,memberActive=member.Active,currentPeriod,currentId=current?.Id,
            rules=rules.Select(x=>new{x.Id,x.Revision,x.MemberId,x.Reason,x.EffectiveFrom,x.EffectiveTo,x.State,
                requestedBy=staff?x.RequestedBy:null,x.ApprovedAt,x.EndedFrom,x.EndReason,x.EndedAt,x.AppliedExistingCount,x.RestoredFutureCount,x.Exceptions,
                pendingEnd=endings.Any(a=>a.TargetId==x.Id&&a.State=="Pending"),
                endRequests=endings.Where(a=>a.TargetId==x.Id).Select(a=>new{a.Id,a.State,a.Reason,a.CreatedAt,a.ReviewedAt}),
                activeNow=x.Id==current?.Id})};
    },ct);

    public async Task<object> Dashboard(User actor,CancellationToken ct)
    {
        var h=actor.HouseId;var dues=await m.Set<Due>().Find(x=>x.HouseId==h&&x.MemberId==actor.Id&&!x.Cancelled&&!x.Exempt).ToListAsync(ct);
        var cleanings=await m.Root.Cleanings.Find(x=>x.HouseId==h&&x.Status=="Published"&&x.EndsAt>DateTime.UtcNow&&x.Assignments.Any(a=>a.UserId==actor.Id)).SortBy(x=>x.StartsAt).Limit(4).ToListAsync(ct);
        var unread=await m.Root.Notifications.CountDocumentsAsync(x=>x.HouseId==h&&x.UserId==actor.Id&&x.ReadAt==null&&x.ArchivedAt==null,cancellationToken:ct);
        var pending=await m.Set<AdministrativeTask>().CountDocumentsAsync(x=>x.HouseId==h&&x.ResponsibleId==actor.Id&&x.State=="Open",cancellationToken:ct);
        object? finances=null;
        if(Access.Has(actor,"finance.read"))
        {
            var accounts=await m.Set<Account>().Find(x=>x.HouseId==h&&x.Active).ToListAsync(ct);var funds=await m.Set<Fund>().Find(x=>x.HouseId==h&&x.Restricted).ToListAsync(ct);var exp=await m.Set<Expense>().Find(x=>x.HouseId==h&&x.State!="Rejected").ToListAsync(ct);
            var amount=accounts.Sum(x=>x.BalanceCents);var reserved=funds.Sum(x=>x.BalanceCents);finances=new{balanceCents=amount,reservedCents=reserved,availableCents=amount-reserved,payableCents=exp.Sum(x=>x.AmountCents-x.PaidCents)};
        }
        return new{myOpenDuesCents=dues.Sum(x=>x.BalanceCents),unread,pendingTasks=pending,finances,cleanings=cleanings.Select(x=>new{x.Id,x.Title,x.Area,x.StartsAt,x.EndsAt,x.Mode,x.Target,x.Status,assigned=x.Assignments.Count(a=>!a.Dispensed),confirmed=x.Assignments.Count(a=>a.Response=="Confirmed"&&!a.Dispensed)})};
    }
    public async Task<object> Lookups(User actor,CancellationToken ct)
    {
        var h=actor.HouseId;var staff=Roles.Coordinates(actor)||actor.Permissions.Count>0;
        List<User> users=staff?await m.Root.Users.Find(x=>x.HouseId==h&&x.Active).SortBy(x=>x.Name).Limit(500).ToListAsync(ct):[actor];
        var items=await m.Set<Item>().Find(x=>x.HouseId==h&&x.Active).SortBy(x=>x.Name).Limit(500).ToListAsync(ct);
        return new{members=users.Select(x=>new{x.Id,x.Name}),items=items.Select(x=>new{x.Id,x.Name,x.Unit}),today=BusinessRules.Today()};
    }
    public async Task<object> Report(User actor,string period,CancellationToken ct)
    {
        Access.Demand(actor,"finance.read");BusinessRules.Month(period);var h=actor.HouseId;var filter=Builders<LedgerEntry>.Filter.Eq(x=>x.HouseId,h)&Builders<LedgerEntry>.Filter.Gte(x=>x.OccurredOn,period+"-01")&Builders<LedgerEntry>.Filter.Lte(x=>x.OccurredOn,period+"-31");
        var entries=await m.Set<LedgerEntry>().Find(filter).ToListAsync(ct);var accounts=await m.Set<Account>().Find(x=>x.HouseId==h).ToListAsync(ct);var funds=await m.Set<Fund>().Find(x=>x.HouseId==h).ToListAsync(ct);
        var due=await m.Set<Due>().Find(x=>x.HouseId==h&&x.Competence==period).ToListAsync(ct);var expenses=await m.Set<Expense>().Find(x=>x.HouseId==h&&x.State!="Rejected").ToListAsync(ct);
        var budget=await m.Set<Budget>().Find(x=>x.HouseId==h&&x.Period==period&&x.State=="Approved").ToListAsync(ct);
        return new{period,incomeCents=entries.Where(x=>x.Kind=="Receipt").Sum(x=>x.SignedCents),expenseCents=-entries.Where(x=>x.Kind is "Expense" or "Advance").Sum(x=>x.SignedCents),refundCents=-entries.Where(x=>x.Kind=="Refund").Sum(x=>x.SignedCents),cashChangeCents=entries.Sum(x=>x.SignedCents),duesExpectedCents=due.Sum(x=>x.AmountCents+x.AdjustmentCents),duesPaidCents=due.Sum(x=>x.PaidCents),duesOutstandingCents=due.Where(x=>!x.Exempt&&!x.Cancelled).Sum(x=>x.BalanceCents),accounts,funds,payables=expenses.Select(x=>new{x.Id,x.Title,x.DueDate,balanceCents=x.AmountCents-x.PaidCents}).Where(x=>x.balanceCents>0),categories=entries.Where(x=>x.Kind=="Expense").GroupBy(x=>x.Category).Select(x=>new{category=x.Key,amountCents=-x.Sum(y=>y.SignedCents)}),budget};
    }
    public async Task<string> ExportLedger(User actor,string period,CancellationToken ct)
    {
        Access.Demand(actor,"finance.read");BusinessRules.Month(period);var f=Builders<LedgerEntry>.Filter.Eq(x=>x.HouseId,actor.HouseId)&Builders<LedgerEntry>.Filter.Gte(x=>x.OccurredOn,period+"-01")&Builders<LedgerEntry>.Filter.Lte(x=>x.OccurredOn,period+"-31");var rows=await m.Set<LedgerEntry>().Find(f).SortBy(x=>x.OccurredOn).ToListAsync(ct);
        await m.Root.Audit.InsertOneAsync(new AuditEvent{HouseId=actor.HouseId,ActorId=actor.Id,Action="report.export",ResourceId=period},cancellationToken:ct);
        return "Data;Conta;Tipo;Categoria;Centavos;Referência\r\n"+string.Join("\r\n",rows.Select(x=>string.Join(";",new[]{x.OccurredOn,x.AccountId,x.Kind,x.Category,x.SignedCents.ToString(System.Globalization.CultureInfo.InvariantCulture),x.FinancialReference??""}.Select(BusinessRules.Csv))));
    }
}
