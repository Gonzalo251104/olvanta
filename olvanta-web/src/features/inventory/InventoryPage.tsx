import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/workspace'
import {
  inventory,
  inventoryMoves,
  movementNames,
  number,
  productName,
  warehouseName,
} from './inventory'
import type { Branch, Inventory } from './inventory'
import { MovementForm } from './MovementForm'
import { Purchases } from './Purchases'

const warehousePath = '/api/v1/branches/{branchId}/inventory/warehouses'
const minimumPath = '/api/v1/branches/{branchId}/inventory/minimum'

function Kardex({ data, branch }: { data: Inventory; branch: Branch }) {
  const [warehouse, setWarehouse] = useState('')
  const [product, setProduct] = useState('')
  const [pages, setPages] = useState<number[]>([])
  const before = pages.at(-1)
  const query = useQuery({
    queryKey: ['inventory', branch.id, 'ledger', warehouse, product, before],
    queryFn: async () =>
      (
        await api.GET(inventoryMoves, {
          params: {
            path: { branchId: Number(branch.id) },
            query: {
              warehouseId: Number(warehouse) || undefined,
              productId: Number(product) || undefined,
              before,
            },
          },
        })
      ).data!,
  })
  return (
    <section className="panel">
      <h2>Kardex</h2>
      <div className="filters">
        <label>
          Filtrar por almacén
          <select
            value={warehouse}
            onChange={(e) => {
              setWarehouse(e.target.value)
              setPages([])
            }}
          >
            <option value="">Todos los almacenes</option>
            {data.warehouses.map((w) => (
              <option key={w.id} value={w.id}>
                {w.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Filtrar por producto
          <select
            value={product}
            onChange={(e) => {
              setProduct(e.target.value)
              setPages([])
            }}
          >
            <option value="">Todos los productos</option>
            {data.products.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
        </label>
      </div>
      {query.error && <p role="alert">{query.error.message}</p>}
      {query.isPending && <p role="status">Consultando kardex…</p>}
      {query.data &&
        (query.data.length ? (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Movimiento</th>
                  <th>Producto y lote</th>
                  <th>Almacén</th>
                  <th>Físico</th>
                  <th>Reservado</th>
                  <th>Valor ({branch.currency})</th>
                  <th>Documento y operador</th>
                </tr>
              </thead>
              <tbody>
                {query.data.map((m) => (
                  <tr key={m.id}>
                    <td>
                      #{m.id} · {movementNames[m.kind]}
                      <small>{new Date(m.time).toLocaleString('es-PE')}</small>
                    </td>
                    <td>
                      {productName(data, m.productId)}
                      <small>
                        {m.lot || 'Sin lote'} {m.expiry}
                      </small>
                    </td>
                    <td>{warehouseName(data, m.warehouseId)}</td>
                    <td>{number(m.quantity)}</td>
                    <td>{number(m.reserved)}</td>
                    <td>{number(m.value)}</td>
                    <td>
                      {m.reason}
                      <small>
                        {m.actor}
                        {m.relatedId ? ' · movimiento #' + m.relatedId : ''}
                      </small>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p>Sin movimientos para esta consulta.</p>
        ))}
      <div className="actions">
        <button
          className="secondary"
          disabled={!pages.length}
          onClick={() => setPages(pages.slice(0, -1))}
        >
          Más recientes
        </button>
        <button
          className="secondary"
          disabled={query.data?.length !== 50}
          onClick={() => setPages([...pages, Number(query.data?.at(-1)?.id)])}
        >
          Más antiguos
        </button>
      </div>
    </section>
  )
}

function Balances({ data, currency }: { data: Inventory; currency: string }) {
  const [lowOnly, setLowOnly] = useState(false)
  const balances = data.balances.filter((b) => !lowOnly || b.belowMinimum)
  return (
    <section className="panel">
      <div className="section-head">
        <h2>Existencias</h2>
        <button className="secondary" aria-pressed={lowOnly} onClick={() => setLowOnly(!lowOnly)}>
          {lowOnly ? 'Ver todas' : 'Solo bajo mínimo'}
        </button>
      </div>
      {balances.length ? (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Producto y almacén</th>
                <th>Físico</th>
                <th>Reservado</th>
                <th>Disponible</th>
                <th>Mínimo</th>
                <th>Costo promedio</th>
                <th>Valor ({currency})</th>
              </tr>
            </thead>
            <tbody>
              {balances.map((b) => {
                return (
                  <tr key={b.warehouseId + '-' + b.productId}>
                    <td>
                      <strong>{productName(data, b.productId)}</strong>
                      <small>{warehouseName(data, b.warehouseId)}</small>
                      {b.lots.length > 0 && (
                        <details>
                          <summary>Ver lotes</summary>
                          {b.lots.map((l) => (
                            <small key={l.lot}>
                              {l.lot || 'Sin lote'} · {number(l.quantity)} físicos · {number(l.reserved)}{' '}
                              reservados {l.expiry && ' · vence ' + l.expiry}
                            </small>
                          ))}
                        </details>
                      )}
                    </td>
                    <td>{number(b.quantity)}</td>
                    <td>{number(b.reserved)}</td>
                    <td>
                      {number(b.available)}
                      {b.belowMinimum && <small className="stock-low">Bajo mínimo</small>}
                    </td>
                    <td>{number(b.minimum)}</td>
                    <td>{number(b.averageCost)}</td>
                    <td>{number(b.value)}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      ) : (
        <p>
          {lowOnly
            ? 'Ninguna existencia está bajo su mínimo.'
            : 'Las primeras recepciones y los mínimos configurados aparecerán aquí.'}
        </p>
      )}
    </section>
  )
}

export function InventoryPage({ branch }: { branch: Branch }) {
  const [tab, setTab] = useState('Existencias')
  const [line, setLine] = useState('')
  const cache = useQueryClient()
  const params = { path: { branchId: Number(branch.id) } }
  const key = ['inventory', branch.id]
  const query = useQuery({
    queryKey: key,
    queryFn: async () => (await api.GET(inventory, { params })).data!,
  })
  const save = useMutation({
    mutationFn: (job: () => Promise<void>) => job(),
    onSuccess: () => cache.invalidateQueries({ queryKey: key }),
  })
  const write = branch.permissions.includes('inventory.write')
  const tabs = [
    'Existencias',
    'Compras',
    ...(write || branch.permissions.includes('inventory.reserve') ? ['Movimientos'] : []),
    'Kardex',
    'Almacenes',
  ]
  const select = (t: string) => {
    setTab(t)
    setLine('')
    save.reset()
  }
  const data = query.data
  return (
    <>
      <header className="section-head directory-heading">
        <h1>Compras e inventario</h1>
      </header>
      <div className="actions stock-tabs">
        {tabs.map((t) => (
          <button
            key={t}
            className={t === tab ? '' : 'secondary'}
            aria-pressed={t === tab}
            onClick={() => select(t)}
          >
            {t}
          </button>
        ))}
      </div>
      {save.error && <p role="alert">{save.error.message}</p>}
      {save.isSuccess && <p role="status">Operación registrada.</p>}
      {query.error && <p role="alert">{query.error.message}</p>}
      {query.isPending && <p role="status">Consultando inventario…</p>}
      {data && (
        <>
          {tab === 'Existencias' && <Balances data={data} currency={branch.currency} />}
          {tab === 'Compras' && (
            <Purchases
              data={data}
              branchId={Number(branch.id)}
              currency={branch.currency}
              writable={write}
              receive={(id) => {
                select('Movimientos')
                setLine(id)
              }}
              submit={save.mutate}
              busy={save.isPending}
            />
          )}
          {tab === 'Movimientos' && (
            <MovementForm
              branch={branch}
              data={data}
              initialLine={line}
              submit={save.mutate}
              busy={save.isPending}
            />
          )}
          {tab === 'Kardex' && <Kardex data={data} branch={branch} />}
          {tab === 'Almacenes' && (
            <div className="stock-layout">
              <section className="panel">
                <h2>Almacenes de la sucursal</h2>
                {data.warehouses.length ? (
                  <ul>
                    {data.warehouses.map((w) => (
                      <li key={w.id}>{w.name}</li>
                    ))}
                  </ul>
                ) : (
                  <p>Aún no hay almacenes registrados.</p>
                )}
                {write && (
                  <form
                    className="fields"
                    onSubmit={(e) => {
                      e.preventDefault()
                      const form = e.currentTarget
                      const name = String(new FormData(form).get('name'))
                      save.mutate(async () => {
                        await api.POST(warehousePath, { params, body: { name } })
                        form.reset()
                      })
                    }}
                  >
                    <label>
                      Nombre del almacén
                      <input name="name" maxLength={100} required />
                    </label>
                    <button disabled={save.isPending}>Crear almacén</button>
                  </form>
                )}
              </section>
              {write && data.warehouses.length > 0 && data.products.length > 0 && (
                <section className="panel">
                  <h2>Existencia mínima</h2>
                  <form
                    className="fields"
                    onSubmit={(e) => {
                      e.preventDefault()
                      const f = new FormData(e.currentTarget)
                      save.mutate(async () => {
                        await api.PUT(minimumPath, {
                          params,
                          body: {
                            productId: Number(f.get('product')),
                            warehouseId: Number(f.get('warehouse')),
                            quantity: Number(f.get('minimum')),
                          },
                        })
                      })
                    }}
                  >
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
                      Producto
                      <select name="product">
                        {data.products.map((p) => (
                          <option key={p.id} value={p.id}>
                            {productName(data, p.id)}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label>
                      Mínimo disponible
                      <input
                        name="minimum"
                        required
                        type="number"
                        min="0"
                        max="99999999"
                        step="0.000001"
                      />
                    </label>
                    <button disabled={save.isPending}>Guardar mínimo</button>
                  </form>
                </section>
              )}
            </div>
          )}
        </>
      )}
    </>
  )
}
