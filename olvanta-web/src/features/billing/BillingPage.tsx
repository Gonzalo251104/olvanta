import { useRef, useState } from 'react'
import { useSearchParams } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, type Models } from '../../shared/workspace'

const deskPath = '/api/v1/branches/{branchId}/billing'
const itemPath = '/api/v1/branches/{branchId}/billing/{id}'
const submitPath = '/api/v1/branches/{branchId}/billing/{id}/submission'
const titles: Record<string, string> = {
  '01': 'Factura',
  '03': 'Boleta',
  '07': 'Nota de crédito',
  '08': 'Nota de débito',
  RC: 'Resumen diario',
  RA: 'Comunicación de baja',
}
const states: Record<string, string> = {
  prepared: 'Preparado',
  'awaiting-summary': 'Pendiente de resumen',
  sending: 'Envío en curso',
  uncertain: 'Resultado incierto',
  processing: 'Ticket en proceso',
  accepted: 'Aceptado',
  rejected: 'Rechazado',
  voided: 'Dado de baja',
  started: 'Iniciado',
}
const kinds: Record<string, string> = {
  invoice: 'Factura',
  receipt: 'Boleta',
  credit: 'Nota de crédito por devolución',
  debit: 'Nota de débito por aumento de valor',
  summary: 'Resumen diario',
  void: 'Comunicación de baja',
}
type Prepare = Omit<Models['FiscalInput'], 'requestId'>
type Detail = Models['BillDetail']
type Desk = Models['BillingDesk']
type Id = number | string

