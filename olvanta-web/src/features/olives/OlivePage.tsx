import { useRef, useState } from 'react'
import { Link } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, type Models } from '../../shared/workspace'
import { inventory, number } from '../inventory/inventory'
import { money } from '../sales/sales'

const route = '/api/v1/branches/{branchId}/olives'
const traceRoute = '/api/v1/branches/{branchId}/olives/lots/{id}/trace'
type Desk = Models['OliveDesk']
type Branch = Models['BranchAccess']
const field = (f: FormData, name: string) => String(f.get(name) ?? '').trim()
const amount = (f: FormData, name: string) => Number(field(f, name))
const kg = (n: number | string) => number(n) + ' kg'
const bulk = (unit: string) => ['KG', 'KGM', 'KILO'].includes(unit.trim().toUpperCase())
const description = (lot: Models['OliveLotView']) => lot.code + ' · ' + lot.product

function WeightedReceipt({
  branch,
  data,
  busy,
  save,
}: {
  branch: Branch
  data: Desk
  busy: boolean
  save: (payload: Omit<Models['MovementInput'], 'operationId'>) => void
}) {
  const [orderId, setOrder] = useState<number>()
  const [lineId, setLine] = useState<number>()
  const [gross, setGross] = useState('')
  const [tare, setTare] = useState('0')
  const orders = useQuery({
    queryKey: ['inventory', branch.id],
    queryFn: async () =>
      (await api.GET(inventory, { params: { path: { branchId: Number(branch.id) } } })).data!,
  })
  const bulkIds = new Set(
    data.products.filter((p) => bulk(p.unit) && Number(p.kgPerUnit) === 1).map((p) => p.id),
  )
  const pending = (line: Models['OrderLineView']) =>
    bulkIds.has(line.productId) && Number(line.quantity) > Number(line.received)
  const purchases = orders.data?.purchases.filter((o) => o.lines.some(pending)) ?? []
  const order = purchases.find((o) => Number(o.id) === orderId) ?? purchases[0]
  const lines = order?.lines.filter(pending) ?? []
  const line = lines.find((l) => Number(l.id) === lineId) ?? lines[0]
  const net = Number((Number(gross) - Number(tare)).toFixed(6))
  if (orders.error) return <p role="alert">{orders.error.message}</p>
  if (orders.isPending) return <p>Consultando compras…</p>
  if (!line || !data.warehouses.length)
    return (
      <p>
        Registre una compra pendiente de un producto en kg con factor 1 y un almacén.{' '}
        <Link to="/inventario">Compras e inventario</Link>
      </p>
    )
  return (
    <form
      className="panel fields"
      onSubmit={(e) => {
        e.preventDefault()
        const f = new FormData(e.currentTarget)
        save({
          kind: 'receipt',
          warehouseId: amount(f, 'warehouse'),
          productId: line.productId,
          purchaseLineId: line.id,
          quantity: net,
          reason: 'Recepción pesada',
          lot: field(f, 'lot'),
          expiry: field(f, 'expiry') || null,
          olive: {
            gross: Number(gross),
            tare: Number(tare),
            variety: field(f, 'variety'),
            grade: field(f, 'grade'),
            origin: field(f, 'origin'),
            supplierLot: field(f, 'supplierLot'),
          },
        })
      }}
    >
      <h2>Recepción pesada</h2>
      <div className="olive-grid">
        <label>
          Compra
          <select
            value={order?.id}
            onChange={(e) => {
              setOrder(Number(e.target.value))
              setLine(undefined)
            }}
          >
            {purchases.map((p) => (
              <option key={p.id} value={p.id}>
                {p.reference} · {p.supplier}
              </option>
            ))}
          </select>
        </label>
        <label>
          Partida de compra
          <select value={line.id} onChange={(e) => setLine(Number(e.target.value))}>
            {lines.map((l) => (
              <option key={l.id} value={l.id}>
                {data.products.find((p) => p.id === l.productId)?.name} · pendiente{' '}
                {kg(Number(l.quantity) - Number(l.received))}
              </option>
            ))}
          </select>
        </label>
        <label>
          Almacén
          <select name="warehouse">
            {data.warehouses.map((w) => (
              <option key={w.id} value={w.id}>
                {w.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Lote interno
          <input name="lot" maxLength={80} required />
        </label>
        <label>
          Lote del proveedor
          <input name="supplierLot" maxLength={80} required />
        </label>
        <label>
          Procedencia
          <input name="origin" maxLength={160} required />
        </label>
        <label>
          Variedad
          <input name="variety" maxLength={80} required />
        </label>
        <label>
          Clasificación comercial
          <input name="grade" maxLength={80} required />
        </label>
        <label>
          Peso bruto (kg)
          <input
            type="number"
            min=".000001"
            step=".000001"
            value={gross}
            onChange={(e) => setGross(e.target.value)}
            required
          />
        </label>
        <label>
          Tara (kg)
          <input
            type="number"
            min="0"
            step=".000001"
            value={tare}
            onChange={(e) => setTare(e.target.value)}
            required
          />
        </label>
        <label>
          Vencimiento
          <input type="date" name="expiry" />
        </label>
      </div>
      <p>
        Peso neto <strong>{kg(net)}</strong>
      </p>
      <button disabled={busy || net <= 0 || net > Number(line.quantity) - Number(line.received)}>
        Registrar recepción
      </button>
    </form>
  )
}

function Repacking({
  data,
  busy,
  save,
}: {
  data: Desk
  busy: boolean
  save: (payload: Omit<Models['OliveRepackInput'], 'operationId'>) => void
}) {
  const active = data.stock.filter((s) => Number(s.available) > 0)
  const [selection, setSelection] = useState('')
  const [qty, setQty] = useState('')
  const [loss, setLoss] = useState('0')
  const [outputs, setOutputs] = useState([{ key: 0, productId: '', code: '', quantity: '' }])
  const sequence = useRef(1)
  const keyOf = (s: Models['OliveStock']) => s.lotId + '-' + s.warehouseId
  const stock = active.find((s) => keyOf(s) === selection) ?? active[0]
  const source = data.lots.find((l) => l.id === stock?.lotId)
  const catalog = data.products.filter((p) =>
    bulk(p.unit) ? Number(p.kgPerUnit) === 1 : !!p.presentation,
  )
  const weightOf = (row: (typeof outputs)[number]) =>
    Number(row.quantity) * Number(catalog.find((p) => String(p.id) === row.productId)?.kgPerUnit ?? 0)
  const totalOut = outputs.reduce((total, row) => total + weightOf(row), 0)
  const inputWeight = Number(qty) * Number(source?.kgPerUnit ?? 0)
  const difference = Number((inputWeight - totalOut - Number(loss)).toFixed(6))
  const edit = (key: number, change: Partial<(typeof outputs)[number]>) =>
    setOutputs((rows) => rows.map((row) => (row.key === key ? { ...row, ...change } : row)))
  if (!source || !stock)
    return <p>Registre o traslade un lote pesado para disponer de existencias en este módulo.</p>
  return (
    <form
      className="panel fields"
      onSubmit={(e) => {
        e.preventDefault()
        save({
          warehouseId: stock.warehouseId,
          sourceId: source.id,
          quantity: Number(qty),
          lossKg: Number(loss),
          reason: field(new FormData(e.currentTarget), 'reason'),
          outputs: outputs.map((r) => ({
            productId: Number(r.productId),
            code: r.code,
            quantity: Number(r.quantity),
          })),
        })
      }}
    >
      <h2>Reenvasado o merma</h2>
      <label>
        Lote de origen
        <select
          value={keyOf(stock)}
          onChange={(e) => {
            setSelection(e.target.value)
            setQty('')
          }}
        >
          {active.map((s) => {
            const lot = data.lots.find((l) => l.id === s.lotId)!
            return (
              <option key={keyOf(s)} value={keyOf(s)}>
                {description(lot)} · {data.warehouses.find((w) => w.id === s.warehouseId)?.name} ·{' '}
                {number(s.available)} {lot.unit} disponibles
              </option>
            )
          })}
        </select>
      </label>
      <div className="olive-grid">
        <label>
          Cantidad consumida ({source.unit})
          <input
            type="number"
            min={source.wholeUnits ? 1 : '.000001'}
            max={stock.available}
            step={source.wholeUnits ? 1 : '.000001'}
            value={qty}
            onChange={(e) => setQty(e.target.value)}
            required
          />
        </label>
        <label>
          Merma pesada (kg)
          <input
            type="number"
            min="0"
            step=".000001"
            value={loss}
            onChange={(e) => setLoss(e.target.value)}
            required
          />
        </label>
      </div>
      <h3>Presentaciones resultantes</h3>
      {outputs.map((row, index) => {
        const product = catalog.find((p) => String(p.id) === row.productId)
        const isBulk = product && bulk(product.unit)
        return (
          <fieldset key={row.key} className="olive-grid">
            <legend>
              Salida {index + 1} · {kg(weightOf(row))}
            </legend>
            <label>
              Producto de salida
              <select
                value={row.productId}
                onChange={(e) => edit(row.key, { productId: e.target.value })}
                required
              >
                <option value="">Seleccione</option>
                {catalog.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name} · {p.presentation || 'Granel'} · {kg(p.kgPerUnit)} por {p.unit}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Lote resultante
              <input
                maxLength={80}
                value={row.code}
                onChange={(e) => edit(row.key, { code: e.target.value })}
                required
              />
            </label>
            <label>
              Cantidad resultante
              <input
                type="number"
                min={isBulk ? '.000001' : 1}
                step={isBulk ? '.000001' : 1}
                value={row.quantity}
                onChange={(e) => edit(row.key, { quantity: e.target.value })}
                required
              />
            </label>
            <button
              type="button"
              className="secondary"
              onClick={() => setOutputs((rows) => rows.filter((r) => r.key !== row.key))}
            >
              Quitar salida
            </button>
          </fieldset>
        )
      })}
      {!outputs.length && (
        <p>Registre el peso completo como merma si no queda ninguna presentación aprovechable.</p>
      )}
      <button
        type="button"
        className="secondary"
        disabled={outputs.length >= 30}
        onClick={() => {
          const key = sequence.current++
          setOutputs((rows) => [...rows, { key, productId: '', code: '', quantity: '' }])
        }}
      >
        Añadir salida
      </button>
      <label>
        Motivo y observaciones
        <input name="reason" maxLength={240} required />
      </label>
      <div className="olive-weights">
        <span>
          Consumido <strong>{kg(inputWeight)}</strong>
        </span>
        <span>
          Resultante <strong>{kg(totalOut)}</strong>
        </span>
        <span>
          Merma <strong>{kg(loss)}</strong>
        </span>
        <span>
          Diferencia <strong>{kg(difference)}</strong>
        </span>
      </div>
      <button disabled={busy || difference !== 0 || inputWeight <= 0}>Registrar reenvasado</button>
    </form>
  )
}

function TracePanel({ branch, data }: { branch: Branch; data: Desk }) {
  const [customer, setCustomer] = useState<number>()
  const [lot, setLot] = useState<number>()
  const params = { path: { branchId: Number(branch.id) } }
  const family = useQuery({
    queryKey: ['olives', branch.id, 'trace', lot],
    enabled: lot != null,
    queryFn: async () =>
      (await api.GET(traceRoute, { params: { path: { ...params.path, id: lot! } } })).data!,
  })
  const deliveries = useQuery({
    queryKey: ['olives', branch.id, 'deliveries', customer],
    enabled: customer != null,
    queryFn: async () =>
      (
        await api.GET('/api/v1/branches/{branchId}/olives/deliveries', {
          params: { ...params, query: { customerId: customer! } },
        })
      ).data!,
  })
  const result = family.data
  const origin = result?.lots.find((l) => l.id === result.rootId)
  const lotName = (id: number | string) => {
    const found = data.lots.find((l) => l.id === id)
    return found ? description(found) : '#' + id
  }
  return (
    <div className="trade-layout">
      <aside className="panel fields">
        <h2>Buscar por lote o cliente</h2>
        <label>
          Lote
          <select value={lot ?? ''} onChange={(e) => setLot(Number(e.target.value) || undefined)}>
            <option value="">Seleccione</option>
            {data.lots.map((l) => (
              <option key={l.id} value={l.id}>
                {description(l)}
              </option>
            ))}
          </select>
        </label>
        {!!data.customers.length && (
          <label>
            Cliente
            <select
              value={customer ?? ''}
              onChange={(e) => {
                setCustomer(Number(e.target.value) || undefined)
                setLot(undefined)
              }}
            >
              <option value="">Seleccione</option>
              {data.customers.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </label>
        )}
        {deliveries.error && <p role="alert">{deliveries.error.message}</p>}
        {deliveries.data && (
          <ul className="trade-list">
            {deliveries.data.map((d) => (
              <li key={d.movementId}>
                <button className="linkbtn" onClick={() => setLot(Number(d.lotId))}>
                  {lotName(d.lotId)}
                </button>
                <small>
                  Venta #{d.saleId} · {d.kind === 'sale-dispatch' ? 'Despacho' : 'Devolución'} ·{' '}
                  {kg(Math.abs(Number(d.kg)))}
                </small>
              </li>
            ))}
          </ul>
        )}
        {deliveries.data?.length === 0 && <p>El cliente aún no tiene despachos de lotes trazables.</p>}
      </aside>
      <section className="panel fields">
        <h2>Ruta del lote</h2>
        {family.error && <p role="alert">{family.error.message}</p>}
        {!result && (
          <p>
            {family.isFetching
              ? 'Consultando movimientos…'
              : 'Seleccione un lote o un despacho del cliente.'}
          </p>
        )}
        {result && origin && (
          <>
            <header>
              <h3>{description(origin)}</h3>
              <p>
                {origin.variety} · {origin.grade} · {origin.origin}
              </p>
              <p>
                {origin.supplier} · Lote de proveedor {origin.supplierLot}
              </p>
              <p>
                Compra {result.receipt.reference} · Recepción #{result.receipt.receiptId} ·{' '}
                {new Date(result.receipt.time).toLocaleDateString('es-PE')}
              </p>
              {origin.expiry && <p>Vence {origin.expiry}</p>}
            </header>
            <div className="olive-weights">
              <span>
                Bruto <strong>{kg(origin.gross ?? 0)}</strong>
              </span>
              <span>
                Tara <strong>{kg(origin.tare ?? 0)}</strong>
              </span>
              <span>
                Neto <strong>{kg(origin.net ?? 0)}</strong>
              </span>
              <span>
                Valor recibido <strong>{money(result.receipt.value, branch.currency)}</strong>
              </span>
            </div>
            <h3>Reenvasados y mermas</h3>
            {result.repacking.length ? (
              <ol className="trade-history">
                {result.repacking.map((r) => (
                  <li key={r.id}>
                    <strong>
                      #{r.id} · {lotName(r.sourceId)}
                    </strong>
                    <p>
                      {kg(r.inputKg)} consumidos · {kg(r.lossKg)} de merma ·{' '}
                      {money(r.lossValue, branch.currency)} de valor retirado
                    </p>
                    <p>
                      {r.reason} · {r.actor}
                    </p>
                    <ul>
                      {result.outputs
                        .filter((o) => o.processId === r.id)
                        .map((o) => {
                          const l = result.lots.find((item) => item.id === o.lotId)!
                          return (
                            <li key={l.id}>
                              {description(l)} · {number(o.quantity)} {l.unit} · {kg(o.kg)} ·{' '}
                              {money(o.value, branch.currency)}
                            </li>
                          )
                        })}
                    </ul>
                  </li>
                ))}
              </ol>
            ) : (
              <p>El lote se conserva en su presentación de recepción.</p>
            )}
            <h3>Despachos y devoluciones</h3>
            {result.deliveries.length ? (
              <ul className="trade-list">
                {result.deliveries.map((d) => (
                  <li key={d.movementId}>
                    {d.kind === 'sale-dispatch' ? 'Despacho' : 'Devolución'} · Venta #{d.saleId} ·{' '}
                    {d.customer} · {kg(Math.abs(Number(d.kg)))}
                    <small>{lotName(d.lotId)}</small>
                  </li>
                ))}
              </ul>
            ) : (
              <p>Aún no hay despachos para este lote.</p>
            )}
          </>
        )}
      </section>
    </div>
  )
}

export default function OlivePage({ branch }: { branch: Branch }) {
  const cache = useQueryClient()
  const [tab, setTab] = useState<'stock' | 'receipt' | 'repack' | 'trace'>('stock')
  const keys = useRef(new Map<string, string>())
  const params = { path: { branchId: Number(branch.id) } }
  const desk = useQuery({
    queryKey: ['olives', branch.id],
    queryFn: async () => (await api.GET(route, { params })).data!,
  })
  const save = useMutation({
    mutationFn: async (work: () => Promise<void>) => work(),
    onSuccess: async () => {
      setTab('stock')
      await Promise.all(
        ['olives', 'inventory', 'sales', 'sales-catalog'].map((k) =>
          cache.invalidateQueries({ queryKey: [k, branch.id] }),
        ),
      )
    },
  })
  async function persist(body: object, post: (operationId: string) => Promise<unknown>) {
    const fingerprint = JSON.stringify(body)
    const operationId = keys.current.get(fingerprint) ?? crypto.randomUUID()
    keys.current.set(fingerprint, operationId)
    await post(operationId)
    keys.current.delete(fingerprint)
  }
  const data = desk.data
  const tabs = [
    ['stock', 'Lotes y existencias'],
    ['receipt', 'Recepción pesada'],
    ['repack', 'Reenvasado y merma'],
    ['trace', 'Trazabilidad'],
  ] as const
  return (
    <>
      <header className="section-head directory-heading">
        <div>
          <p>{branch.name}</p>
          <h1>Comercialización de aceitunas</h1>
        </div>
        <Link to="/inventario">Compras e inventario</Link>
      </header>
      <div className="actions olive-tabs">
        {tabs
          .filter(
            ([key]) =>
              key === 'stock' || key === 'trace' || branch.permissions.includes('inventory.write'),
          )
          .map(([key, title]) => (
            <button
              className="secondary"
              key={key}
              aria-pressed={tab === key}
              onClick={() => {
                save.reset()
                setTab(key)
              }}
            >
              {title}
            </button>
          ))}
      </div>
      {desk.isPending && <p>Consultando lotes…</p>}
      {desk.error && <p role="alert">{desk.error.message}</p>}
      {save.error && <p role="alert">{save.error.message}</p>}
      {save.isSuccess && <p role="status">Movimiento registrado en el kardex.</p>}
      {data && tab === 'receipt' && (
        <WeightedReceipt
          branch={branch}
          data={data}
          busy={save.isPending}
          save={(body) =>
            save.mutate(() =>
              persist(body, (operationId) =>
                api.POST('/api/v1/branches/{branchId}/inventory/movements', {
                  params,
                  body: { ...body, operationId },
                }),
              ),
            )
          }
        />
      )}
      {data && tab === 'repack' && (
        <Repacking
          data={data}
          busy={save.isPending}
          save={(body) =>
            save.mutate(() =>
              persist(body, (operationId) =>
                api.POST('/api/v1/branches/{branchId}/olives/repacking', {
                  params,
                  body: { ...body, operationId },
                }),
              ),
            )
          }
        />
      )}
      {data && tab === 'trace' && <TracePanel branch={branch} data={data} />}
      {data && tab === 'stock' && (
        <section className="panel">
          <h2>Existencias por lote</h2>
          {data.stock.length ? (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    {[
                      'Lote y presentación',
                      'Almacén',
                      'Existencia',
                      'Disponible',
                      'Peso disponible',
                    ].map((h) => (
                      <th key={h}>{h}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {data.stock.map((s) => {
                    const lot = data.lots.find((l) => l.id === s.lotId)!
                    return (
                      <tr key={s.lotId + '-' + s.warehouseId}>
                        <td>
                          {description(lot)}
                          <small>
                            {lot.presentation} · {kg(lot.kgPerUnit)} por {lot.unit}
                          </small>
                        </td>
                        <td>{data.warehouses.find((w) => w.id === s.warehouseId)?.name}</td>
                        <td>
                          {number(s.onHand)} {lot.unit}
                        </td>
                        <td>
                          {number(s.available)} {lot.unit}
                        </td>
                        <td>{kg(Number(s.available) * Number(lot.kgPerUnit))}</td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            </div>
          ) : (
            <p>Los lotes aparecerán al registrar una recepción pesada.</p>
          )}
        </section>
      )}
    </>
  )
}
