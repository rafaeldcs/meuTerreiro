using System.Threading.RateLimiting;
using MongoDB.Driver;
using Terreiro.Data;
using Terreiro.Domain;
using Terreiro.Service;
using Terreiro.WebAPI;

var builder = WebApplication.CreateBuilder(args);
var origin = builder.Configuration["App:Origin"] ?? "http://localhost:8080";
if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri) || originUri.AbsolutePath != "/" || originUri.Query.Length > 0)
    throw new InvalidOperationException("App__Origin deve ser somente a origem autorizada.");
if (!builder.Environment.IsDevelopment() && originUri.Scheme != "https")
    throw new InvalidOperationException("HTTPS é obrigatório fora de Development.");
if (builder.Environment.IsProduction())
    throw new InvalidOperationException("Este alpha é exclusivo de homologação. Complete os critérios de produção antes de habilitar Production.");
origin = origin.TrimEnd('/');
var houseId = builder.Configuration["App:HouseId"] ?? "tenda-dagua";
var connection = builder.Configuration["Mongo:ConnectionString"] ?? throw new InvalidOperationException("Configure Mongo__ConnectionString.");
var database = builder.Configuration["Mongo:Database"] ?? "terreiro_hml";
builder.Services.AddSingleton(new MongoStore(connection, database));
builder.Services.AddSingleton(sp => new AuthService(sp.GetRequiredService<MongoStore>(), houseId));
builder.Services.AddSingleton<ModuleStore>();
builder.Services.AddSingleton<EfiPixClient>();
builder.Services.AddSingleton<PixService>();
builder.Services.AddHostedService<PixWorker>();
builder.Services.AddSingleton<CleaningService>();
builder.Services.AddSingleton<FinanceService>();
builder.Services.AddSingleton<OperationsService>();
builder.Services.AddSingleton<QueryService>();
builder.Services.AddSingleton<ReconciliationService>();
builder.Services.AddSingleton<AdvancedCleaningService>();
builder.Services.AddSingleton<CommunicationService>();
builder.Services.AddSingleton<SecurityService>();
builder.Services.AddSingleton(sp => new EvidenceService(sp.GetRequiredService<ModuleStore>(), builder.Configuration["Files:Directory"] ?? "/app/data/evidence"));
builder.Services.AddHostedService<EvidenceWorker>();
builder.Services.AddHostedService<ScheduledWorker>();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => { o.MultipartBodyLengthLimit=11*1024*1024; o.ValueLengthLimit=65536; o.MultipartHeadersLengthLimit=16384; });
builder.Services.AddHostedService<PushWorker>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 12*1024*1024);
var app = builder.Build();
var store = app.Services.GetRequiredService<MongoStore>();
await store.Initialize(CancellationToken.None);
await app.Services.GetRequiredService<ModuleStore>().Initialize(houseId, CancellationToken.None);
await LegacyAccessMigration.Run(store,houseId,builder.Configuration.GetValue<bool>("Bootstrap:MigrateLegacyPermissions"),CancellationToken.None);
var bootstrapPassword = builder.Configuration["Bootstrap:Password"];
if (!string.IsNullOrWhiteSpace(bootstrapPassword))
    await app.Services.GetRequiredService<AuthService>().Bootstrap(builder.Configuration["Bootstrap:Login"] ?? "admin", bootstrapPassword, CancellationToken.None);

