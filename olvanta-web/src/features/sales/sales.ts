import type { Models } from '../../shared/workspace'

export const salesPath = '/api/v1/branches/{branchId}/sales'
export const cashPath = '/api/v1/branches/{branchId}/sales/cash'
export const catalogPath = '/api/v1/branches/{branchId}/sales/catalog'
export type Branch = Models['BranchAccess']
export type Sale = Models['SaleDetail']
export type Cash = Models['CashDetail']
export type Command = Omit<Models['TradeInput'], 'requestId'>
export type Quote = Omit<Models['QuoteInput'], 'requestId'>
export type Run = (input: Command) => void
export const money = (value: number | string | null | undefined, currency: string) =>
  value == null ? '—' : Number(value).toLocaleString('es-PE', { style: 'currency', currency })
export const stages: Record<string, string> = {
  quote: 'Cotización',
  order: 'Pedido',
  confirmed: 'Confirmada',
  dispatched: 'Despachada',
  cancelled: 'Anulada',
}
export const operations: Record<string, string> = {
  quote: 'Cotización',
  order: 'Pedido',
  confirm: 'Confirmación',
  cancel: 'Anulación',
  dispatch: 'Despacho',
  pay: 'Cobro',
  return: 'Devolución',
  refund: 'Reembolso',
  open: 'Apertura',
  close: 'Cierre',
  'cash-in': 'Ingreso de efectivo',
  'deposit-in': 'Garantía recibida',
  'deposit-out': 'Garantía devuelta',
  'cash-out': 'Retiro de efectivo',
}
export const methods: Record<string, string> = {
  cash: 'Efectivo',
  card: 'Tarjeta',
  transfer: 'Transferencia',
}
