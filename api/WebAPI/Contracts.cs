namespace Terreiro.WebAPI;
public sealed record LoginInput(string Login, string Password);
public sealed record PasswordInput(string CurrentPassword, string NewPassword);
public sealed record MemberInput(string Name, string Login, string Password, string Role);
public sealed record ResponseInput(int Version, string Response);
public sealed record VerifyInput(int Revision, string? TaskId, string? MemberId, string? Participation);
public sealed record FinishInput(int Revision, bool Cancel, string Reason);
public sealed record SubscriptionKeys(string P256dh, string Auth);
public sealed record SubscriptionInput(string Endpoint, SubscriptionKeys Keys);