app.Use(async (context, next) =>
{
    context.Response.Headers["Cache-Control"] = "no-store, private";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    try
    {
        if (context.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            Rules.Require(context.Request.Headers["Origin"].ToString().TrimEnd('/') == origin
                && context.Request.Headers["X-Terreiro-Client"] == "app", "origin_rejected", "Origem da requisição não autorizada.", 403);
            var upload = context.Request.Path == "/api/evidence/upload";
            Rules.Require(upload ? context.Request.HasFormContentType : context.Request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true,
                "content_type", upload ? "Envie um formulário com o arquivo." : "Envie JSON.", 415);
            if (!upload && context.Request.ContentLength > 2*1024*1024) throw new RuleException("body_large", "Requisição excede o limite.", 413);
        }
        if (context.Request.Path.StartsWithSegments("/api") && context.Request.Path != "/api/auth/login" && context.Request.Path != "/api/auth/recover" && context.Request.Path != "/api/status")
        {
            var auth = context.RequestServices.GetRequiredService<AuthService>();
            var user = await auth.Authenticate(context.Request.Cookies["terreiro_session"], context.RequestAborted);
            Rules.Require(user != null, "unauthorized", "Entre para continuar.", 401);
            context.Items["actor"] = user;
            if (user!.MustChangePassword)
                Rules.Require(context.Request.Path == "/api/auth/me" || context.Request.Path == "/api/auth/password" || context.Request.Path == "/api/auth/logout",
                    "password_change_required", "Altere a senha temporária antes de continuar.", 403);
        }
        await next();
    }
    catch (RuleException ex)
    {
        context.Response.StatusCode = ex.Status;
        await context.Response.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message });
    }
    catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new { code = "duplicate", message = "Este registro já existe. Atualize e confira antes de repetir." });
    }
    catch (Exception ex) when (ex is BadHttpRequestException or System.Text.Json.JsonException or OverflowException)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { code = "invalid_request", message = "Dados inválidos na requisição." });
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
    catch (Exception ex)
    {
        // Do not put tokens, request bodies, passwords, or Mongo connection strings in general logs.
        app.Logger.LogError("Request failed ({ErrorType}); trace {TraceId}", ex.GetType().Name, context.TraceIdentifier);
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new { code = "internal_error", message = "Não foi possível concluir. Atualize e tente novamente.", traceId = context.TraceIdentifier });
    }
});
app.UseRateLimiter();
app.MapGet("/health", async (MongoStore db, CancellationToken ct) =>
{
    await db.Database.RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("ping", 1), cancellationToken: ct);
    return Results.Ok(new { status = "ok" });
});
app.MapGet("/api/status", () => new { version = "0.2.2-alpha.1", environment = "Homologação", readyForProduction = false });
app.MapPost("/api/auth/login", async (LoginInput input, HttpContext context, AuthService auth, CancellationToken ct) =>
{
    var (user, token) = await auth.Login(input.Login ?? "", input.Password ?? "", ct);
    context.Response.Cookies.Append("terreiro_session", token, CookieOptions());
    return Results.Ok(UserView(user));
}).RequireRateLimiting("login");
app.MapGet("/api/auth/me", (HttpContext c) => Results.Ok(UserView(Actor(c))));
app.MapPost("/api/auth/logout", async (HttpContext c, AuthService auth, CancellationToken ct) =>
{
    await auth.Logout(c.Request.Cookies["terreiro_session"], ct);
    c.Response.Cookies.Delete("terreiro_session", CookieOptions());
    return Results.NoContent();
});
app.MapPost("/api/auth/password", async (PasswordInput input, HttpContext c, AuthService auth, CancellationToken ct) =>
{
    await auth.ChangePassword(Actor(c), input.CurrentPassword ?? "", input.NewPassword ?? "", ct);
    c.Response.Cookies.Delete("terreiro_session", CookieOptions());
    return Results.NoContent();
});
app.MapGet("/api/members", async (HttpContext c, MongoStore db, CancellationToken ct) =>
{
    var actor = Actor(c);
    Rules.Require(Roles.Coordinates(actor)||Access.Finance(actor)||Access.Has(actor,"finance.write")||Access.Has(actor,"finance.approve"), "forbidden", "Cadastro restrito à coordenação e à equipe financeira autorizada.", 403);
    var users = await db.Users.Find(u => u.HouseId == houseId && u.Active).SortBy(u => u.Name).Limit(500).ToListAsync(ct);
    return Results.Ok(users.Select(u => new { u.Id, u.Name, u.Role, u.IsMember }));
});
app.MapPost("/api/members", async (MemberInput input, HttpContext c, AuthService auth, SecurityService security, CancellationToken ct) =>
{
    Rules.Require(Actor(c).Role==Roles.Admin,"forbidden","Apenas a administração pode criar acessos.",403);
    if(input.Role!=Roles.Member)await security.RequireRecent(Actor(c),c.Request.Cookies["terreiro_session"]!,ct);
    return Results.Ok(UserView(await auth.CreateMember(Actor(c), input.Name ?? "", input.Login ?? "", input.Password ?? "", input.Role, ct)));
});
app.MapGet("/api/cleanings", async (HttpContext c, CleaningService svc, CancellationToken ct) =>
    Results.Ok((await svc.List(Actor(c), ct)).Select(x => CleaningView(x, Actor(c)))));
