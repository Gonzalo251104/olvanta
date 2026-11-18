import type { Models } from '../../shared/workspace'

export const inventory = '/api/v1/branches/{branchId}/inventory'
export const inventoryMoves = '/api/v1/branches/{branchId}/inventory/movements'
export type Inventory = Models['InventoryView']
export type Branch = Models['BranchAccess']
export const movementNames: Record<string, string> = {
  'sale-confirm': 'Reserva de venta',
  'sale-cancel': 'Liberación por anulación',
  'sale-dispatch': 'Despacho de venta',
  'sale-return': 'Devolución de venta',
  receipt: 'Recepción de compra',
  entry: 'Entrada',
  issue: 'Salida',
  transfer: 'Traslado de salida',
  'transfer-in': 'Traslado de entrada',
  reserve: 'Reserva',
  release: 'Liberación de reserva',
  'adjust-in': 'Ajuste positivo',
  'adjust-out': 'Ajuste negativo',
}
export const number = (value: number | string | undefined) =>
  value === undefined ? '—' : Number(value).toLocaleString('es-PE', { maximumFractionDigits: 6 })
export const productName = (data: Inventory, id: number | string) => {
  const product = data.products.find((p) => p.id === id)
  return product ? product.name + ' (' + product.unit + ')' : String(id)
}
export const warehouseName = (data: Inventory, id: number | string) =>
  data.warehouses.find((w) => w.id === id)?.name ?? String(id)
