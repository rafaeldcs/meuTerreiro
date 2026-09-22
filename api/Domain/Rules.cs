using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace Terreiro.Domain;

public sealed class RuleException(string code, string message, int status = 400) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
public static partial class Rules
{
    public static void Require([DoesNotReturnIf(false)] bool value, string code, string message, int status = 400)
    {
        if (!value) throw new RuleException(code, message, status);
    }
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    public static string Login(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        Require(LoginRegex().IsMatch(normalized), "login_invalid", "Use de 3 a 50 letras, números, ponto, hífen ou sublinhado no usuário.");
        return normalized;
    }
    [GeneratedRegex("^[a-z0-9._-]{3,50}$")]
    private static partial Regex LoginRegex();
    public static string Text(string value, int min, int max, string label)
    {
        var trimmed = value.Trim();
        Require(trimmed.Length >= min && trimmed.Length <= max, "text_invalid", $"{label}: use entre {min} e {max} caracteres.");
        return trimmed;
    }
    public static void Password(string value) => Require(value.Length is >= 14 and <= 128,
        "password_invalid", "Use uma senha de 14 a 128 caracteres.");
    public static void Plan(string mode, int? target, DateTime start, DateTime end, DateTime now)
    {
        Require(mode is "Team" or "General", "mode_invalid", "Modalidade inválida.");
        Require(mode != "Team" || target is > 0 and <= 500, "target_invalid", "Defina uma equipe-alvo de 1 a 500 pessoas.");
        Require(start > now && end > start && end - start <= TimeSpan.FromHours(24), "time_invalid", "Informe um horário futuro e duração de até 24 horas.");
    }
    public static void CanRespond(Cleaning cleaning, string userId, int version, string response, DateTime now)
    {
        Require(cleaning.Status == "Published" && !cleaning.NeedsScheduleReview && cleaning.EndsAt > now, "schedule_closed", "Esta escala não aceita mais respostas.", 409);
        Require(cleaning.PublicationVersion == version, "version_changed", "A escala mudou. Atualize a tela antes de responder.", 409);
        Require(cleaning.Assignments.Any(a => a.UserId == userId && !a.Dispensed), "not_assigned", "Você não está nesta escala.", 403);
        Require(response is "Confirmed" or "Unavailable", "response_invalid", "Resposta inválida.");
    }
    public static void CanClose(Cleaning cleaning, DateTime now)
    {
        Require(cleaning.Status == "Published", "schedule_closed", "Atividade já encerrada ou cancelada.", 409);
        Require(now >= cleaning.StartsAt, "not_started", "A atividade ainda não começou.", 409);
        Require(cleaning.Tasks.All(t => !t.Required || t.Verified), "tasks_pending", "Existem tarefas obrigatórias não verificadas.", 409);
    }
    public static bool ValidPushEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length > 0 || uri.Fragment.Length > 0 || endpoint.Length > 4096) return false;
        // Exact transport hosts. Never allow arbitrary user-supplied URLs (SSRF).
        return uri.Host is "fcm.googleapis.com" or "updates.push.services.mozilla.com" or "web.push.apple.com";
    }
    public static bool IsMobile(string userAgent) => userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase)
        || userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("iPod", StringComparison.OrdinalIgnoreCase);
}
