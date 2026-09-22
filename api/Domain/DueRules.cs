namespace Terreiro.Domain;

/// <summary>Presentation never mutates PaidCents, creates a receipt or moves cash.</summary>
public static class DueRules
{
    public static long FullExemptionDelta(Due due)
    {
        Rules.Require(!due.Cancelled && !due.Exempt, "due_closed", "Mensalidade cancelada ou já isenta.", 409);
        Rules.Require(due.PaidCents == 0, "due_has_payment", "Já existe pagamento nesta mensalidade. Revise o valor recebido antes de solicitar isenção integral.", 409);
        Rules.Require(due.BalanceCents > 0, "due_settled", "Esta mensalidade já está regularizada.", 409);
        return -checked(due.AmountCents + due.AdjustmentCents);
    }
    public static void ValidateAdjustment(Due due, long delta, bool exempt, bool cancelled)
    {
        Rules.Require(!(exempt && cancelled), "adjustment_state", "Isenção e cancelamento são situações diferentes.");
        Rules.Require(delta >= -BusinessRules.MaxCents && delta <= BusinessRules.MaxCents, "amount_invalid", "Ajuste inválido.");
        var effective = checked(due.AmountCents + due.AdjustmentCents + delta);
        Rules.Require(effective >= due.PaidCents && effective >= 0, "adjustment_balance", "O ajuste não pode apagar pagamentos; revise o recebimento primeiro.", 409);
        Rules.Require(!(exempt || cancelled) || effective == 0, "adjustment_state", "Isenção integral ou cancelamento exige valor ajustado zero.");
    }
    public static bool CanRead(User actor, Due due) => actor.HouseId == due.HouseId && (Access.Finance(actor) || actor.Id == due.MemberId);
    public static string DisplayState(Due due) => due.State switch
    {
        "Exempt" when due.BalanceCents == 0 && due.PaidCents == 0 => "Pago · Isento",
        "Exempt" => "Isenção a revisar",
        "Paid" => "Pago",
        "Partial" => "Pagamento parcial",
        "Cancelled" => "Cancelado",
        _ => "Em aberto"
    };
    public static void ValidateFilters(int? year, int? month, string state, string query)
    {
        Rules.Require(year is null or >= 2000 and <= 2100, "year_invalid", "Ano fora do intervalo suportado.");
        Rules.Require(month is null or >= 1 and <= 12, "month_invalid", "Mês inválido.");
        Rules.Require(state is "all" or "paid" or "exempt" or "pending" or "partial" or "cancelled", "state_invalid", "Filtro de situação inválido.");
        Rules.Require(query.Length <= 100, "query_invalid", "Use até 100 caracteres na busca.");
    }
}
