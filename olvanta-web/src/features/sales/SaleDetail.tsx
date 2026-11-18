import { useState } from 'react'
import type { Models } from '../../shared/workspace'
import { methods, money, operations, stages, type Branch, type Cash, type Run, type Sale } from './sales'

export function Journal({ entries, currency }: { entries: Models['TradeEntry'][]; currency: string }) {
  return (
    <ol className="trade-history">
      {entries.map((e) => (
        <li key={e.id}>
          <strong>{operations[e.kind]}</strong>
          {' · '}
          {new Date(e.time).toLocaleString('es-PE')} · {e.actor}
          {e.saleId && ' · Venta #' + e.saleId}
          {e.lineId && ' · Partida #' + e.lineId}
          {Number(e.quantity) > 0 && ' · Cant. ' + e.quantity}
          {[
            'pay',
            'refund',
            'return',
            'open',
            'close',
            'cash-in',
            'cash-out',
            'deposit-in',
            'deposit-out',
          ].includes(e.kind) && ' · ' + money(e.amount, currency)}
          {e.method && ' · ' + methods[e.method]}
          {e.returnId && ' · Devolución #' + e.returnId}
          {e.kind === 'return' && ' · #' + e.id}
          {e.note && ' · ' + e.note}
        </li>
      ))}
    </ol>
  )
}

