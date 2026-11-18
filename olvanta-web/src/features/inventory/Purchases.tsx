import { useState } from 'react'
import type { FormEvent } from 'react'
import { api } from '../../shared/workspace'
import { number, productName } from './inventory'
import type { Inventory } from './inventory'

export function Purchases({
  data,
  branchId,
  currency,
  writable,
  receive,
  submit,
  busy,
}: {
  data: Inventory
  branchId: number
  currency: string
  writable: boolean
  receive: (id: string) => void
  submit: (job: () => Promise<void>) => void
  busy: boolean
}) {
  const [lines, setLines] = useState([0])
  const products = data.products.filter((p) => p.active)
  function create(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const form = e.currentTarget
    const fields = new FormData(form)
    submit(async () => {
      await api.POST('/api/v1/branches/{branchId}/inventory/purchases', {
        params: { path: { branchId } },
        body: {
          reference: String(fields.get('reference')),
          supplierId: Number(fields.get('supplier')),
          lines: lines.map((id) => ({
            productId: Number(fields.get('product' + id)),
            quantity: Number(fields.get('quantity' + id)),
            unitCost: Number(fields.get('cost' + id)),
          })),
        },
      })
      form.reset()
      setLines([0])
    })
  }
  return (
    <div className="stock-layout">
      <section className="panel">
        <h2>Órdenes de compra</h2>
        {data.purchases.length === 0 && <p>Aún no hay compras en esta sucursal.</p>}
        {data.purchases.map((order) => (
          <article className="stock-order" key={order.id}>
            <h3>
              {order.reference} · {order.supplier}
            </h3>
            <ul>
              {order.lines.map((line) => (
                <li key={line.id}>
                  <strong>{productName(data, line.productId)}</strong>
                  <span>
                    {number(line.received)} de {number(line.quantity)} recibidos · {order.currency}{' '}
                    {number(line.unitCost)} por unidad
                  </span>
                  {Number(line.received) < Number(line.quantity) ? (
                    writable && (
                      <button className="linkbtn" onClick={() => receive(String(line.id))}>
                        Recibir mercadería
                      </button>
                    )
                  ) : (
                    <small>Recepción completa</small>
                  )}
                </li>
              ))}
            </ul>
          </article>
        ))}
      </section>
      {writable && (
        <section className="panel">
          <h2>Nueva compra</h2>
          {data.suppliers.length && products.length ? (
            <form className="fields" onSubmit={create}>
              <label>
                Referencia de compra
                <input name="reference" maxLength={80} required />
              </label>
              <label>
                Proveedor
                <select name="supplier">
                  {data.suppliers.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name}
                    </option>
                  ))}
                </select>
              </label>
              {lines.map((id, index) => (
                <fieldset key={id}>
                  <legend>Partida {index + 1}</legend>
                  <label>
                    Producto
                    <select name={'product' + id}>
                      {products.map((p) => (
                        <option key={p.id} value={p.id}>
                          {productName(data, p.id)}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    Cantidad en unidad base
                    <input
                      name={'quantity' + id}
                      type="number"
                      min="0.000001"
                      max="99999999"
                      step="0.000001"
                      required
                    />
                  </label>
                  <label>
                    Costo por unidad ({currency})
                    <input
                      name={'cost' + id}
                      type="number"
                      min="0"
                      max="99999999"
                      step="0.000001"
                      required
                    />
                  </label>
                  {lines.length > 1 && (
                    <button
                      type="button"
                      className="linkbtn"
                      onClick={() => setLines(lines.filter((x) => x !== id))}
                    >
                      Quitar partida
                    </button>
                  )}
                </fieldset>
              ))}
              <div className="actions">
                <button
                  type="button"
                  className="secondary"
                  disabled={lines.length >= 100}
                  onClick={() => setLines([...lines, Math.max(...lines) + 1])}
                >
                  Añadir partida
                </button>
                <button disabled={busy}>Registrar compra</button>
              </div>
            </form>
          ) : (
            <p>
              Registre un proveedor y un producto activos en los maestros para crear la primera compra.
            </p>
          )}
        </section>
      )}
    </div>
  )
}