function Preparation({
  desk,
  initialSale,
  parent,
  submit,
  busy,
}: {
  desk: Desk
  initialSale?: Id
  parent?: Detail
  submit: (input: Prepare) => void
  busy: boolean
}) {
  const [kind, setKind] = useState(
    parent ? (parent.document.status === 'awaiting-summary' ? 'summary' : 'void') : 'invoice',
  )
  const [selected, setSelected] = useState(
    String(parent?.document.saleId ?? initialSale ?? desk.sales[0]?.id ?? ''),
  )
  const sale = desk.sales.find((s) => String(s.id) === selected)
  const options = parent
    ? parent.document.status === 'awaiting-summary'
      ? ['summary']
      : ['void', ...(['01', '03'].includes(parent.document.type) ? ['credit', 'debit'] : [])]
    : ['invoice', 'receipt']
  const series = desk.series.filter(
    (s) => s.kind === kind && (!parent || s.code[0] === parent.document.identifier[0]),
  )
  const needsSeries = !['summary', 'void'].includes(kind)
  if (!sale) return <p>Confirme una venta en Ventas y caja para preparar el primer comprobante.</p>
  return (
    <form
      className="fields"
      onSubmit={(e) => {
        e.preventDefault()
        const f = new FormData(e.currentTarget)
        submit({
          kind,
          saleId: sale.id,
          parentId: parent?.document.id ?? null,
          seriesId: needsSeries ? Number(f.get('series')) : null,
          zeroTaxCode: String(f.get('tax') || '') || null,
          reason: String(f.get('reason') || '') || null,
          notDelivered: f.has('notDelivered'),
          returnId: kind === 'credit' ? Number(f.get('return')) : null,
          charge: kind === 'debit' ? Number(f.get('charge')) : null,
          chargeReference: kind === 'debit' ? String(f.get('reference')) : null,
          saleLineId: kind === 'debit' ? Number(f.get('line')) : null,
        })
      }}
    >
      {!parent && (
        <label>
          Venta
          <select value={selected} onChange={(e) => setSelected(e.target.value)}>
            {desk.sales.map((s) => (
              <option key={s.id} value={s.id}>
                #{s.id} · {s.customer} · {s.currency} {s.total}
              </option>
            ))}
          </select>
        </label>
      )}
      <label>
        Tipo de documento
        <select value={kind} onChange={(e) => setKind(e.target.value)}>
          {options.map((k) => (
            <option key={k} value={k}>
              {kinds[k]}
            </option>
          ))}
        </select>
      </label>
      {needsSeries && (
        <label>
          Serie fiscal
          <select name="series" required>
            {series.map((s) => (
              <option key={s.id} value={s.id}>
                {s.code} · {s.site}
              </option>
            ))}
          </select>
        </label>
      )}
      {needsSeries && !series.length && (
        <p>Registre una serie de este tipo en Administración → Organización.</p>
      )}
      {!parent && (
        <label>
          Afectación de las partidas a tasa cero
          <select name="tax">
            <option value="">Solo partidas gravadas</option>
            <option value="20">Exoneradas</option>
            <option value="30">Inafectas</option>
          </select>
        </label>
      )}
      {kind === 'credit' && (
        <label>
          Devolución de la venta
          <select name="return" required>
            {sale.returns.map((r) => (
              <option key={r.id} value={r.id}>
                #{r.id} · {r.reason} · {r.amount}
              </option>
            ))}
          </select>
        </label>
      )}
      {kind === 'debit' && (
        <>
          <label>
            Partida afectada
            <select name="line" required>
              {parent?.data.lines.map((l) => (
                <option key={l.saleLineId} value={l.saleLineId}>
                  {l.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Aumento con impuesto
            <input type="number" name="charge" min=".01" step=".01" required />
          </label>
          <label>
            Referencia del cargo
            <input name="reference" maxLength={64} required />
          </label>
        </>
      )}
      {['void', 'credit', 'debit'].includes(kind) && (
        <label>
          Motivo
          <input name="reason" maxLength={200} required />
        </label>
      )}
      {kind === 'void' && (
        <label className="billing-check">
          <input type="checkbox" name="notDelivered" required />
          Confirmo que el comprobante no fue otorgado al cliente.
        </label>
      )}
      <button
        disabled={busy || (needsSeries && !series.length) || (kind === 'credit' && !sale.returns.length)}
      >
        Preparar y firmar
      </button>
    </form>
  )
}

export function BillingPage({ branch }: { branch: Models['BranchAccess'] }) {
  const cache = useQueryClient()
  const params = { path: { branchId: Number(branch.id) } }
  const prefix = ['billing', branch.id]
  const [selected, setSelected] = useState<Id>()
  const [search] = useSearchParams()
  const requestedSale = search.get('venta')
  const [formOpen, setNewDoc] = useState<boolean>()
  const [related, setRelated] = useState(false)
  const [filter, setFilter] = useState('')
  const [scenario, setScenario] = useState('accept')
  const retry = useRef(new Map<string, string>())
  const can = branch.permissions.includes('billing.write')
  const desk = useQuery({
    queryKey: prefix,
    queryFn: async () => (await api.GET(deskPath, { params })).data!,
  })
  const rows = desk.data?.documents.filter((d) => !filter || d.status === filter) ?? []
  const original = rows.find((r) => String(r.saleId) === requestedSale && ['01', '03'].includes(r.type))
  const initialSale = desk.data?.sales.find((s) => String(s.id) === requestedSale)?.id
  const newDoc = formOpen ?? (can && initialSale != null && !original)
  const id = rows.find((r) => r.id === selected)?.id ?? original?.id ?? rows[0]?.id
  const details = useQuery({
    queryKey: [...prefix, 'document', id],
    enabled: id != null && !newDoc,
    queryFn: async () =>
      (await api.GET(itemPath, { params: { path: { ...params.path, id: Number(id) } } })).data!,
  })
  const refresh = () => cache.invalidateQueries({ queryKey: prefix })
  const prepare = useMutation({
    mutationFn: async (input: Prepare) => {
      const key = JSON.stringify(input),
        requestId = retry.current.get(key) ?? crypto.randomUUID()
      retry.current.set(key, requestId)
      const r = await api.POST(deskPath, { params, body: { ...input, requestId } })
      retry.current.delete(key)
      return r.data!
    },
    onSuccess: async (value) => {
      setNewDoc(false)
      setRelated(false)
      setFilter('')
      setSelected(value)
      await refresh()
    },
  })
  const send = useMutation({
    mutationFn: async (action: string) =>
      api.POST(submitPath, {
        params: { path: { ...params.path, id: Number(id) } },
        body: { action, scenario: action === 'query' ? 'accept' : scenario },
      }),
    onSettled: async () => {
      setScenario('accept')
      await refresh()
    },
  })
  function choose(value: Id) {
    setFilter('')
    setNewDoc(false)
    setRelated(false)
    setSelected(value)
    setScenario('accept')
    prepare.reset()
    send.reset()
  }
  const detail = details.data,
    row = detail?.document,
    live = !!row && row.mode === desk.data?.mode
  const files = '/api/v1/branches/' + branch.id + '/billing/' + id + '/files/'
  const busy = prepare.isPending || send.isPending
  return (
    <>
      <header className="section-head directory-heading">
        <div>
          <p>
            {desk.data?.mode === 'beta' ? 'SUNAT BETA · Pruebas externas' : 'Simulación local'} · Sin
            validez tributaria
          </p>
          <h1>Facturación electrónica</h1>
        </div>
        <div className="actions">
          <button className="secondary" onClick={() => void refresh()}>
            Actualizar
          </button>
          {can && (
            <button
              onClick={() => {
                setNewDoc(true)
                setRelated(false)
                prepare.reset()
                send.reset()
              }}
            >
              Nuevo comprobante
            </button>
          )}
        </div>
      </header>
      {[desk.error, details.error, prepare.error, send.error]
        .filter((e) => e != null)
        .map((e, i) => (
          <p key={i} role="alert">
            {e.message}
          </p>
        ))}
      {desk.isPending && <p role="status">Consultando facturación…</p>}
      {desk.data && (
        <div className="trade-layout">
          <aside className="panel">
            <h2>Comprobantes</h2>
            <label>
              Estado del documento
              <select
                value={filter}
                onChange={(e) => {
                  setFilter(e.target.value)
                  setRelated(false)
                  setNewDoc(false)
                }}
              >
                <option value="">Todos</option>
                {Object.entries(states)
                  .filter(([k]) => k !== 'started')
                  .map(([k, v]) => (
                    <option key={k} value={k}>
                      {v}
                    </option>
                  ))}
              </select>
            </label>
            <ul className="trade-list">
              {rows.map((d) => (
                <li key={d.id}>
                  <button
                    className="linkbtn"
                    aria-pressed={!newDoc && d.id === id}
                    onClick={() => choose(d.id)}
                  >
                    {titles[d.type]} {d.identifier}
                  </button>
                  <small>
                    {states[d.status]} · Venta #{d.saleId}
                  </small>
                </li>
              ))}
            </ul>
            {!rows.length && <p>Sin documentos para esta consulta.</p>}
          </aside>
          {newDoc ? (
            <section className="panel">
              <h2>Preparar comprobante</h2>
              <Preparation
                desk={desk.data}
                initialSale={initialSale}
                busy={busy}
                submit={prepare.mutate}
              />
            </section>
          ) : (
            detail &&
            row && (
              <section className="panel">
                <header className="section-head">
                  <h2>
                    {titles[row.type]} {row.identifier}
                  </h2>
                  <strong>{states[row.status]}</strong>
                </header>
                <p>
                  {row.mode === 'beta' ? 'SUNAT BETA' : 'Simulación local'} · Venta #{row.saleId} ·{' '}
                  {detail.data.date}
                </p>
                <p>
                  {detail.data.customer} · {detail.data.customerId}
                  <br />
                  {detail.data.issuer} · RUC {detail.data.ruc}
                </p>
                <div className="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <th>Descripción</th>
                        <th>Cantidad</th>
                        <th>Importe</th>
                      </tr>
                    </thead>
                    <tbody>
                      {detail.data.lines.map((l, i) => (
                        <tr key={i}>
                          <td>{l.name}</td>
                          <td>
                            {l.quantity} {l.unit}
                          </td>
                          <td>
                            {detail.data.currency} {Number(l.total).toFixed(2)}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <p>
                  IGV {detail.data.currency} {Number(detail.data.tax).toFixed(2)} ·{' '}
                  <strong>
                    Total {detail.data.currency} {Number(detail.data.total).toFixed(2)}
                  </strong>
                </p>
                <div className="actions">
                  {['xml', 'zip', 'pdf', ...(row.hasCdr ? ['cdr'] : [])].map((k) => (
                    <a key={k} href={files + k} download>
                      {k.toUpperCase()}
                    </a>
                  ))}
                </div>
                {row.parentId && (
                  <button className="linkbtn" onClick={() => choose(row.parentId!)}>
                    Ver documento de origen
                  </button>
                )}
                {desk.data.documents
                  .filter((r) => r.parentId === row.id)
                  .map((r) => (
                    <button key={r.id} className="linkbtn" onClick={() => choose(r.id)}>
                      {titles[r.type]} {r.identifier} · {states[r.status]}
                    </button>
                  ))}
                {detail.ticket && <p>Ticket {detail.ticket}</p>}
                {can && live && (
                  <div className="fields trade-action">
                    {row.status === 'prepared' && (
                      <>
                        {row.mode === 'simulation' && (
                          <label>
                            Respuesta simulada
                            <select value={scenario} onChange={(e) => setScenario(e.target.value)}>
                              <option value="accept">Aceptación</option>
                              <option value="lost">Pérdida de respuesta</option>
                              <option value="reject">Rechazo</option>
                            </select>
                          </label>
                        )}
                        <button disabled={busy} onClick={() => send.mutate('send')}>
                          {row.mode === 'beta' ? 'Enviar a SUNAT BETA' : 'Simular envío'}
                        </button>
                      </>
                    )}
                    {['uncertain', 'sending', 'processing'].includes(row.status) && (
                      <button disabled={busy} onClick={() => send.mutate('query')}>
                        Consultar resultado
                      </button>
                    )}
                    {['awaiting-summary', 'accepted'].includes(row.status) &&
                      !['RC', 'RA'].includes(row.type) && (
                        <button className="secondary" onClick={() => setRelated(!related)}>
                          Preparar documento vinculado
                        </button>
                      )}
                    {related && (
                      <Preparation
                        key={id}
                        desk={desk.data}
                        parent={detail}
                        submit={prepare.mutate}
                        busy={busy}
                      />
                    )}
                  </div>
                )}
                <h3>Historial de envío</h3>
                {detail.attempts.length ? (
                  <ol className="trade-history">
                    {detail.attempts.map((a) => (
                      <li key={a.id}>
                        <strong>
                          {a.action === 'query' ? 'Consulta' : 'Envío'} · {states[a.outcome]}
                        </strong>
                        <br />
                        {new Date(a.time).toLocaleString('es-PE')} · {a.actor} · {a.detail}{' '}
                        {a.hasResponse && (
                          <a href={files + 'response?attemptId=' + a.id} download>
                            Respuesta
                          </a>
                        )}
                      </li>
                    ))}
                  </ol>
                ) : (
                  <p>Aún no hay intentos.</p>
                )}
              </section>
            )
          )}
        </div>
      )}
    </>
  )
}