export function SaleDetail({
  sale,
  branch,
  session,
  busy,
  run,
}: {
  sale: Sale
  branch: Branch
  session?: Cash
  busy: boolean
  run: Run
}) {
  const [action, setAction] = useState('')
  const can = (p: string) => branch.permissions.includes(p)
  const trading = ['confirmed', 'dispatched'].includes(sale.stage)
  const collected = sale.history.some((e) => e.kind === 'pay')
  const actions: [string, string, boolean][] = [
    ['order', 'Convertir en pedido', can('sales.write') && sale.stage === 'quote'],
    ['confirm', 'Confirmar venta', can('sales.write') && sale.stage === 'order'],
    ['pay', 'Registrar cobro', can('cash.write') && trading && Number(sale.balance) > 0 && !!session],
    [
      'dispatch',
      'Despachar',
      !sale.leaseId &&
        can('sales.dispatch') &&
        sale.stage === 'confirmed' &&
        (sale.terms === 'credit' || Number(sale.balance) === 0),
    ],
    [
      'return',
      'Registrar devolución',
      can('sales.return') && sale.lines.some((l) => Number(l.returnable) > 0),
    ],
    ['refund', 'Reembolsar', can('cash.write') && Number(sale.balance) < 0 && !!session],
    [
      'cancel',
      'Anular',
      !sale.leaseId &&
        can('sales.write') &&
        !collected &&
        ['quote', 'order', 'confirmed'].includes(sale.stage),
    ],
  ]
  const options = actions.filter((x) => x[2])
  const show = options.some((a) => a[0] === action) ? action : ''
  const returns = sale.history.filter(
    (e) =>
      e.kind === 'return' &&
      Number(e.amount) >
        sale.history.filter((r) => r.returnId === e.id).reduce((n, r) => n + Number(r.amount), 0),
  )
  return (
    <section className="panel trade-print">
      <header className="section-head">
        <div>
          <small>{branch.name} · Documento comercial interno</small>
          <h2>
            {stages[sale.stage]} · #{sale.id}
          </h2>
        </div>
        <button className="secondary screen-only" onClick={() => window.print()}>
          Imprimir documento
        </button>
      </header>
      <p>
        <strong>{sale.customer}</strong> ·{' '}
        {sale.terms === 'credit' ? 'Crédito · vence ' + sale.dueDate : 'Contado'} ·{' '}
        {new Date(sale.created).toLocaleDateString('es-PE')}
      </p>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Artículo</th>
              <th>Cantidad</th>
              <th>Precio</th>
              <th>Dto.</th>
              <th>Impuesto incluido</th>
              <th>Total</th>
            </tr>
          </thead>
          <tbody>
            {sale.lines.map((l) => (
              <tr key={l.id}>
                <td>
                  {l.description}
                  {l.warehouse && <small>{l.warehouse}</small>}
                  {l.lot && <small>Lote {l.lot}</small>}
                </td>
                <td>
                  {l.quantity} {l.unit}
                </td>
                <td>{money(l.price, sale.currency)}</td>
                <td>{l.discount}%</td>
                <td>{money(l.tax, sale.currency)}</td>
                <td>{money(l.total, sale.currency)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p>
        <strong>Total {money(sale.total, sale.currency)}</strong>
        {trading && (
          <>
            {' '}
            · {Number(sale.balance) < 0 ? 'Saldo a favor' : 'Saldo por cobrar'}{' '}
            {money(Math.abs(Number(sale.balance)), sale.currency)}
          </>
        )}
      </p>
      <div className="screen-only">
        <div className="actions">
          {options.map(([kind, label]) => (
            <button
              key={kind}
              disabled={busy}
              className="secondary"
              onClick={() => {
                if (['order', 'confirm', 'dispatch'].includes(kind)) run({ kind, saleId: sale.id })
                else setAction(kind)
              }}
            >
              {label}
            </button>
          ))}
        </div>
        {can('cash.write') && trading && Number(sale.balance) !== 0 && !session && (
          <p>Abra una sesión en Caja para registrar el movimiento de dinero.</p>
        )}
        {show && (
          <form
            key={action}
            className="fields trade-action"
            onSubmit={(e) => {
              e.preventDefault()
              const f = new FormData(e.currentTarget)
              run({
                kind: show,
                saleId: sale.id,
                note: String(f.get('note') ?? ''),
                ...(show === 'return'
                  ? { lineId: Number(f.get('line')), quantity: Number(f.get('quantity')) }
                  : {}),
                ...(['pay', 'refund'].includes(show)
                  ? {
                      cashSessionId: session!.id,
                      amount: Number(f.get('amount')),
                      method: String(f.get('method')),
                    }
                  : {}),
                ...(show === 'refund' ? { returnId: Number(f.get('return')) } : {}),
              })
            }}
          >
            {show === 'return' && (
              <>
                <label>
                  Partida a devolver
                  <select name="line">
                    {sale.lines
                      .filter((l) => Number(l.returnable) > 0)
                      .map((l) => (
                        <option value={l.id} key={l.id}>
                          {l.description} · {l.returnable} {l.unit} · {l.warehouse} {l.lot}
                        </option>
                      ))}
                  </select>
                </label>
                <label>
                  Cantidad devuelta
                  <input type="number" name="quantity" min=".000001" step=".000001" required />
                </label>
              </>
            )}
            {show === 'refund' && (
              <label>
                Devolución de origen
                <select name="return" required>
                  {returns.map((e) => (
                    <option value={e.id} key={e.id}>
                      #{e.id} · {e.note} · {money(e.amount, sale.currency)}
                    </option>
                  ))}
                </select>
              </label>
            )}
            {['pay', 'refund'].includes(show) && (
              <>
                <label>
                  Importe
                  <input
                    name="amount"
                    type="number"
                    min=".01"
                    step=".01"
                    max={Math.abs(Number(sale.balance))}
                    defaultValue={Math.abs(Number(sale.balance))}
                    required
                  />
                </label>
                <label>
                  Medio de pago
                  <select name="method">
                    {Object.entries(methods).map(([v, l]) => (
                      <option key={v} value={v}>
                        {l}
                      </option>
                    ))}
                  </select>
                </label>
              </>
            )}
            <label>
              {show === 'pay' ? 'Referencia (opcional)' : 'Motivo'}
              <input name="note" maxLength={240} required={show !== 'pay'} />
            </label>
            <button disabled={busy}>Guardar operación</button>
          </form>
        )}
      </div>
      <h3>Historial</h3>
      <Journal entries={sale.history} currency={sale.currency} />
    </section>
  )
}
