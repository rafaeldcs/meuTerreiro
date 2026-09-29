export const RESPONSE_LABELS = Object.freeze({Pending:'Aguardando resposta', Confirmed:'Participação confirmada', Unavailable:'Impedimento informado',NeedsReview:'Aguardando revisão da escala'});
export const STATUS_LABELS = Object.freeze({Published:'Publicada', Cancelled:'Cancelada', Completed:'Concluída',ClosedWithPending:'Encerrada com pendências'});
export const PARTICIPATION_LABELS = Object.freeze({Unverified:'Ainda não verificada', Present:'Participou', Partial:'Participou parcialmente', Absent:'Não participou'});
export function formatDate(value) {
  if (value == null) return "Data indisponível";
  const date = new Date(value);
  if (!Number.isFinite(date.getTime())) return 'Data indisponível';
  return new Intl.DateTimeFormat('pt-BR', {dateStyle:'medium',timeStyle:'short',timeZone:'America/Sao_Paulo'}).format(date);
}
export function isMobile(userAgent = '') { return /Android|iPhone|iPod/i.test(userAgent); }
export function canRespond(cleaning, now = new Date()) {
  return cleaning.status === 'Published' && !cleaning.needsScheduleReview && !cleaning.myDispensed && new Date(cleaning.endsAt) > now && !!cleaning.myResponse;
}
export function coverage(cleaning) {
  return {assigned:cleaning.assigned,confirmed:cleaning.confirmed,pending:cleaning.pending,
    missing:cleaning.mode === 'Team' ? Math.max(0,cleaning.target-cleaning.confirmed) : cleaning.assigned-cleaning.confirmed};
}
export function decodePublicKey(key) {
  const decoded = atob(key.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(key.length/4)*4,'='));
  return Uint8Array.from(decoded, char => char.charCodeAt(0));
}
