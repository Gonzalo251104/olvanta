import { useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { api } from '../../shared/workspace'
import type { Models } from '../../shared/workspace'
import { inventoryMoves, movementNames, number, productName, warehouseName } from './inventory'
import type { Branch, Inventory } from './inventory'

export function MovementForm({
  data,
  branch,
  initialLine,
  submit,
  busy,
}: {
  data: Inventory
  branch: Branch
  initialLine: string
  busy: boolean
  submit: (job: () => Promise<void>) => void
}) {
  const canWrite = branch.permissions.includes('inventory.write')
  const kinds = [
    ...(canWrite ? ['receipt', 'entry', 'issue', 'transfer'] : []),
    ...(branch.permissions.includes('inventory.reserve') ? ['reserve', 'release'] : []),
    ...(branch.permissions.includes('inventory.adjust') ? ['adjust-in', 'adjust-out'] : []),
  ]
  const pending = data.purchases.flatMap((order) =>
    order.lines
      .filter((l) => Number(l.received) < Number(l.quantity))
      .map((l) => ({ ...l, reference: order.reference })),
  )
  const [kind, setKind] = useState(initialLine ? 'receipt' : canWrite ? 'entry' : 'reserve')
  const [line, setLine] = useState(initialLine)
  const [warehouse, setWarehouse] = useState(String(data.warehouses[0]?.id ?? ''))
  const [product, setProduct] = useState('')
  const [reservation, setReservation] = useState('')
  const [lot, setLot] = useState('')
  const last = useRef({ key: '', id: '' })
  const orderLine = pending.find((l) => String(l.id) === line) ?? pending[0]
  const reserved = data.reservations.find((r) => String(r.id) === reservation) ?? data.reservations[0]
  const products = data.products.filter((p) => p.active || !['entry', 'reserve'].includes(kind))
  const selectedProduct =
    kind === 'receipt'
      ? orderLine?.productId
      : (products.find((p) => String(p.id) === product) ?? products[0])?.id
  const currentBalance = data.balances.find(
    (b) => String(b.warehouseId) === warehouse && b.productId === selectedProduct,
  )
  const inbound = ['entry', 'receipt', 'adjust-in'].includes(kind)
  const release = kind === 'release'
  function save(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const form = e.currentTarget
    const fields = new FormData(form)
    const body: Omit<Models['MovementInput'], 'operationId'> = {
      kind,
      warehouseId: release ? reserved.warehouseId : Number(warehouse),
      productId: release ? reserved.productId : Number(selectedProduct),
      quantity: release ? reserved.quantity : Number(fields.get('quantity')),
      reason: String(fields.get('reason')),
      lot: release ? reserved.lot : lot || null,
      expiry: String(fields.get('expiry') ?? '') || null,
      unitCost: fields.has('cost') ? Number(fields.get('cost')) : null,
      purchaseLineId: kind === 'receipt' ? orderLine.id : null,
      destinationId: kind === 'transfer' ? Number(fields.get('destination')) : null,
      relatedId: release ? reserved.id : fields.get('related') ? Number(fields.get('related')) : null,
    }
    const key = JSON.stringify(body)
    if (last.current.key !== key) last.current = { key, id: crypto.randomUUID() }
    const operationId = last.current.id
    submit(async () => {
      await api.POST(inventoryMoves, {
        params: { path: { branchId: Number(branch.id) } },
        body: { ...body, operationId },
      })
      last.current = { key: '', id: '' }
      form.reset()
      setLot('')
    })
  }
  if (!data.warehouses.length)
    return <p className="panel">Cree un almacén en Almacenes para registrar movimientos.</p>
  return (
    <section className="panel">
      <h2>Registrar movimiento</h2>
      <form className="fields master-editor" onSubmit={save}>
        <label>
          Tipo de movimiento
          <select
            value={kind}
            onChange={(e) => {
              setKind(e.target.value)
              setLot('')
            }}
          >
            {kinds.map((k) => (
              <option key={k} value={k}>
                {movementNames[k]}
              </option>
            ))}
          </select>
        </label>
        {release ? (
          <label>
            Reserva abierta
            <select value={reserved?.id ?? ''} required onChange={(e) => setReservation(e.target.value)}>
              {data.reservations.map((r) => (
                <option key={r.id} value={r.id}>
                  #{r.id} · {warehouseName(data, r.warehouseId)} · {productName(data, r.productId)} ·{' '}
                  {number(r.quantity)} · {r.reason}
                </option>
              ))}
            </select>
          </label>
        ) : (
          <>
            <label>
              Almacén de origen o recepción
              <select
                value={warehouse}
                required
                onChange={(e) => {
                  setWarehouse(e.target.value)
                  setLot('')
                }}
              >
                {data.warehouses.map((w) => (
                  <option key={w.id} value={w.id}>
                    {w.name}
                  </option>
                ))}
              </select>
            </label>
            {kind === 'receipt' ? (
              <label>
                Partida pendiente
                <select value={orderLine?.id ?? ''} required onChange={(e) => setLine(e.target.value)}>
                  {pending.map((l) => (
                    <option key={l.id} value={l.id}>
                      {l.reference} · {productName(data, l.productId)} · pendiente{' '}
                      {number(Number(l.quantity) - Number(l.received))}
                    </option>
                  ))}
                </select>
              </label>
            ) : (
              <label>
                Producto
                <select
                  value={selectedProduct ?? ''}
                  required
                  onChange={(e) => {
                    setProduct(e.target.value)
                    setLot('')
                  }}
                >
                  {products.map((p) => (
                    <option key={p.id} value={p.id}>
                      {productName(data, p.id)}
                    </option>
                  ))}
                </select>
              </label>
            )}
            <label>
              Cantidad en unidad base
              <input
                name="quantity"
                required
                type="number"
                min="0.000001"
                max="99999999"
                step="0.000001"
              />
            </label>
            {inbound ? (
              <label>
                Lote (opcional)
                <input value={lot} maxLength={80} onChange={(e) => setLot(e.target.value)} />
              </label>
            ) : (
              <label>
                Lote disponible
                <select value={lot} onChange={(e) => setLot(e.target.value)}>
                  <option value="">Sin lote</option>
                  {currentBalance?.lots
                    .filter((l) => l.lot)
                    .map((l) => (
                      <option key={l.lot} value={l.lot}>
                        {l.lot} · {number(l.available)} disponibles
                      </option>
                    ))}
                </select>
              </label>
            )}
            {inbound && lot.trim() && (
              <label>
                Vencimiento (opcional)
                <input type="date" name="expiry" />
              </label>
            )}
            {['entry', 'adjust-in'].includes(kind) && (
              <label>
                Costo por unidad ({branch.currency})
                <input name="cost" required type="number" min="0" max="99999999" step="0.000001" />
              </label>
            )}
            {kind === 'transfer' && (
              <label>
                Almacén de destino
                <select name="destination" required>
                  {data.warehouses
                    .filter((w) => String(w.id) !== warehouse)
                    .map((w) => (
                      <option key={w.id} value={w.id}>
                        {w.name}
                      </option>
                    ))}
                </select>
              </label>
            )}
            {kind.startsWith('adjust-') && (
              <label>
                Movimiento compensado (opcional)
                <input type="number" min="1" name="related" />
              </label>
            )}
          </>
        )}
        <label>
          Motivo o documento
          <input name="reason" required maxLength={240} />
        </label>
        <div className="actions">
          <button disabled={busy || (release && !reserved) || (kind === 'receipt' && !orderLine)}>
            Confirmar movimiento
          </button>
        </div>
      </form>
    </section>
  )
}