app.MapGet("/api/cleanings/{id}", async (string id, HttpContext c, CleaningService svc, CancellationToken ct) =>
    Results.Ok(CleaningView(await svc.Get(Actor(c), id, ct), Actor(c))));
app.MapPost("/api/cleanings", async (CreateCleaning input, HttpContext c, CleaningService svc, CancellationToken ct) =>
{
    Rules.Require(input.Title != null && input.Area != null && input.MemberIds != null && input.Tasks != null && input.Tasks.All(t => t != null), "invalid_request", "Campos obrigatórios ausentes.");
    return Results.Ok(CleaningView(await svc.Create(Actor(c), input, ct), Actor(c)));
});
app.MapPost("/api/cleanings/{id}/response", async (string id, ResponseInput input, HttpContext c, CleaningService svc, CancellationToken ct) =>
    Results.Ok(CleaningView(await svc.Respond(Actor(c), id, input.Version, input.Response, ct), Actor(c))));
app.MapPost("/api/cleanings/{id}/verify", async (string id, VerifyInput input, HttpContext c, CleaningService svc, CancellationToken ct) =>
    Results.Ok(CleaningView(await svc.Verify(Actor(c), id, input.Revision, input.TaskId, input.MemberId, input.Participation, ct), Actor(c))));
app.MapPost("/api/cleanings/{id}/finish", async (string id, FinishInput input, HttpContext c, CleaningService svc, CancellationToken ct) =>
    Results.Ok(CleaningView(await svc.Finish(Actor(c), id, input.Revision, input.Cancel, input.Reason ?? "", ct), Actor(c))));
