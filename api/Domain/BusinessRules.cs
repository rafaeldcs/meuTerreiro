using System.Globalization;
using System.Text.RegularExpressions;
namespace Terreiro.Domain;
public static class Access
{
    public static bool Has(User user, string permission) => user.Permissions.Contains(permission);
    public static void Demand(User user, string permission) => Rules.Require(Has(user, permission), "forbidden", "Você não tem permissão para esta operação.", 403);
    public static bool Finance(User user) => Has(user, "finance.read");
    public static readonly string[] Permissions = ["finance.read", "finance.write", "finance.approve", "stock.read", "stock.write", "stock.approve", "office.read", "office.write", "members.write", "security.manage", "audit.read", "communications.publish"];
    public static List<string> ForRole(string role) => role switch
    {
        Roles.Admin => ["members.write", "security.manage", "office.read", "office.write", "communications.publish"],
        Roles.Treasury => ["finance.read", "finance.write", "finance.approve"],
        "Stock" => ["stock.read", "stock.write", "stock.approve"],
        "Secretary" => ["office.read", "office.write", "communications.publish"],
        "Reviewer" => ["finance.read", "stock.read", "office.read", "audit.read"],
        _ => []
    };
}
public static partial class BusinessRules
{
    public const long MaxCents = 100_000_000_00L;
    public static long Money(long cents, bool allowZero = false)
    {
        Rules.Require(cents >= (allowZero ? 0 : 1) && cents <= MaxCents, "amount_invalid", "Valor monetário fora do limite permitido.");
        return cents;
    }
    public static long Quantity(long milli, bool allowZero = false)
    {
        Rules.Require(milli >= (allowZero ? 0 : 1) && milli <= 1_000_000_000_000L, "quantity_invalid", "Quantidade inválida.");
        return milli;
    }
    public static string Month(string period)
    {
        Rules.Require(Regex.IsMatch(period ?? "", "^[0-9]{4}-(0[1-9]|1[0-2])$") && int.Parse(period![..4], CultureInfo.InvariantCulture) is >= 2000 and <= 2100,
            "period_invalid", "Informe a competência no formato AAAA-MM.");
        return period!;
    }
    public static string Date(string value)
    {
        Rules.Require(DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) && day.Year is >= 2000 and <= 2100,
            "date_invalid", "Informe uma data válida no formato AAAA-MM-DD.");
        return value;
    }
    public static string Today() => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Sao_Paulo").ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public static void NotFuture(string date) => Rules.Require(string.CompareOrdinal(Date(date), Today()) <= 0, "future_payment", "Recebimento ou pagamento confirmado não pode ter data futura.");
    public static void Allocate(Receipt receipt, long amount, Due? due)
    {
        Money(amount);
        Rules.Require(receipt.AvailableCents >= amount, "insufficient_receipt", "O recebimento não possui saldo disponível para esta destinação.", 409);
        if (due != null) Rules.Require(!due.Exempt && !due.Cancelled && due.BalanceCents >= amount, "due_balance", "O valor excede o saldo da mensalidade ou ela não aceita pagamentos.", 409);
    }
    public static void Independent(string requester, string approver) => Rules.Require(requester != approver, "independent_review", "É necessária aprovação por outra pessoa.", 403);
    public static void Revision(int actual, int expected) => Rules.Require(actual == expected, "conflict", "Este registro mudou. Atualize antes de continuar.", 409);
    public static void Reserve(StockLot lot, long amount)
    {
        Quantity(amount);
        Rules.Require(lot.AvailableMilli >= amount, "insufficient_stock", "Estoque disponível insuficiente.", 409);
        Rules.Require(lot.ExpiresOn == null || string.CompareOrdinal(lot.ExpiresOn, Today()) >= 0, "expired_stock", "Este lote está vencido.", 409);
    }
    public static string Reference(string value) => Rules.Text(value, 3, 150, "Referência da operação");
    public static void ApprovalPayload(Approval approval) => Rules.Require(Rules.Hash(approval.PayloadJson) == approval.PayloadHash, "approval_integrity", "Os dados da aprovação foram alterados.", 409);
    public static string Csv(string? value)
    {
        var text = value ?? "";
        if (text.TrimStart().StartsWith('=') || text.TrimStart().StartsWith('+') || text.TrimStart().StartsWith('-') || text.TrimStart().StartsWith('@')) text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
