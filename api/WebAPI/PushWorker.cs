using System.Net;
using System.Text.Json;
using MongoDB.Driver;
using Terreiro.Data;
using Terreiro.Domain;
using Terreiro.Service;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;

namespace Terreiro.WebAPI;

public sealed class PushWorker(MongoStore db, ModuleStore m, AuthService auth, IConfiguration cfg, ILogger<PushWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!cfg.GetValue<bool>("WebPush:Enabled")) return;
        var subject = cfg["WebPush:Subject"] ?? "";
        var publicKey = cfg["WebPush:PublicKey"] ?? "";
        var privateKey = cfg["WebPush:PrivateKey"] ?? "";
        if (!Uri.TryCreate(subject, UriKind.Absolute, out var subjectUri) || subjectUri.Scheme != "https" || publicKey.Length < 80 || privateKey.Length < 40)
            throw new InvalidOperationException("WebPush habilitado exige Subject HTTPS e chaves VAPID válidas.");
        var vapid = new VapidAuthentication(publicKey, privateKey) { Subject = subject };
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(12) };
        var sender = new PushServiceClient(http) { DefaultAuthentication = vapid, AutoRetryAfter = false };
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var item = await db.Notifications.FindOneAndUpdateAsync(
                    n => n.HouseId == auth.HouseId && (n.PushState == "Pending" || n.PushState == "Retry") && n.DueAt <= now
                        && (n.LeaseUntil == null || n.LeaseUntil <= now),
                    Builders<Notification>.Update.Set(n => n.LeaseUntil, now.AddMinutes(2)).Inc(n => n.Attempts, 1),
                    new FindOneAndUpdateOptions<Notification> { ReturnDocument = ReturnDocument.After }, stoppingToken);
                if (item == null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    continue;
                }
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                budget.CancelAfter(TimeSpan.FromSeconds(90));
                var preference=await m.Set<NotificationPreference>().Find(x=>x.HouseId==item.HouseId&&x.UserId==item.UserId).FirstOrDefaultAsync(stoppingToken)??new NotificationPreference();
                var local=TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"));
                var start=preference.QuietStartHour;var end=preference.QuietEndHour;var quiet=start!=end&&(start<end?local.Hour>=start&&local.Hour<end:local.Hour>=start||local.Hour<end);
                if(preference.PushEnabled&&!preference.MutedCategories.Contains(item.Category)&&quiet&&item.ExpiresAt>DateTime.UtcNow)
                {
                    var resume=local.Date.AddHours(end);if(resume<=local)resume=resume.AddDays(1);var utc=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(resume,DateTimeKind.Unspecified),TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"));
                    await db.Notifications.UpdateOneAsync(n=>n.Id==item.Id&&n.LeaseUntil==item.LeaseUntil&&n.PushState!="Suppressed",Builders<Notification>.Update.Set(n=>n.PushState,"Retry").Set(n=>n.DueAt,utc).Set(n=>n.LeaseUntil,null).Inc(n=>n.Attempts,-1),cancellationToken:stoppingToken);continue;
                }
                var state = !preference.PushEnabled || preference.MutedCategories.Contains(item.Category) ? "Suppressed" : await Dispatch(item, sender, vapid, budget.Token);
                // Do not overwrite suppression written concurrently by schedule cancellation.
                await db.Notifications.UpdateOneAsync(n => n.Id == item.Id && n.LeaseUntil == item.LeaseUntil && n.PushState != "Suppressed",
                    Builders<Notification>.Update.Set(n => n.PushState, state).Set(n => n.LeaseUntil, null)
                        .Set(n => n.DueAt, DateTime.UtcNow.AddSeconds(Math.Min(300, 15 * item.Attempts))), cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning("Push worker will retry after failure ({ErrorType})", ex.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
    private async Task<string> Dispatch(Notification n, PushServiceClient sender, VapidAuthentication vapid, CancellationToken ct)
    {
        if (n.ExpiresAt <= DateTime.UtcNow || n.Attempts > 4) return "Expired";
        var user = await db.Users.Find(u => u.Id == n.UserId && u.HouseId == n.HouseId && u.Active).FirstOrDefaultAsync(ct);
        if (user == null) return "Suppressed";
        if(n.Category=="DueReminder"&&n.ReferenceId!=null)
        {
            var due=await m.Set<Due>().Find(x=>x.Id==n.ReferenceId&&x.HouseId==n.HouseId&&x.MemberId==n.UserId).FirstOrDefaultAsync(ct);
            if(due==null||due.BalanceCents==0)return "Suppressed";
            var recent=DateTime.UtcNow.AddDays(-3);if(await m.Set<Evidence>().Find(x=>x.HouseId==n.HouseId&&x.MemberId==n.UserId&&x.DueIds.Contains(n.ReferenceId)&&x.CreatedAt>=recent&&x.ReceiptId==null).AnyAsync(ct))return "Suppressed";
        }
        if (n.SuppressWhenCancelled && n.CleaningId != null)
        {
            var cleaning = await db.Cleanings.Find(x => x.Id == n.CleaningId && x.HouseId == n.HouseId).FirstOrDefaultAsync(ct);
            if (cleaning == null || cleaning.Status != "Published" || cleaning.PublicationVersion != n.PublicationVersion) return "Suppressed";
        }
        var devices = await db.Devices.Find(d => d.UserId == n.UserId && d.HouseId == n.HouseId).Limit(5).ToListAsync(ct);
        var retry = false;
        var attempted = false;
        foreach (var device in devices)
        {
            if (!Rules.ValidPushEndpoint(device.Endpoint)) continue;
            // Recheck association after a possible logout or device account switch.
            if (!await db.Devices.Find(d => d.Id == device.Id && d.UserId == n.UserId && d.SessionId == device.SessionId).AnyAsync(ct)) continue;
            // A login cookie expires independently of permission to receive generic mobile alerts.
            // Logout, password reset and access revocation explicitly remove registrations.
            var deliveryId = n.Id + ":" + device.Id;
            var previous = await db.Deliveries.Find(d => d.Id == deliveryId && d.Status == "Accepted").AnyAsync(ct);
            if (previous) { attempted = true; continue; }
            try
            {
                attempted = true;
                // Never transmit title, amount, participant names, explanations, or a payment status.
                var payload = JsonSerializer.Serialize(new { id = n.Id, path = "/notificacoes", title = "Atualização no aplicativo", body = "Abra o aplicativo para consultar." });
                var subscription = new PushSubscription { Endpoint = device.Endpoint };
                subscription.SetKey(PushEncryptionKeyName.P256DH, device.P256dh);
                subscription.SetKey(PushEncryptionKeyName.Auth, device.Auth);
                var message = new PushMessage(payload)
                {
                    TimeToLive = Math.Max(0, Math.Min(900, (int)(n.ExpiresAt - DateTime.UtcNow).TotalSeconds)),
                    Topic = n.Id[..32]
                };
                await sender.RequestPushMessageDeliveryAsync(subscription, message, vapid, ct);
                await db.Deliveries.ReplaceOneAsync(d => d.Id == deliveryId, new PushDelivery { Id = deliveryId, Status = "Accepted" }, new ReplaceOptions { IsUpsert = true }, ct);
            }
            catch (PushServiceClientException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                await db.Devices.DeleteOneAsync(d => d.Id == device.Id && d.SessionId == device.SessionId, ct);
            }
            catch (Exception ex) when (ex is PushServiceClientException or HttpRequestException or TaskCanceledException)
            {
                if (ct.IsCancellationRequested) throw;
                retry = true;
                logger.LogWarning("Mobile push not confirmed ({ErrorType})", ex.GetType().Name);
            }
        }
        return retry ? (n.Attempts >= 4 ? "Failed" : "Retry") : attempted ? "Accepted" : "NotEnabled";
    }
}
