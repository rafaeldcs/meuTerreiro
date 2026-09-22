using System.Text.Json;
using MongoDB.Driver;
using Terreiro.Data;
using Terreiro.Domain;
namespace Terreiro.Service;

public sealed record CreateCleaning(string OperationId, string Title, string Area, string Mode,
    int? Target, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string[] MemberIds, string[] Tasks);
public sealed class CleaningService(MongoStore db, AuthService auth, ModuleStore modules)
{
    private string House => auth.HouseId;
    private void Coordinator(User actor, Cleaning? cleaning = null)
    {
        Rules.Require(Roles.Coordinates(actor) && (cleaning == null || actor.Role == Roles.Admin || cleaning.CoordinatorId == actor.Id),
            "forbidden", "Esta ação é exclusiva da coordenação responsável.", 403);
    }
    public async Task<List<Cleaning>> List(User actor, CancellationToken ct)
    {
        var f = Builders<Cleaning>.Filter.Eq(x => x.HouseId, House);
        if (actor.Role != Roles.Admin)
            f &= Builders<Cleaning>.Filter.ElemMatch(x => x.Assignments, x => x.UserId == actor.Id)
                | Builders<Cleaning>.Filter.Eq(x => x.CoordinatorId, actor.Id);
        return await db.Cleanings.Find(f).SortByDescending(x => x.StartsAt).Limit(100).ToListAsync(ct);
    }
    public async Task<Cleaning> Get(User actor, string id, CancellationToken ct)
    {
        var item = await db.Cleanings.Find(x => x.Id == id && x.HouseId == House).FirstOrDefaultAsync(ct);
        Rules.Require(item != null, "not_found", "Atividade não encontrada.", 404);
        Rules.Require(actor.Role == Roles.Admin || item!.CoordinatorId == actor.Id || item.Assignments.Any(a => a.UserId == actor.Id),
            "not_found", "Atividade não encontrada.", 404);
        return item!;
    }
    public async Task<Cleaning> Create(User actor, CreateCleaning input, CancellationToken ct)
    {
        Coordinator(actor);
        Rules.Require(Guid.TryParse(input.OperationId, out _), "operation_invalid", "Identificador da operação inválido.");
        Rules.Require(input.MemberIds.Length <= 500 && input.Tasks.Length is > 0 and <= 30, "size_invalid", "Informe até 500 participantes e de 1 a 30 tarefas.");
        var id = Rules.Hash(House + "|cleaning|" + actor.Id + "|" + input.OperationId)[..32];
        var hash = Rules.Hash(JsonSerializer.Serialize(input));
        var previous = await db.Cleanings.Find(x => x.Id == id && x.HouseId == House).FirstOrDefaultAsync(ct);
        if (previous != null)
        {
            Rules.Require(previous.RequestHash == hash, "operation_reused", "Esta operação já foi utilizada com outros dados.", 409);
            return previous;
        }
        Rules.Plan(input.Mode, input.Target, input.StartsAt.UtcDateTime, input.EndsAt.UtcDateTime, DateTime.UtcNow);
        var title = Rules.Text(input.Title, 3, 120, "Título");
        var area = Rules.Text(input.Area, 2, 120, "Local/área");
        var taskTitles = input.Tasks.Select(t => Rules.Text(t, 2, 200, "Tarefa")).ToArray();
        var ids = input.MemberIds.Distinct().OrderBy(x => x).ToArray();
        Rules.Require(input.Mode != "Team" || ids.Length > 0, "members_required", "Selecione os participantes.");
        Rules.Require(input.Mode != "General" || input.MemberIds.Length == 0, "general_scope", "O mutirão usa a lista de membros ativos, definida pelo servidor.");
        return await db.Transaction(async (s, c) =>
        {
            await modules.Set<HouseGate>().UpdateOneAsync(s,x=>x.Id==House,Builders<HouseGate>.Update.Inc(x=>x.Sequence,1),cancellationToken:c);
            var currentActor=await db.Users.Find(s,u=>u.Id==actor.Id&&u.HouseId==actor.HouseId&&u.Active&&u.AuthRevision==actor.AuthRevision).FirstOrDefaultAsync(c);
            Rules.Require(currentActor!=null,"access_changed","Seu acesso foi alterado. Entre novamente.",401);
            var actorLock=await db.Users.UpdateOneAsync(s,u=>u.Id==actor.Id&&u.HouseId==House&&u.Active&&u.AuthRevision==actor.AuthRevision,Builders<User>.Update.Inc(u=>u.OperationRevision,1),cancellationToken:c);
            Rules.Require(actorLock.MatchedCount==1,"access_changed","Seu acesso foi alterado.",401);
            var members = input.Mode == "General"
                ? await db.Users.Find(s, u => u.HouseId == House && u.Active && u.IsMember).Limit(501).ToListAsync(c)
                : await db.Users.Find(s, u => u.HouseId == House && u.Active && u.IsMember && ids.Contains(u.Id)).ToListAsync(c);
            Rules.Require(members.Count is > 0 and <= 500, "members_invalid", "Selecione de 1 a 500 membros ativos.");
            Rules.Require(input.Mode == "General" || members.Count == ids.Length, "members_invalid", "Um participante não está ativo nesta casa.");
            var selected = members.Select(m => m.Id).ToArray();
            // Lock the participating documents in this transaction to serialize overlapping publications.
            await db.Users.UpdateManyAsync(s, u => u.HouseId == House && selected.Contains(u.Id),
                Builders<User>.Update.Inc(u => u.ScheduleRevision, 1), cancellationToken: c);
            var start = input.StartsAt.UtcDateTime;
            var end = input.EndsAt.UtcDateTime;
            var conflict = await db.Cleanings.Find(s, x => x.HouseId == House && x.Status == "Published"
                && x.StartsAt < end && x.EndsAt > start && x.Assignments.Any(a => selected.Contains(a.UserId) && !a.Dispensed)).AnyAsync(c);
            Rules.Require(!conflict, "schedule_conflict", "Há participante escalado em outro turno neste horário.", 409);
            var item = new Cleaning
            {
                Id = id, RequestHash = hash, HouseId = House, CreatedBy = actor.Id, CoordinatorId = actor.Id,
                Title = title, Area = area, Mode = input.Mode, Target = input.Mode == "Team" ? input.Target : null,
                StartsAt = start, EndsAt = end,
                Assignments = members.OrderBy(m => m.Name).Select(m => new Assignment { UserId = m.Id, Name = m.Name }).ToList(),
                Tasks = taskTitles.Select(t => new CleaningTask { Title = t }).ToList()
            };
            await db.Cleanings.InsertOneAsync(s, item, cancellationToken: c);
            foreach (var member in members)
                await Notify(s, "published:" + id, member.Id, "Nova escala de limpeza", "Você foi escalado. Confira os detalhes e responda no aplicativo.", item, true, c);
            await Audit(s, actor, "cleaning.published", id, c);
            return item;
        }, ct);
    }
    public async Task<Cleaning> Respond(User actor, string id, int version, string response, CancellationToken ct)
    {
        return await db.Transaction(async (s, c) =>
        {
            await modules.Set<HouseGate>().UpdateOneAsync(s,x=>x.Id==House,Builders<HouseGate>.Update.Inc(x=>x.Sequence,1),cancellationToken:c);
            var currentActor=await db.Users.Find(s,u=>u.Id==actor.Id&&u.HouseId==actor.HouseId&&u.Active&&u.AuthRevision==actor.AuthRevision).FirstOrDefaultAsync(c);
            Rules.Require(currentActor!=null,"access_changed","Seu acesso foi alterado. Entre novamente.",401);
            var actorLock=await db.Users.UpdateOneAsync(s,u=>u.Id==actor.Id&&u.HouseId==House&&u.Active&&u.AuthRevision==actor.AuthRevision,Builders<User>.Update.Inc(u=>u.OperationRevision,1),cancellationToken:c);
            Rules.Require(actorLock.MatchedCount==1,"access_changed","Seu acesso foi alterado.",401);
            var item = await Load(s, id, c);
            Rules.CanRespond(item, actor.Id, version, response, DateTime.UtcNow);
            var assignment = item.Assignments.Single(a => a.UserId == actor.Id);
            if (assignment.Response == response) return item;
            assignment.Response = response;
            assignment.RespondedAt = DateTime.UtcNow;
            await Save(s, item, c);
            await Notify(s, $"response:{id}:{actor.Id}:{item.Revision}", item.CoordinatorId,
                "Resposta à escala", "Uma resposta foi atualizada. Consulte a cobertura da equipe.", item, false, c);
            await Audit(s, actor, "cleaning.response." + response, id, c);
            return item;
        }, ct);
    }
    public async Task<Cleaning> Verify(User actor, string id, int revision, string? taskId, string? memberId, string? participation, CancellationToken ct)
    {
        return await db.Transaction(async (s, c) =>
        {
            await modules.Set<HouseGate>().UpdateOneAsync(s,x=>x.Id==House,Builders<HouseGate>.Update.Inc(x=>x.Sequence,1),cancellationToken:c);
            var currentActor=await db.Users.Find(s,u=>u.Id==actor.Id&&u.HouseId==actor.HouseId&&u.Active&&u.AuthRevision==actor.AuthRevision).FirstOrDefaultAsync(c);
            Rules.Require(currentActor!=null,"access_changed","Seu acesso foi alterado. Entre novamente.",401);
            var actorLock=await db.Users.UpdateOneAsync(s,u=>u.Id==actor.Id&&u.HouseId==House&&u.Active&&u.AuthRevision==actor.AuthRevision,Builders<User>.Update.Inc(u=>u.OperationRevision,1),cancellationToken:c);
            Rules.Require(actorLock.MatchedCount==1,"access_changed","Seu acesso foi alterado.",401);
            var item = await Load(s, id, c);
            Coordinator(actor, item);
            Rules.Require(item.Revision == revision, "conflict", "Outro registro mudou a atividade. Atualize a tela.", 409);
            Rules.Require(item.Status == "Published" && DateTime.UtcNow >= item.StartsAt, "not_started", "Registre a execução somente após o início da atividade.", 409);
            Rules.Require((taskId != null) != (memberId != null), "invalid_action", "Informe uma tarefa ou um participante por operação.");
            if (taskId != null)
            {
                var task = item.Tasks.FirstOrDefault(t => t.Id == taskId);
                Rules.Require(task != null, "not_found", "Tarefa não encontrada.", 404);
                task!.Verified = true;
                task.VerifiedBy = actor.Id;
            }
            else
            {
                Rules.Require(participation is "Present" or "Partial" or "Absent", "participation_invalid", "Participação inválida.");
                var assignment = item.Assignments.FirstOrDefault(a => a.UserId == memberId);
                Rules.Require(assignment != null, "not_found", "Participante não encontrado.", 404);
                Rules.Require(!assignment.Dispensed,"dispensed","A dispensa não pode ser convertida em ausência. Revise a designação primeiro.",409);
                assignment!.Participation = participation!;
                assignment.VerifiedBy = actor.Id;
            }
            await Save(s, item, c);
            await Audit(s, actor, taskId != null ? "cleaning.task_verified" : "cleaning.participation_recorded", taskId ?? memberId!, c);
            return item;
        }, ct);
    }
    public async Task<Cleaning> Finish(User actor, string id, int revision, bool cancel, string reason, CancellationToken ct)
    {
        return await db.Transaction(async (s, c) =>
        {
            await modules.Set<HouseGate>().UpdateOneAsync(s,x=>x.Id==House,Builders<HouseGate>.Update.Inc(x=>x.Sequence,1),cancellationToken:c);
            var currentActor=await db.Users.Find(s,u=>u.Id==actor.Id&&u.HouseId==actor.HouseId&&u.Active&&u.AuthRevision==actor.AuthRevision).FirstOrDefaultAsync(c);
            Rules.Require(currentActor!=null,"access_changed","Seu acesso foi alterado. Entre novamente.",401);
            var actorLock=await db.Users.UpdateOneAsync(s,u=>u.Id==actor.Id&&u.HouseId==House&&u.Active&&u.AuthRevision==actor.AuthRevision,Builders<User>.Update.Inc(u=>u.OperationRevision,1),cancellationToken:c);
            Rules.Require(actorLock.MatchedCount==1,"access_changed","Seu acesso foi alterado.",401);
            var item = await Load(s, id, c);
            Coordinator(actor, item);
            Rules.Require(item.Revision == revision && item.Status == "Published", "conflict", "A atividade foi alterada ou encerrada. Atualize a tela.", 409);
            if (!cancel) Rules.CanClose(item, DateTime.UtcNow);
            // Release only material that never left stock. Issued quantities still need real settlement.
            var reservations=await modules.Set<Reservation>().Find(s,x=>x.HouseId==House&&x.ActivityKind=="Cleaning"&&x.ActivityId==item.Id&&(x.State=="Reserved"||x.State=="Issued")).ToListAsync(c);
            foreach(var reservation in reservations)
            {
                var unissued=reservation.QuantityMilli-reservation.IssuedMilli;
                if(unissued>0)
                {
                    var lot=await modules.Get<StockLot>(s,House,reservation.LotId,c);
                    Rules.Require(lot.ReservedMilli>=unissued,"stock_integrity","Reserva inconsistente. Revise o estoque.",409);
                    lot.ReservedMilli-=unissued;await modules.Save(s,lot,c);
                    await modules.Insert(s,new StockMovement{HouseId=House,ItemId=reservation.ItemId,LotId=lot.Id,Kind="Released",QuantityMilli=unissued,SourceId=reservation.Id,ActorId=actor.Id,Reason="Liberação de material não retirado ao encerrar a limpeza"},c);
                }
                reservation.State=reservation.IssuedMilli==reservation.ConsumedMilli+reservation.ReturnedMilli+reservation.LostMilli?"Released":"CancelledWithIssued";
                await modules.Save(s,reservation,c);
            }
            item.Status = cancel ? "Cancelled" : "Completed";
            item.ClosingReason = Rules.Text(reason, 3, 300, "Motivo/observação de encerramento");
            item.PublicationVersion++;
            await Save(s, item, c);
            await db.Notifications.UpdateManyAsync(s, n => n.HouseId == House && n.CleaningId == id && n.SuppressWhenCancelled,
                Builders<Notification>.Update.Set(n => n.PushState, "Suppressed"), cancellationToken: c);
            foreach (var a in item.Assignments)
                await Notify(s, $"finished:{id}:{item.PublicationVersion}", a.UserId,
                    cancel ? "Limpeza cancelada" : "Limpeza encerrada", "A situação da atividade foi atualizada. Consulte no aplicativo.", item, false, c);
            await Audit(s, actor, "cleaning." + item.Status, id, c);
            return item;
        }, ct);
    }
    private async Task<Cleaning> Load(IClientSessionHandle session, string id, CancellationToken ct)
    {
        var item = await db.Cleanings.Find(session, x => x.Id == id && x.HouseId == House).FirstOrDefaultAsync(ct);
        Rules.Require(item != null, "not_found", "Atividade não encontrada.", 404);
        return item!;
    }
    private async Task Save(IClientSessionHandle session, Cleaning item, CancellationToken ct)
    {
        var revision = item.Revision;
        item.Revision++;
        var result = await db.Cleanings.ReplaceOneAsync(session, x => x.Id == item.Id && x.HouseId == House && x.Revision == revision, item, cancellationToken: ct);
        Rules.Require(result.ModifiedCount == 1, "conflict", "Outra pessoa alterou esta atividade. Atualize a tela.", 409);
    }
    private Task Audit(IClientSessionHandle session, User actor, string action, string id, CancellationToken ct) =>
        db.Audit.InsertOneAsync(session, new AuditEvent { HouseId = House, ActorId = actor.Id, Action = action, ResourceId = id }, cancellationToken: ct);
    private Task Notify(IClientSessionHandle session, string key, string userId, string title, string body, Cleaning item, bool suppress, CancellationToken ct) =>
        db.Notifications.InsertOneAsync(session, new Notification
        {
            Id = Rules.Hash(House + "|" + key + "|" + userId), HouseId = House, UserId = userId,
            Title = title, Body = body, Path = "/limpezas/" + item.Id, CleaningId = item.Id,
            PublicationVersion = item.PublicationVersion, SuppressWhenCancelled = suppress,
            ExpiresAt = suppress ? item.EndsAt : DateTime.UtcNow.AddDays(7)
        }, cancellationToken: ct);
}
