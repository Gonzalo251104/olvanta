import { useState } from 'react'
import { Journal } from './SaleDetail'
import { money, type Branch, type Cash, type Run } from './sales'

export function CashPage({
  branch,
  sessions,
  run,
  busy,
}: {
  branch: Branch
  sessions: Cash[]
  run: Run
  busy: boolean
}) {
  const [id, setId] = useState('')
  const active = sessions.find((s) => !s.closed)
  const selected = sessions.find((s) => String(s.id) === id) ?? sessions[0]
  const [kind, setKind] = useState('cash-in')
  const operation = active ? kind : 'open'
  const labels: Record<string, string> = {
    open: 'Abrir caja',
    close: 'Cerrar caja',
    'cash-in': 'Registrar ingreso',
    'cash-out': 'Registrar retiro',
  }
  return (
    <div className="stock-layout">
      {branch.permissions.includes('cash.write') && (
        <section className="panel screen-only">
          <h2>{active ? 'Caja abierta #' + active.id : 'Apertura de caja'}</h2>
          <form
            key={String(active?.id) + operation}
            className="fields"
            onSubmit={(e) => {
              e.preventDefault()
              const f = new FormData(e.currentTarget)
              run({
                kind: operation,
                cashSessionId: active?.id ?? null,
                amount: Number(f.get('amount')),
                note: String(f.get('note') ?? ''),
              })
            }}
          >
            {active && (
              <label>
                Movimiento de caja
                <select value={kind} onChange={(e) => setKind(e.target.value)}>
                  <option value="cash-in">Ingreso de efectivo</option>
                  <option value="cash-out">Retiro de efectivo</option>
                  <option value="close">Cierre por arqueo</option>
                </select>
              </label>
            )}
            <label>
              {operation === 'close'
                ? 'Efectivo contado'
                : active
                  ? 'Importe en efectivo'
                  : 'Fondo inicial'}
              <input
                name="amount"
                type="number"
                min={['open', 'close'].includes(operation) ? 0 : 0.01}
                max="99999999999999.99"
                step=".01"
                required
              />
            </label>
            {active && (
              <label>
                {operation === 'close' ? 'Observación (opcional)' : 'Motivo'}
                <input name="note" maxLength={240} required={operation !== 'close'} />
              </label>
            )}
            <button disabled={busy}>{labels[operation]}</button>
          </form>
        </section>
      )}
      <section className="panel trade-print">
        <h2>Arqueo de caja</h2>
        {selected ? (
          <>
            <label className="screen-only">
              Sesión
              <select value={selected.id} onChange={(e) => setId(e.target.value)}>
                {sessions.map((s) => (
                  <option key={s.id} value={s.id}>
                    #{s.id} · {new Date(s.opened).toLocaleString('es-PE')} ·{' '}
                    {s.closed ? 'Cerrada' : 'Abierta'}
                  </option>
                ))}
              </select>
            </label>
            <h3>
              {branch.name} · Caja #{selected.id}
            </h3>
            <p>
              Fondo inicial {money(selected.opening, selected.currency)}
              <br />
              Efectivo esperado <strong>{money(selected.expected, selected.currency)}</strong>
            </p>
            {selected.closed && (
              <p>
                Contado {money(selected.counted, selected.currency)} · Diferencia{' '}
                <strong>{money(selected.difference, selected.currency)}</strong>
              </p>
            )}
            <Journal entries={selected.history} currency={selected.currency} />
            <button className="secondary screen-only" onClick={() => window.print()}>
              Imprimir arqueo
            </button>
          </>
        ) : (
          <p>Aún no hay sesiones de caja en esta sucursal.</p>
        )}
      </section>
    </div>
  )
}
