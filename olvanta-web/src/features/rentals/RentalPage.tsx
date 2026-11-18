import { useRef, useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, type Models } from '../../shared/workspace'
import { methods, money } from '../sales/sales'

const root = '/api/v1/branches/{branchId}/rentals'

type Lease = Models['LeaseView']
type Desk = Models['RentalDeskView']
type FormAction = (event: FormEvent<HTMLFormElement>, work: (data: FormData) => Promise<void>) => void
const field = (data: FormData, name: string) => String(data.get(name) ?? '').trim()
const numeric = (data: FormData, name: string) => Number(field(data, name))
function following(date: string) {
  const day = new Date(date + 'T00:00:00Z')
  day.setUTCDate(day.getUTCDate() + 1)
  return day.toISOString().slice(0, 10)
}

function LeaseForm({
  data,
  unitId,
  previous,
  save,
  busy,
}: {
  data: Desk
  unitId: number
  previous?: Lease
  save: (input: Models['LeaseInput']) => Promise<void>
  busy: boolean
}) {
  const [start] = useState(previous ? following(previous.endedOn ?? previous.end) : data.today)
  if (!data.customers.length || !data.services.length)
    return <p>Registre un cliente y un servicio de alquiler en Maestros para crear el contrato.</p>
  return (
    <form
      key={previous?.id}
      className="fields rent-form"
      onSubmit={async (e) => {
        e.preventDefault()
        const f = new FormData(e.currentTarget)
        await save({
          unitId,
          customerId: previous?.customerId ?? numeric(f, 'customer'),
          serviceId: numeric(f, 'service'),
          previousId: previous?.id ?? null,
          start: field(f, 'start'),
          end: field(f, 'end'),
          monthlyRent: numeric(f, 'monthly'),
          deposit: numeric(f, 'deposit'),
          dueDay: numeric(f, 'day'),
          conditions: field(f, 'conditions'),
        })
      }}
    >
      <h3>{previous ? 'Renovar contrato #' + previous.id : 'Nuevo contrato'}</h3>
      {previous ? (
        <p>Arrendatario · {previous.tenant}</p>
      ) : (
        <label>
          Arrendatario
          <select name="customer" required>
            {data.customers.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </label>
      )}
      <label>
        Servicio y moneda
        <select name="service" defaultValue={previous?.serviceId} required>
          {data.services.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
      </label>
      <label>
        Inicio de vigencia
        <input name="start" type="date" defaultValue={start} required readOnly={!!previous} />
      </label>
      <label>
        Fin pactado
        <input name="end" type="date" min={start && previous ? start : undefined} required />
      </label>
      <label>
        Renta mensual con impuestos
        <input
          name="monthly"
          type="number"
          min=".01"
          step=".01"
          defaultValue={previous?.monthlyRent}
          required
        />
      </label>
      <label>
        Garantía pactada
        <input
          name="deposit"
          type="number"
          min="0"
          step=".01"
          defaultValue={previous?.deposit ?? 0}
          required
        />
      </label>
      <label>
        Día de vencimiento
        <input name="day" type="number" min="1" max="31" defaultValue={previous?.dueDay ?? 1} required />
      </label>
      <label>
        Condiciones acordadas
        <textarea name="conditions" defaultValue={previous?.conditions} maxLength={1000} required />
      </label>
      <button disabled={busy}>Guardar contrato</button>
    </form>
  )
}

export default function RentalPage({ branch }: { branch: Models['BranchAccess'] }) {
  const cache = useQueryClient()
  const [date, setDate] = useState('')
  const [unitId, setUnit] = useState<number>()
  const [leaseId, setLease] = useState<number>()
  const [view, setView] = useState<
    'account' | 'unit' | 'lease' | 'renew' | 'end' | 'deposit' | 'extra' | 'pay'
  >('account')
  const [payId, setPay] = useState<number>()
  const pendingKeys = useRef(new Map<string, string>())
  const can = (p: string) => branch.permissions.includes(p)
  const params = { path: { branchId: Number(branch.id) } }
  const query = useQuery({
    queryKey: ['rentals', branch.id, date],
    queryFn: async () =>
      (await api.GET(root, { params: { ...params, query: { date: date || undefined } } })).data!,
  })
  const mutation = useMutation({
    mutationFn: (work: () => Promise<void>) => work(),
    onSuccess: async () => {
      setView('account')
      await Promise.all(
        ['rentals', 'sales', 'cash', 'billing'].map((k) =>
          cache.invalidateQueries({ queryKey: [k, branch.id] }),
        ),
      )
    },
  })
  const submit: FormAction = (e, work) => {
    e.preventDefault()
    const f = new FormData(e.currentTarget)
    mutation.mutate(() => work(f))
  }
  const data = query.data
  const unit = data?.units.find((u) => Number(u.id) === unitId) ?? data?.units[0]
  const leases = data?.contracts.filter((c) => c.unitId === unit?.id) ?? []
  const lease = leases.find((c) => Number(c.id) === leaseId) ?? leases[0]
  const charges = data?.charges.filter((q) => q.leaseId === lease?.id) ?? []
  const charge = charges.find((q) => Number(q.id) === payId)
  const visibleDate = date || data?.today || ''
  const path = { path: { branchId: Number(branch.id), id: Number(lease?.id) } }
  const busy = mutation.isPending
  const choose = (next: typeof view) => {
    mutation.reset()
    setView(next)
  }
  const cash = can('cash.write') && data?.cashSessionId != null && lease?.currency === data.cashCurrency
  async function withKey(payload: object, post: (key: string) => Promise<unknown>) {
    const group = JSON.stringify(payload)
    const key = pendingKeys.current.get(group) ?? crypto.randomUUID()
    pendingKeys.current.set(group, key)
    await post(key)
    pendingKeys.current.delete(group)
  }
  return (
    <>
      <header className="section-head directory-heading">
        <div>
          <p>Inmuebles · {branch.name}</p>
          <h1>Alquiler de locales</h1>
        </div>
        <div className="actions">
          <button className="secondary" onClick={() => void query.refetch()}>
            Actualizar
          </button>
          {can('rentals.write') && <button onClick={() => choose('unit')}>Registrar local</button>}
        </div>
      </header>
      {query.error && <p role="alert">{query.error.message}</p>}
      {mutation.error && <p role="alert">{mutation.error.message}</p>}
      {mutation.isSuccess && <p role="status">Operación registrada.</p>}
      {query.isPending && <p>Cargando locales…</p>}
      {data && (
        <div className="trade-layout">
          <aside className="panel fields">
            <h2>Locales</h2>
            <label>
              Disponibilidad al
              <input type="date" value={visibleDate} onChange={(e) => setDate(e.target.value)} />
            </label>
            <ul className="trade-list">
              {data.units.map((u) => (
                <li key={u.id}>
                  <button
                    className="linkbtn"
                    aria-pressed={u.id === unit?.id}
                    onClick={() => {
                      setUnit(Number(u.id))
                      setLease(undefined)
                      choose('account')
                    }}
                  >
                    {u.code} · {u.name}
                  </button>
                  <small>{u.occupied ? 'Ocupado' : 'Disponible'}</small>
                </li>
              ))}
            </ul>
            {!data.units.length && <p>Aún no hay locales en esta sucursal.</p>}
          </aside>
          <section className="panel fields">
            {view === 'unit' ? (
              <form
                className="fields rent-form"
                onSubmit={(e) =>
                  submit(e, async (f) => {
                    const result = await api.POST('/api/v1/branches/{branchId}/rentals/units', {
                      params,
                      body: {
                        name: field(f, 'name'),
                        code: field(f, 'code'),
                        address: field(f, 'address'),
                        features: field(f, 'features'),
                      },
                    })
                    setUnit(Number(result.data))
                    setLease(undefined)
                  })
                }
              >
                <h2>Registrar local</h2>
                <label>
                  Código del local
                  <input name="code" maxLength={30} required />
                </label>
                <label>
                  Nombre del local
                  <input name="name" maxLength={100} required />
                </label>
                <label>
                  Dirección
                  <input name="address" maxLength={200} required />
                </label>
                <label>
                  Características
                  <textarea name="features" maxLength={500} />
                </label>
                <button disabled={busy}>Guardar local</button>
              </form>
            ) : unit ? (
              <>
                <header>
                  <h2>
                    {unit.code} · {unit.name}
                  </h2>
                  <p>{unit.address}</p>
                  {unit.features && <p>{unit.features}</p>}
                </header>
                <div className="actions">
                  {can('rentals.write') && (
                    <button className="secondary" onClick={() => choose('lease')}>
                      Crear contrato
                    </button>
                  )}
                </div>
                {leases.length > 0 && (
                  <label>
                    Contrato
                    <select
                      value={lease?.id}
                      onChange={(e) => {
                        setLease(Number(e.target.value))
                        choose('account')
                      }}
                    >
                      {leases.map((c) => (
                        <option key={c.id} value={c.id}>
                          #{c.id} · {c.tenant} · {c.start} a {c.endedOn ?? c.end}
                        </option>
                      ))}
                    </select>
                  </label>
                )}
                {view === 'lease' || view === 'renew' ? (
                  <LeaseForm
                    key={view + '-' + unit.id + '-' + lease?.id}
                    data={data}
                    unitId={Number(unit.id)}
                    previous={view === 'renew' ? lease : undefined}
                    busy={busy}
                    save={async (body) => {
                      mutation.mutate(async () => {
                        const result = await api.POST('/api/v1/branches/{branchId}/rentals/contracts', {
                          params,
                          body,
                        })
                        setLease(Number(result.data))
                      })
                    }}
                  />
                ) : lease ? (
                  <>
                    <div className="rent-account">
                      <div>
                        <h3>{lease.tenant}</h3>
                        <p>
                          Vigencia {lease.start} a {lease.end} · vence el día {lease.dueDay}
                        </p>
                        <p>{lease.conditions}</p>
                        {lease.previousId && <p>Renovación del contrato #{lease.previousId}</p>}
                        {lease.endedOn && (
                          <p>
                            Finalización {lease.endedOn} · {lease.endReason}
                          </p>
                        )}
                      </div>
                      <dl>
                        <dt>Renta mensual</dt>
                        <dd>{money(lease.monthlyRent, lease.currency)}</dd>
                        <dt>Deuda de rentas y cargos</dt>
                        <dd>
                          {money(
                            charges.reduce((a, q) => a + Number(q.balance), 0),
                            lease.currency,
                          )}
                        </dd>
                        <dt>Garantía en custodia / pactada</dt>
                        <dd>
                          {money(lease.held, lease.currency)} / {money(lease.deposit, lease.currency)}
                        </dd>
                      </dl>
                    </div>
                    {can('rentals.write') && (
                      <form
                        className="fields rent-period"
                        onSubmit={(e) =>
                          submit(e, async (f) => {
                            await api.POST('/api/v1/branches/{branchId}/rentals/contracts/{id}/months', {
                              params: path,
                              body: { month: field(f, 'month') + '-01' },
                            })
                          })
                        }
                      >
                        <label>
                          Periodo de renta
                          <input
                            key={lease.id}
                            type="month"
                            name="month"
                            defaultValue={(data.today < lease.start
                              ? lease.start
                              : data.today > (lease.endedOn ?? lease.end)
                                ? (lease.endedOn ?? lease.end)
                                : data.today
                            ).slice(0, 7)}
                            required
                          />
                        </label>
                        <button disabled={busy}>Generar cuota</button>
                      </form>
                    )}
                    <div className="actions">
                      {can('rentals.extra') && (
                        <button className="secondary" onClick={() => choose('extra')}>
                          Autorizar cargo adicional
                        </button>
                      )}
                      {cash && (
                        <button className="secondary" onClick={() => choose('deposit')}>
                          Operar garantía
                        </button>
                      )}
                      {can('rentals.write') &&
                        !data.contracts.some((c) => c.previousId === lease.id) && (
                          <button className="secondary" onClick={() => choose('renew')}>
                            Renovar
                          </button>
                        )}
                      {can('rentals.write') && !lease.endedOn && (
                        <button className="secondary" onClick={() => choose('end')}>
                          Finalizar contrato
                        </button>
                      )}
                    </div>
                    {can('cash.write') && !cash && (
                      <p>
                        Para cobrar o devolver garantías abra una caja en {lease.currency}.{' '}
                        <Link to="/ventas">Ventas y caja</Link>
                      </p>
                    )}
                    {view === 'end' && (
                      <form
                        className="fields rent-form"
                        onSubmit={(e) =>
                          submit(e, async (f) => {
                            await api.POST('/api/v1/branches/{branchId}/rentals/contracts/{id}/end', {
                              params: path,
                              body: { date: field(f, 'date'), reason: field(f, 'reason') },
                            })
                          })
                        }
                      >
                        <h3>Finalización</h3>
                        <label>
                          Último día de vigencia
                          <input type="date" name="date" min={lease.start} max={lease.end} required />
                        </label>
                        <label>
                          Motivo de finalización
                          <input name="reason" maxLength={200} required />
                        </label>
                        <button disabled={busy}>Confirmar finalización</button>
                      </form>
                    )}
                    {view === 'extra' && (
                      <form
                        className="fields rent-form"
                        onSubmit={(e) =>
                          submit(e, async (f) => {
                            await api.POST('/api/v1/branches/{branchId}/rentals/contracts/{id}/extras', {
                              params: path,
                              body: {
                                reference: field(f, 'reference'),
                                reason: field(f, 'reason'),
                                due: field(f, 'due'),
                                amount: numeric(f, 'amount'),
                              },
                            })
                          })
                        }
                      >
                        <h3>Cargo adicional</h3>
                        <label>
                          Referencia única
                          <input name="reference" maxLength={64} required />
                        </label>
                        <label>
                          Concepto autorizado
                          <input name="reason" maxLength={160} required />
                        </label>
                        <label>
                          Vencimiento
                          <input
                            type="date"
                            name="due"
                            min={lease.start}
                            max={lease.endedOn ?? lease.end}
                            required
                          />
                        </label>
                        <label>
                          Importe del cargo
                          <input type="number" name="amount" step=".01" min=".01" required />
                        </label>
                        <button disabled={busy}>Guardar cargo autorizado</button>
                      </form>
                    )}
                    {view === 'deposit' && cash && (
                      <form
                        className="fields rent-form"
                        onSubmit={(e) =>
                          submit(e, async (f) => {
                            const body = {
                              sessionId: Number(data.cashSessionId),
                              kind: field(f, 'kind'),
                              amount: numeric(f, 'amount'),
                              method: field(f, 'method'),
                              note: field(f, 'note'),
                            }
                            await withKey({ lease: lease.id, ...body }, (requestId) =>
                              api.POST('/api/v1/branches/{branchId}/rentals/contracts/{id}/deposits', {
                                params: path,
                                body: { ...body, requestId },
                              }),
                            )
                          })
                        }
                      >
                        <h3>Movimiento de garantía</h3>
                        <label>
                          Operación de garantía
                          <select name="kind">
                            <option value="deposit-in">Recibir garantía</option>
                            <option value="deposit-out">Devolver garantía</option>
                          </select>
                        </label>
                        <PaymentFields />
                        <label>
                          Detalle del movimiento
                          <input name="note" maxLength={140} required />
                        </label>
                        <button disabled={busy}>Registrar garantía</button>
                      </form>
                    )}
                    <h3>Cuotas y cargos</h3>
                    {charges.length ? (
                      <div className="table-wrap">
                        <table>
                          <thead>
                            <tr>
                              {['Concepto', 'Vence', 'Importe', 'Saldo', 'Estado', 'Acciones'].map(
                                (t) => (
                                  <th key={t}>{t}</th>
                                ),
                              )}
                            </tr>
                          </thead>
                          <tbody>
                            {charges.map((q) => (
                              <tr key={q.id}>
                                <td>
                                  {q.description}
                                  <small>Cuenta #{q.saleId}</small>
                                </td>
                                <td>{q.due}</td>
                                <td>{money(q.amount, lease.currency)}</td>
                                <td>{money(q.balance, lease.currency)}</td>
                                <td>
                                  {Number(q.balance) === 0
                                    ? 'Pagada'
                                    : q.due < data.today
                                      ? 'Vencida'
                                      : Number(q.balance) < Number(q.amount)
                                        ? 'Pago parcial'
                                        : 'Pendiente'}
                                </td>
                                <td>
                                  {cash && Number(q.balance) > 0 && (
                                    <button
                                      className="linkbtn"
                                      onClick={() => {
                                        setPay(Number(q.id))
                                        choose('pay')
                                      }}
                                    >
                                      Cobrar
                                    </button>
                                  )}{' '}
                                  {can('billing.read') && (
                                    <Link to={'/facturacion?venta=' + q.saleId}>Facturación</Link>
                                  )}
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    ) : (
                      <p>Aún no hay cuotas ni cargos generados.</p>
                    )}
                    {view === 'pay' && charge && cash && (
                      <form
                        className="fields rent-form"
                        key={charge.id}
                        onSubmit={(e) =>
                          submit(e, async (f) => {
                            const body = {
                              kind: 'pay',
                              saleId: charge.saleId,
                              cashSessionId: data.cashSessionId,
                              amount: numeric(f, 'amount'),
                              method: field(f, 'method'),
                            }
                            await withKey(body, (requestId) =>
                              api.POST('/api/v1/branches/{branchId}/sales/actions', {
                                params,
                                body: { ...body, requestId },
                              }),
                            )
                          })
                        }
                      >
                        <h3>Cobrar {charge.description}</h3>
                        <PaymentFields max={Number(charge.balance)} />
                        <button disabled={busy}>Registrar cobro</button>
                      </form>
                    )}
                    {data.deposits.some((d) => d.leaseId === lease.id) && (
                      <>
                        <h3>Historial de garantías</h3>
                        <ul className="trade-history">
                          {data.deposits
                            .filter((d) => d.leaseId === lease.id)
                            .map((d) => (
                              <li key={d.id}>
                                {d.kind === 'deposit-in' ? 'Recibida' : 'Devuelta'} ·{' '}
                                {money(d.amount, lease.currency)} · {methods[d.method]} · {d.actor}
                                <small>
                                  {d.note} · {new Date(d.time).toLocaleString('es-PE')}
                                </small>
                              </li>
                            ))}
                        </ul>
                      </>
                    )}
                  </>
                ) : (
                  <p>Este local aún no tiene contratos.</p>
                )}
              </>
            ) : (
              <p>Seleccione o registre un local para consultar sus contratos.</p>
            )}
          </section>
        </div>
      )}
    </>
  )
}

function PaymentFields({ max }: { max?: number }) {
  return (
    <>
      <label>
        Importe del movimiento
        <input type="number" name="amount" min=".01" max={max} step=".01" required />
      </label>
      <label>
        Medio de pago
        <select name="method">
          {Object.entries(methods).map(([key, label]) => (
            <option value={key} key={key}>
              {label}
            </option>
          ))}
        </select>
      </label>
    </>
  )
}
