namespace Terreiro.Domain;

// Monetary amounts are signed integer cents. Quantities use thousandths of the item unit.
// Immutable movements are appended; correction is a linked compensating movement, not deletion.
public abstract class Entity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string HouseId { get; set; } = "";
    public int Revision { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class HouseGate : Entity { public long Sequence { get; set; } }
public sealed class OperationRecord : Entity
{
    public string ActorId { get; set; } = "";
    public string Kind { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string ResultJson { get; set; } = "";
}
public sealed class Account : Entity
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "Bank";
    public long BalanceCents { get; set; }
    public bool Active { get; set; } = true;
}
public sealed class Fund : Entity
{
    public string Name { get; set; } = "";
    public string Purpose { get; set; } = "";
    public long GoalCents { get; set; }
    public long BalanceCents { get; set; }
    public bool Restricted { get; set; } = true;
    public bool Active { get; set; } = true;
}
public sealed class ContributionRule : Entity
{
    public string CreatedBy { get; set; } = "";
    public string Name { get; set; } = "Mensalidade";
    public long AmountCents { get; set; }
    public int DueDay { get; set; } = 10;
    public string EffectiveFrom { get; set; } = "";
    public string? EffectiveTo { get; set; }
    public List<string> MemberIds { get; set; } = [];
    public bool AllActiveMembers { get; set; } = true;
    public bool Active { get; set; } = true;
}
public sealed class Due : Entity
{
    public string MemberId { get; set; } = "";
    public string RuleId { get; set; } = "";
    public string Competence { get; set; } = "";
    public string DueDate { get; set; } = "";
    public long AmountCents { get; set; }
    public long AdjustmentCents { get; set; }
    public long PaidCents { get; set; }
    public bool Exempt { get; set; }
    public string? MemberExemptionId { get; set; }
    public long MemberExemptionAdjustmentCents { get; set; }
    public string? ExemptionReason { get; set; }
    public string? ExemptionApprovalId { get; set; }
    public string? ExemptionApprovedBy { get; set; }
    public DateTime? ExemptionApprovedAt { get; set; }
    public bool Cancelled { get; set; }
    public long BalanceCents => AmountCents + AdjustmentCents - PaidCents;
    public string State => Cancelled ? "Cancelled" : Exempt ? "Exempt" : BalanceCents == 0 ? "Paid" : PaidCents > 0 ? "Partial" : "Open";
}
public sealed class Receipt : Entity
{
    public string AccountId { get; set; } = "";
    public long AmountCents { get; set; }
    public long AllocatedCents { get; set; }
    public long RefundedCents { get; set; }
    public long AvailableCents => AmountCents - AllocatedCents - RefundedCents;
    public string FinancialReference { get; set; } = "";
    public string Verification { get; set; } = "Treasury";
    public string VerifiedBy { get; set; } = "";
    public string? MemberId { get; set; }
    public string PayerName { get; set; } = "";
    public string OccurredOn { get; set; } = "";
    public string VerificationNote { get; set; } = "";
    public string? CashSessionId { get; set; }
    public string? EvidenceId { get; set; }
}
public sealed class Allocation : Entity
{
    public string ReceiptId { get; set; } = "";
    public string? DueId { get; set; }
    public string? FundId { get; set; }
    public string? MemberId { get; set; }
    public string Kind { get; set; } = "Due";
    public long AmountCents { get; set; }
    public long ReversedCents { get; set; }
    public string CreatedBy { get; set; } = "";
}
public sealed class LedgerEntry : Entity
{
    public string AccountId { get; set; } = "";
    public string? FundId { get; set; }
    public string? EventId { get; set; }
    public long SignedCents { get; set; }
    public string Kind { get; set; } = "";
    public string Category { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string? ReversalOf { get; set; }
    public string OccurredOn { get; set; } = "";
    public string Description { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string? FinancialReference { get; set; }
    public string? StatementLineId { get; set; }
}
public sealed class Approval : Entity
{
    public string Type { get; set; } = "";
    public string TargetId { get; set; } = "";
    public int TargetRevision { get; set; }
    public string RequestedBy { get; set; } = "";
    public string? ReviewedBy { get; set; }
    public string Reason { get; set; } = "";
    public string? ReviewNote { get; set; }
    public string PayloadJson { get; set; } = "";
    public string PayloadHash { get; set; } = "";
    public string State { get; set; } = "Pending";
    public DateTime? ReviewedAt { get; set; }
}
public sealed class Expense : Entity
{
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "Expense"; // Expense, Reimbursement, Advance
    public string Category { get; set; } = "Outros";
    public long AmountCents { get; set; }
    public long PaidCents { get; set; }
    public string DueDate { get; set; } = "";
    public string RequestedBy { get; set; } = "";
    public string Beneficiary { get; set; } = "";
    public string? SupplierId { get; set; }
    public string? EvidenceId { get; set; }
    public string? FundId { get; set; }
    public string? EventId { get; set; }
    public string? PurchaseId { get; set; }
    public string State { get; set; } = "PendingApproval";
    public string? ApprovedBy { get; set; }
    public string? ApprovalId { get; set; }
    public long SettledAdvanceCents { get; set; }
    public long ReturnedAdvanceCents { get; set; }
}
public sealed class ExpensePayment : Entity
{
    public string ExpenseId { get; set; } = "";
    public string AccountId { get; set; } = "";
    public long AmountCents { get; set; }
    public string FinancialReference { get; set; } = "";
    public string OccurredOn { get; set; } = "";
    public string? CashSessionId { get; set; }
    public string? EvidenceId { get; set; }
}
public sealed class Refund : Entity
{
    public string ReceiptId { get; set; } = "";
    public long AmountCents { get; set; }
    public string ApprovalId { get; set; } = "";
    public string Reason { get; set; } = "";
    public string State { get; set; } = "Approved";
    public string? FinancialReference { get; set; }
    public string? EvidenceId { get; set; }
}
public sealed class CashSession : Entity
{
    public string AccountId { get; set; } = "";
    public string OperatorId { get; set; } = "";
    public long OpeningCents { get; set; }
    public long? ExpectedCents { get; set; }
    public long? CountedCents { get; set; }
    public long? DifferenceCents { get; set; }
    public string State { get; set; } = "Open";
    public string? ReviewedBy { get; set; }
    public string Note { get; set; } = "";
}
public sealed class Closing : Entity
{
    public string Period { get; set; } = "";
    public string State { get; set; } = "PendingReview";
    public string PreparedBy { get; set; } = "";
    public string? ReviewedBy { get; set; }
    public string SnapshotJson { get; set; } = "";
    public string Note { get; set; } = "";
    public List<string> PreviousSnapshots { get; set; } = [];
}
public sealed class StatementBatch : Entity
{
    public string AccountId { get; set; } = "";
    public string OriginalCsv { get; set; } = "";
    public string FileHash { get; set; } = "";
    public string ImportedBy { get; set; } = "";
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public long OpeningCents { get; set; }
    public long ClosingCents { get; set; }
}
public sealed class StatementLine : Entity
{
    public string AccountId { get; set; } = "";
    public string BatchId { get; set; } = "";
    public string FinancialReference { get; set; } = "";
    public string OccurredOn { get; set; } = "";
    public long SignedCents { get; set; }
    public string Description { get; set; } = "";
    public string? LedgerId { get; set; }
    public string? MatchedBy { get; set; }
}
public sealed class Evidence : Entity
{
    public string UploadedBy { get; set; } = "";
    public string MemberId { get; set; } = "";
    public string Purpose { get; set; } = "Receipt";
    public List<string> DueIds { get; set; } = [];
    public string OriginalName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string ObjectKey { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Length { get; set; }
    public string ScanState { get; set; } = "Quarantine";
    public string AnalysisState { get; set; } = "Pending";
    public int Attempts { get; set; }
    public string? ExtractedText { get; set; }
    public long? DeclaredCents { get; set; }
    public string? DeclaredDate { get; set; }
    public string? ExtractedAmount { get; set; }
    public string? ExtractedReference { get; set; }
    public bool PossibleScheduled { get; set; }
    public int? PageCount { get; set; }
    public string ReviewState { get; set; } = "AwaitingVerification";
    public string? ReceiptId { get; set; }
    public string? DuplicateOf { get; set; }
    public string? AnalysisNote { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public DateTime? PurgedAt { get; set; }
    public DateTime? RetainUntil { get; set; }
}
public sealed class ReviewCase : Entity
{
    public string EvidenceId { get; set; } = "";
    public string MemberId { get; set; } = "";
    public string ReasonCode { get; set; } = "";
    public string State { get; set; } = "Open";
    public string? Resolution { get; set; }
    public List<CaseMessage> Messages { get; set; } = [];
}
public sealed class CaseMessage
{
    public string AuthorId { get; set; } = "";
    public string Text { get; set; } = "";
    public bool Internal { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}
public sealed class Supplier : Entity
{
    public string Name { get; set; } = "";
    public string Contact { get; set; } = "";
    public string Document { get; set; } = "";
    public string PaymentDetails { get; set; } = "";
    public bool Active { get; set; } = true;
}
public sealed class Item : Entity
{
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "un";
    public string Category { get; set; } = "Limpeza";
    public long MinimumMilli { get; set; }
    public bool Reusable { get; set; }
    public bool Active { get; set; } = true;
}
public sealed class StockLot : Entity
{
    public string ItemId { get; set; } = "";
    public string Lot { get; set; } = "";
    public string Location { get; set; } = "Almoxarifado";
    public string? ExpiresOn { get; set; }
    public long OnHandMilli { get; set; }
    public long ReservedMilli { get; set; }
    public long AvailableMilli => OnHandMilli - ReservedMilli;
}
public sealed class StockMovement : Entity
{
    public string ItemId { get; set; } = "";
    public string LotId { get; set; } = "";
    public string Kind { get; set; } = "";
    public long QuantityMilli { get; set; }
    public string SourceId { get; set; } = "";
    public string Reason { get; set; } = "";
    public string ActorId { get; set; } = "";
}
public sealed class Reservation : Entity
{
    public string LotId { get; set; } = "";
    public string ItemId { get; set; } = "";
    public string ActivityId { get; set; } = "";
    public string ActivityKind { get; set; } = "Cleaning";
    public long QuantityMilli { get; set; }
    public long IssuedMilli { get; set; }
    public long ConsumedMilli { get; set; }
    public long ReturnedMilli { get; set; }
    public long LostMilli { get; set; }
    public string State { get; set; } = "Reserved";
}
public sealed class Purchase : Entity
{
    public string Title { get; set; } = "";
    public string SupplierId { get; set; } = "";
    public string RequestedBy { get; set; } = "";
    public string? FundId { get; set; }
    public string? EventId { get; set; }
    public string DueDate { get; set; } = "";
    public long FreightCents { get; set; }
    public string State { get; set; } = "Draft";
    public string? ExpenseId { get; set; }
    public string? ApprovedBy { get; set; }
    public List<PurchaseLine> Lines { get; set; } = [];
}
public sealed class PurchaseLine
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ItemId { get; set; } = "";
    public long QuantityMilli { get; set; }
    public long ReceivedMilli { get; set; }
    public long TotalCents { get; set; }
}
public sealed class Donation : Entity
{
    public string Kind { get; set; } = "Material";
    public string Description { get; set; } = "";
    public string? DonorId { get; set; }
    public string DonorName { get; set; } = "";
    public string? FundId { get; set; }
    public string? ReceiptId { get; set; }
    public string? ItemId { get; set; }
    public long QuantityMilli { get; set; }
    public long ReceivedMilli { get; set; }
    public long EstimatedCents { get; set; }
    public string State { get; set; } = "Promised";
}
public sealed class HouseEvent : Entity
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Location { get; set; } = "";
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string CoordinatorId { get; set; } = "";
    public List<string> ParticipantIds { get; set; } = [];
    public long BudgetCents { get; set; }
    public string State { get; set; } = "Planned";
}
public sealed class Asset : Entity
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Ownership { get; set; } = "House";
    public string Location { get; set; } = "";
    public string Condition { get; set; } = "Good";
    public string? LoanedTo { get; set; }
    public string? ReturnDue { get; set; }
    public long EstimatedCents { get; set; }
    public string State { get; set; } = "Available";
}
public sealed class Maintenance : Entity
{
    public string? AssetId { get; set; }
    public string Title { get; set; } = "";
    public string DueDate { get; set; } = "";
    public string ResponsibleId { get; set; } = "";
    public string State { get; set; } = "Open";
    public string Note { get; set; } = "";
    public string? ExpenseId { get; set; }
}
public sealed class AdministrativeTask : Entity
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string ResponsibleId { get; set; } = "";
    public string DueDate { get; set; } = "";
    public string? EventId { get; set; }
    public string State { get; set; } = "Open";
}
public sealed class DocumentRecord : Entity
{
    public string Title { get; set; } = "";
    public string Category { get; set; } = "Institucional";
    public string EvidenceId { get; set; } = "";
    public string ResponsibleId { get; set; } = "";
    public string? ReviewDate { get; set; }
}
public sealed class Budget : Entity
{
    public string Period { get; set; } = "";
    public string Category { get; set; } = "";
    public long PlannedIncomeCents { get; set; }
    public long PlannedExpenseCents { get; set; }
    public string State { get; set; } = "Draft";
    public string CreatedBy { get; set; } = "";
    public string? ApprovedBy { get; set; }
}
public sealed class Announcement : Entity
{
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string AuthorId { get; set; } = "";
    public List<string> RecipientIds { get; set; } = [];
    public bool RequireAcknowledgement { get; set; }
    public int PublicationVersion { get; set; } = 1;
    public string State { get; set; } = "Published";
}
public sealed class Acknowledgement : Entity
{
    public string AnnouncementId { get; set; } = "";
    public string UserId { get; set; } = "";
    public int PublicationVersion { get; set; }
}
public sealed class NotificationPreference : Entity
{
    public string UserId { get; set; } = "";
    public bool PushEnabled { get; set; } = true;
    public List<string> MutedCategories { get; set; } = [];
    public int QuietStartHour { get; set; } = 22;
    public int QuietEndHour { get; set; } = 7;
}
public sealed class CleaningSwap : Entity
{
    public string CleaningId { get; set; } = "";
    public int PublicationVersion { get; set; }
    public string OriginalId { get; set; } = "";
    public string SubstituteId { get; set; } = "";
    public string State { get; set; } = "AwaitingAcceptance";
    public string? ApprovedBy { get; set; }
}
public sealed class CleaningSeries : Entity
{
    public string Title { get; set; } = "";
    public string Area { get; set; } = "";
    public string CoordinatorId { get; set; } = "";
    public string Mode { get; set; } = "Team";
    public int? Target { get; set; }
    public int IntervalWeeks { get; set; } = 1;
    public DateTime NextStartsAt { get; set; }
    public int DurationMinutes { get; set; }
    public DateTime Until { get; set; }
    public List<string> MemberIds { get; set; } = [];
    public List<string> Tasks { get; set; } = [];
    public bool Active { get; set; } = true;
}
public sealed class MemberProfile : Entity
{
    public string UserId { get; set; } = "";
    public string State { get; set; } = "Active";
    public string JoinedOn { get; set; } = "";
    public string? LeftOn { get; set; }
    public List<string> GroupIds { get; set; } = [];
}
public sealed class PaymentIntent : Entity
{
    public string AccountId { get; set; }="";
    public string ProviderEnvironment { get; set; }="Sandbox";
    public DateTime NextCheckAt { get; set; }=DateTime.UtcNow;
    public DateTime? LeaseUntil { get; set; }
    public List<string> ReceiptIds { get; set; }=[];
    public string MemberId { get; set; } = "";
    public List<string> DueIds { get; set; } = [];
    public long AmountCents { get; set; }
    public string Txid { get; set; } = "";
    public string State { get; set; } = "PendingCreation";
    public string? CopyPaste { get; set; }
    public string? ReceiptId { get; set; }
    public string? ErrorCode { get; set; }
}
public sealed class AccessChange : Entity
{
    public string TargetUserId { get; set; } = "";
    public string RequestedBy { get; set; } = "";
    public string? ReviewedBy { get; set; }
    public long TargetAuthRevision { get; set; }
    public string NewRole { get; set; } = Roles.Member;
    public bool Active { get; set; } = true;
    public bool IsMember { get; set; } = true;
    public string Reason { get; set; } = "";
    public string State { get; set; } = "Pending";
}

public sealed class BankObservation : Entity
{
    public string IntentId { get; set; }="";
    public string AccountId { get; set; }="";
    public string FinancialReference { get; set; }="";
    public long AmountCents { get; set; }
    public string OccurredOn { get; set; }="";
    public string State { get; set; }="Pending";
    public string? ReceiptId { get; set; }
    public long ProviderRefundedCents { get; set; }
    public string Note { get; set; }="";
    public DateTime LastCheckedAt { get; set; }=DateTime.UtcNow;
}