app.MapGet("/api/notifications", async (HttpContext c, MongoStore db, CancellationToken ct) =>
{
    var user = Actor(c);
    var list = await db.Notifications.Find(n => n.HouseId == houseId && n.UserId == user.Id).SortByDescending(n => n.CreatedAt).Limit(100).ToListAsync(ct);
    return Results.Ok(list.Select(n => new { n.Id, n.Title, n.Body, n.Path, n.CreatedAt, n.ReadAt, n.ExpiresAt }));
});
app.MapPost("/api/notifications/{id}/read", async (string id, HttpContext c, MongoStore db, CancellationToken ct) =>
{
    var user = Actor(c);
    // This operation never accepts a cleaning invitation or verifies participation.
    await db.Notifications.UpdateOneAsync(n => n.Id == id && n.UserId == user.Id && n.HouseId == houseId && n.ReadAt == null,
        Builders<Notification>.Update.Set(n => n.ReadAt, DateTime.UtcNow), cancellationToken: ct);
    return Results.NoContent();
});
app.MapGet("/api/push/config", (IConfiguration cfg) => new { enabled = cfg.GetValue<bool>("WebPush:Enabled"), publicKey = cfg["WebPush:PublicKey"] ?? "" });
app.MapPost("/api/push/subscribe", async (SubscriptionInput input, HttpContext c, MongoStore db, IConfiguration cfg, CancellationToken ct) =>
{
    Rules.Require(cfg.GetValue<bool>("WebPush:Enabled"), "push_disabled", "Push ainda não configurado neste ambiente.", 409);
    Rules.Require(Rules.IsMobile(c.Request.Headers.UserAgent.ToString()), "mobile_only", "Notificações são habilitadas somente no celular.", 403);
    Rules.Require(Rules.ValidPushEndpoint(input.Endpoint ?? ""), "endpoint_invalid", "Destino de push não autorizado.");
    Rules.Require(input.Keys != null && ValidKey(input.Keys.P256dh, 65) && ValidKey(input.Keys.Auth, 16), "keys_invalid", "Chaves de assinatura inválidas.");
    var user = Actor(c);
    var device = new PushDevice { Id = Rules.Hash(input.Endpoint), Endpoint = input.Endpoint, P256dh = input.Keys!.P256dh, Auth = input.Keys.Auth,
        HouseId = houseId, UserId = user.Id, SessionId = Rules.Hash(c.Request.Cookies["terreiro_session"]!) };
    await db.Devices.ReplaceOneAsync(d => d.Id == device.Id, device, new ReplaceOptions { IsUpsert = true }, ct);
    return Results.NoContent();
});
app.MapPost("/api/push/revoke", async (HttpContext c, MongoStore db, CancellationToken ct) =>
{
    var session = Rules.Hash(c.Request.Cookies["terreiro_session"]!);
    await db.Devices.DeleteManyAsync(d => d.SessionId == session && d.HouseId == houseId, ct);
    return Results.NoContent();
});
app.MapAdministrativeModules();
app.Run();

User Actor(HttpContext context) => (User)context.Items["actor"]!;
object UserView(User user) => new { user.Id, user.Name, user.Role, user.Permissions, user.IsMember, user.MustChangePassword };
CookieOptions CookieOptions() => new() { HttpOnly = true, Secure = !app.Environment.IsDevelopment(), SameSite = SameSiteMode.Strict, Path = "/", MaxAge = TimeSpan.FromHours(12), IsEssential = true };
bool ValidKey(string? value, int bytes)
{
    if (string.IsNullOrWhiteSpace(value) || value.Length > 200) return false;
    try { return Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '=')).Length == bytes; }
    catch (Exception ex) when (ex is FormatException or ArgumentNullException) { return false; }
}
object CleaningView(Cleaning x, User actor)
{
    var canManage = actor.Role == Roles.Admin || (actor.Role == Roles.Coordinator && x.CoordinatorId == actor.Id);
    return new
    {
        x.Id, x.Title, x.Area, x.Mode, x.Target, x.Minimum, x.Maximum, x.EventId, x.SeriesId, x.NeedsScheduleReview, x.StartsAt, x.EndsAt, x.Status, x.Revision, x.PublicationVersion,
        canManage, assigned = x.Assignments.Count(a=>!a.Dispensed), dispensed=x.Assignments.Count(a=>a.Dispensed), confirmed = x.Assignments.Count(a => !a.Dispensed && a.Response == "Confirmed"),
        pending = x.Assignments.Count(a => !a.Dispensed && (a.Response == "Pending" || a.Response=="NeedsReview")), unavailable = x.Assignments.Count(a => !a.Dispensed && a.Response == "Unavailable"),
        myDispensed = x.Assignments.FirstOrDefault(a=>a.UserId==actor.Id)?.Dispensed ?? false,
        myResponse = x.Assignments.FirstOrDefault(a => a.UserId == actor.Id)?.Response,
        myParticipation = x.Assignments.FirstOrDefault(a => a.UserId == actor.Id)?.Participation,
        assignments = canManage ? x.Assignments.ToArray() : [],
        tasks = x.Tasks.Select(t => new { t.Id, t.Title, t.Required, t.Verified, t.AssignedTo, t.ReportedDone }).ToArray()
    };
}
