import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api, type Models } from '../../shared/workspace'

const reports = [
  { kind: 'sales', label: 'Ventas confirmadas', permission: 'sales.read', period: true },
  { kind: 'collections', label: 'Cobros y devoluciones', permission: 'cash.read', period: true },
  { kind: 'debt', label: 'Cuentas por cobrar', permission: 'sales.read' },
  { kind: 'cash', label: 'Arqueos de caja', permission: 'cash.read', period: true },
  { kind: 'stock', label: 'Existencias y valor', permission: 'inventory.read', lot: true },
  { kind: 'ledger', label: 'Kardex', permission: 'inventory.read', period: true, lot: true },
  { kind: 'lots', label: 'Trazabilidad de aceitunas', permission: 'inventory.read', lot: true },
  { kind: 'rentals', label: 'Locales ocupados y deuda', permission: 'rentals.read' },
  { kind: 'parking', label: 'Ocupación de estacionamientos', permission: 'parking.read' },
  {
    kind: 'parking-income',
    label: 'Ingresos de estacionamientos',
    permission: 'parking.read',
    period: true,
  },
  { kind: 'billing', label: 'Comprobantes pendientes y rechazados', permission: 'billing.read' },
]

export function canSeeReports(branch: Models['BranchAccess']) {
  return reports.some((r) => branch.permissions.includes(r.permission))
}

type Filters = { From?: string; Through?: string; Seller?: string; Lot?: string }
const dateFormatter = new Intl.DateTimeFormat('sv-SE', { timeZone: 'America/Lima', dateStyle: 'short' })

export default function ReportsPage({ branch }: { branch: Models['BranchAccess'] }) {
  const allowed = reports.filter((r) => branch.permissions.includes(r.permission))
  const [selected, setSelected] = useState(allowed[0].kind)
  const [filters, setFilters] = useState<Filters>({})
  const report = allowed.find((r) => r.kind === selected)!
  const today = dateFormatter.format(new Date())
  const query = useQuery({
    queryKey: ['reports', branch.id, selected, filters],
    queryFn: async () =>
      (
        await api.GET('/api/v1/branches/{branchId}/reports/{kind}', {
          params: { path: { branchId: Number(branch.id), kind: selected }, query: filters },
        })
      ).data!,
  })
  const file = (format: string) =>
    '/api/v1/branches/' +
    branch.id +
    '/reports/' +
    selected +
    '/export/' +
    format +
    '?' +
    new URLSearchParams(Object.entries(filters).filter((pair): pair is [string, string] => !!pair[1]))
  return (
    <>
      <header className="section-head directory-heading">
        <div>
          <p>{branch.name}</p>
          <h1>Reportes de operación</h1>
        </div>
      </header>
      <div className="trade-layout">
        <aside className="panel fields">
          <label>
            Reporte
            <select
              value={selected}
              onChange={(e) => {
                setSelected(e.target.value)
                setFilters({})
              }}
            >
              {allowed.map((r) => (
                <option value={r.kind} key={r.kind}>
                  {r.label}
                </option>
              ))}
            </select>
          </label>
          <form
            key={selected}
            className="fields"
            onSubmit={(e) => {
              e.preventDefault()
              const form = new FormData(e.currentTarget)
              const val = (key: string) => String(form.get(key) ?? '').trim() || undefined
              const next = {
                From: val('from'),
                Through: val('through'),
                Seller: val('seller'),
                Lot: val('lot'),
              }
              if (
                Object.keys(next).every(
                  (key) => next[key as keyof Filters] === filters[key as keyof Filters],
                )
              )
                void query.refetch()
              else setFilters(next)
            }}
          >
            {report.period && (
              <>
                <label>
                  Desde
                  <input name="from" type="date" defaultValue={today.slice(0, 7) + '-01'} required />
                </label>
                <label>
                  Hasta
                  <input name="through" type="date" defaultValue={today} required />
                </label>
              </>
            )}
            {selected === 'sales' && (
              <label>
                Usuario vendedor
                <input name="seller" maxLength={256} />
              </label>
            )}
            {report.lot && (
              <label>
                Código del lote
                <input name="lot" maxLength={80} />
              </label>
            )}
            <button disabled={query.isFetching}>Consultar</button>
          </form>
        </aside>
        <section className="panel fields">
          {query.isPending && <p role="status">Consultando reporte…</p>}
          {query.error && <p role="alert">{query.error.message}</p>}
          {query.data && (
            <>
              <header className="section-head">
                <div>
                  <h2>{query.data.title}</h2>
                  <p>
                    {query.data.scope} · {query.data.currency}
                  </p>
                </div>
                <div className="actions">
                  <a href={file('csv')} download>
                    Descargar CSV
                  </a>
                  {selected === 'cash' && (
                    <a href={file('pdf')} download>
                      Descargar PDF
                    </a>
                  )}
                </div>
              </header>
              {!query.data.rows.length ? (
                <p>Sin registros para esta consulta.</p>
              ) : (
                <div className="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        {query.data.columns.map((c) => (
                          <th key={c}>{c}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {query.data.rows.map((row, index) => (
                        <tr key={index}>
                          {row.map((cell, position) => (
                            <td key={position}>{cell}</td>
                          ))}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </>
          )}
        </section>
      </div>
    </>
  )
}
