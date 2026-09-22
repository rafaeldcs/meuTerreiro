namespace Terreiro.Domain;

public static class Roles
{
    public const string Admin = "Admin", Coordinator = "Coordinator", Member = "Member", Treasury = "Treasury";
    public static readonly string[] All = [Admin, Coordinator, Member, Treasury, "Stock", "Secretary", "Reviewer"];
    public static bool Coordinates(User user) => user.Role is Admin or Coordinator;
}
public sealed class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string HouseId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Login { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public List<string> RecoveryHashes { get; set; } = [];
    public string Role { get; set; } = Roles.Member;
    public List<string> Permissions { get; set; } = [];
    public bool IsMember { get; set; } = true;
    public bool Active { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
    public int FailedLogins { get; set; }
    public long ScheduleRevision { get; set; }
    public long AuthRevision { get; set; }
    public long AccessRevision { get; set; }
    public long OperationRevision { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class LoginSession
{
    public DateTime ReauthenticatedAt { get; set; } = DateTime.UtcNow;
    // Only a SHA-256 hash of the opaque cookie is persisted.
    public string Id { get; set; } = "";
    public string UserId { get; set; } = "";
    public string HouseId { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}
public sealed class Assignment
{
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Response { get; set; } = "Pending";
    public DateTime? RespondedAt { get; set; }
    public string Participation { get; set; } = "Unverified";
    public bool Dispensed { get; set; }
    public string? VerifiedBy { get; set; }
}
public sealed class CleaningTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public bool Required { get; set; } = true;
    public string? AssignedTo { get; set; }
    public bool ReportedDone { get; set; }
    public bool Verified { get; set; }
    public string? VerifiedBy { get; set; }
}
public sealed class Cleaning
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string HouseId { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string CoordinatorId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Area { get; set; } = "";
    public string Mode { get; set; } = "Team";
    public int? Target { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string Status { get; set; } = "Published";
    // Revision is the document CAS; publication version is the schedule the person accepted.
    public int Revision { get; set; } = 1;
    public int PublicationVersion { get; set; } = 1;
    public bool NeedsScheduleReview { get; set; }
    public string? EventId { get; set; }
    public string? SeriesId { get; set; }
    public int? Minimum { get; set; }
    public int? Maximum { get; set; }
    public List<string> PublicationHistory { get; set; } = [];
    public string? ClosingReason { get; set; }
    public List<Assignment> Assignments { get; set; } = [];
    public List<CleaningTask> Tasks { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class Notification
{
    public string? ReferenceId { get; set; }
    public string Id { get; set; } = "";
    public string HouseId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Path { get; set; } = "/notificacoes";
    public string Category { get; set; } = "Cleaning";
    public string? CleaningId { get; set; }
    public int? PublicationVersion { get; set; }
    public bool SuppressWhenCancelled { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public string PushState { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTime DueAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeaseUntil { get; set; }
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);
}
public sealed class PushDevice
{
    public string Id { get; set; } = "";
    public string HouseId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class PushDelivery
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class AuditEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string HouseId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string Action { get; set; } = "";
    public string ResourceId { get; set; } = "";
    public string? Detail { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}
