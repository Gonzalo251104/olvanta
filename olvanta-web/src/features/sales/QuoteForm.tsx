import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api, type Models } from '../../shared/workspace'
import { catalogPath, money, type Branch, type Quote } from './sales'

function QuoteRow({
  id,
  catalog,
  branch,
}: {
  id: number
  catalog: Models['SalesCatalog']
  branch: Branch
}) {
  const [selected, setSelected] = useState(String(catalog.items[0]?.id ?? ''))
  const [location, setLocation] = useState(String(catalog.warehouses[0]?.id ?? ''))
  const item = catalog.items.find((x) => String(x.id) === selected)
  const stock = catalog.stock.filter((x) => x.itemId === item?.id && String(x.warehouseId) === location)
  return (
    <fieldset>
      <legend>Partida {id + 1}</legend>
      <label>
        Artículo
        <select
          name={'item' + id}
          value={selected}
          onChange={(e) => setSelected(e.target.value)}
          required
        >
          {catalog.items.map((i) => (
            <option key={i.id} value={i.id}>
              {i.name} · {i.code} · {money(i.price, branch.currency)} / {i.unit}
            </option>
          ))}
        </select>
      </label>
      <label>
        Cantidad
        <input
          name={'quantity' + id}
          type="number"
          min="0.000001"
          max="99999999"
          step="0.000001"
          defaultValue="1"
          required
        />
      </label>
      {branch.permissions.includes('sales.discount') && (
        <label>
          Descuento (%)
          <input
            name={'discount' + id}
            type="number"
            min="0"
            max={branch.maxDiscount ?? 100}
            step=".01"
            defaultValue="0"
            required
          />
        </label>
      )}
      {item?.kind === 'products' && (
        <>
          <label>
            Almacén
            <select
              name={'warehouse' + id}
              value={location}
              onChange={(e) => setLocation(e.target.value)}
              required
            >
              {catalog.warehouses.map((w) => (
                <option value={w.id} key={w.id}>
                  {w.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Lote
            <select key={selected + ':' + location} name={'lot' + id}>
              <option value="">Sin lote</option>
              {stock
                .filter((s) => s.lot)
                .map((s) => (
                  <option key={s.lot} value={s.lot}>
                    {s.lot} · {s.available} disponibles
                  </option>
                ))}
            </select>
          </label>
        </>
      )}
    </fieldset>
  )
}

export function QuoteForm({
  branch,
  busy,
  submit,
}: {
  branch: Branch
  busy: boolean
  submit: (input: Quote) => void
}) {
  const [lines, setLines] = useState([0])
  const [terms, setTerms] = useState('cash')
  const catalog = useQuery({
    queryKey: ['sales-catalog', branch.id],
    queryFn: async () =>
      (await api.GET(catalogPath, { params: { path: { branchId: Number(branch.id) } } })).data!,
  })
  if (catalog.error) return <p role="alert">{catalog.error.message}</p>
  if (!catalog.data) return <p role="status">Consultando catálogo…</p>
  const data = catalog.data
  if (!data.items.length || !data.customers.length)
    return (
      <p className="panel">
        Registre un cliente y un producto o servicio activo con precio en {branch.currency} para cotizar.
      </p>
    )
  return (
    <section className="panel">
      <h2>Nueva cotización</h2>
      <form
        className="fields"
        onSubmit={(e) => {
          e.preventDefault()
          const f = new FormData(e.currentTarget)
          submit({
            customerId: Number(f.get('customer')),
            terms,
            dueDate: terms === 'credit' ? String(f.get('due')) : null,
            lines: lines.map((i) => ({
              itemId: Number(f.get('item' + i)),
              quantity: Number(f.get('quantity' + i)),
              discount: Number(f.get('discount' + i) ?? 0),
              warehouseId: f.has('warehouse' + i) ? Number(f.get('warehouse' + i)) : null,
              lot: String(f.get('lot' + i) ?? ''),
            })),
          })
        }}
      >
        <label>
          Cliente
          <select name="customer" required>
            {data.customers.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Condición
          <select value={terms} onChange={(e) => setTerms(e.target.value)}>
            <option value="cash">Contado</option>
            <option value="credit">Crédito</option>
          </select>
        </label>
        {terms === 'credit' && (
          <label>
            Fecha de vencimiento
            <input name="due" type="date" required />
          </label>
        )}
        {lines.map((id) => (
          <div key={id}>
            <QuoteRow id={id} catalog={data} branch={branch} />
            {lines.length > 1 && (
              <button
                className="linkbtn"
                type="button"
                onClick={() => setLines(lines.filter((x) => x !== id))}
              >
                Quitar partida {id + 1}
              </button>
            )}
          </div>
        ))}
        <div className="actions">
          <button
            disabled={lines.length === 100}
            className="secondary"
            type="button"
            onClick={() => setLines([...lines, Math.max(...lines) + 1])}
          >
            Añadir partida
          </button>
          <button disabled={busy}>Guardar cotización</button>
        </div>
      </form>
    </section>
  )
}
