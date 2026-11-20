import { useRef, useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, type Models } from '../../shared/workspace'
import { methods, money } from '../sales/sales'

type Desk = Models['ParkingDeskView']
type Form = FormEvent<HTMLFormElement>
const root = '/api/v1/branches/{branchId}/parking'
const value = (form: FormData, field: string) => String(form.get(field) ?? '').trim()
const integer = (form: FormData, field: string) => Number(value(form, field))
const localTime = (timestamp: string) =>
  new Date(timestamp).toLocaleString('es-PE', { timeZone: 'America/Lima' })

function PaymentFields({ customers }: { customers: Desk['customers'] }) {
  return (
    <>
      <label>
        Medio de pago
        <select name="method">
          {Object.entries(methods).map(([id, name]) => (
            <option key={id} value={id}>
              {name}
            </option>
          ))}
        </select>
      </label>
      <label>
        Cliente
        <select name="customer">
          <option value="">Sin ficha de cliente</option>
          {customers.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
      </label>
    </>
  )
}

export default function ParkingPage({ branch }: { branch: Models['BranchAccess'] }) {
  const cache = useQueryClient()
  const [siteId, setSite] = useState<number>()
  const [stayId, setStay] = useState<number>()
  const [passId, setPass] = useState<number>()
  const [mode, setMode] = useState<'entry' | 'history' | 'passes' | 'setup'>('entry')
  const [find, setFind] = useState('')
  const [historyPlate, setHistoryPlate] = useState('')
  const pendingKeys = useRef(new Map<string, string>())
  const branchId = Number(branch.id)
  const scope = { path: { branchId } }
  const can = (permission: string) => branch.permissions.includes(permission)
  const desk = useQuery({
    queryKey: ['parking', branch.id, siteId, historyPlate],
    queryFn: async () =>
      (
        await api.GET(root, {
          params: { ...scope, query: { siteId, historyPlate: historyPlate || undefined } },
        })
      ).data!,
  })
  const data = desk.data
  const site = data?.sites.find((s) => s.id === data.siteId)
  const stay = data?.stays.find((s) => Number(s.id) === stayId)
  const pass = data?.passes.find((p) => Number(p.id) === passId)
  const params = { path: { branchId, siteId: Number(site?.id) } }
  const ticketParams = { path: { ...params.path, id: Number(stay?.id) } }
  const quote = useQuery({
    queryKey: ['parking', branch.id, 'quote', site?.id, stay?.id],
    enabled: !!stay && !stay.exited && can('parking.collect'),
    queryFn: async () =>
      (
        await api.GET('/api/v1/branches/{branchId}/parking/sites/{siteId}/stays/{id}/quote', {
          params: ticketParams,
        })
      ).data!,
  })
  const act = useMutation({
    mutationFn: (work: () => Promise<void>) => work(),
    onSuccess: async () => {
      await Promise.all(
        ['parking', 'cash', 'sales', 'billing'].map((k) =>
          cache.invalidateQueries({ queryKey: [k, branch.id] }),
        ),
      )
    },
  })
  const submit = (e: Form, work: (fields: FormData) => Promise<void>) => {
    e.preventDefault()
    const fields = new FormData(e.currentTarget)
    act.mutate(() => work(fields))
  }
  async function retry<T>(payload: object, run: (requestId: string) => Promise<T>) {
    const key = JSON.stringify(payload)
    const requestId = pendingKeys.current.get(key) ?? crypto.randomUUID()
    pendingKeys.current.set(key, requestId)
    const result = await run(requestId)
    pendingKeys.current.delete(key)
    return result
  }
  const busy = act.isPending
  const choose = (tab: typeof mode) => {
    act.reset()
    setMode(tab)
    setFind('')
    setHistoryPlate('')
    setStay(undefined)
    setPass(undefined)
  }
  const report = (id: number) => {
    act.reset()
    setStay(id)
    setPass(undefined)
  }
  const q = quote.data
  const toPrint = mode !== 'setup' && (stay || pass)
  return (
    <>
      <div className="screen-only">
        <header className="section-head directory-heading">
          <div>
            <p>{branch.name}</p>
            <h1>Estacionamientos</h1>
          </div>
          <button className="secondary" onClick={() => void desk.refetch()}>
            Actualizar ocupación
          </button>
        </header>
        {desk.error && <p role="alert">{desk.error.message}</p>}
        {desk.error && historyPlate && (
          <button
            className="secondary"
            onClick={() => {
              setFind('')
              setHistoryPlate('')
            }}
          >
            Limpiar búsqueda
          </button>
        )}
        {act.error && <p role="alert">{act.error.message}</p>}
        {desk.isPending && <p role="status">Consultando estacionamientos…</p>}
        {data && (
          <>
            <div className="parking-toolbar fields">
              {site && (
                <label>
                  Estacionamiento
                  <select
                    value={site.id}
                    onChange={(e) => {
                      setSite(Number(e.target.value))
                      choose('entry')
                    }}
                  >
                    {data.sites.map((s) => (
                      <option key={s.id} value={s.id}>
                        {s.name}
                      </option>
                    ))}
                  </select>
                </label>
              )}
              <div className="actions">
                {site && (
                  <>
                    <button
                      className="secondary"
                      aria-pressed={mode === 'entry'}
                      onClick={() => choose('entry')}
                    >
                      En curso
                    </button>
                    <button
                      className="secondary"
                      aria-pressed={mode === 'history'}
                      onClick={() => choose('history')}
                    >
                      Salidas
                    </button>
                    <button
                      className="secondary"
                      aria-pressed={mode === 'passes'}
                      onClick={() => choose('passes')}
                    >
                      Abonos
                    </button>
                  </>
                )}
                {can('parking.setup') && (
                  <button
                    className="secondary"
                    aria-pressed={mode === 'setup'}
                    onClick={() => choose('setup')}
                  >
                    Configuración
                  </button>
                )}
              </div>
            </div>
            {!site && mode !== 'setup' && <p>Aún no hay estacionamientos en esta sucursal.</p>}
            {site && (
              <div className="parking-zones">
                {data.zones.map((z) => (
                  <article className="panel parking-zone" key={z.id}>
                    <strong>{z.name}</strong>
                    <span>
                      {z.occupied} / {z.capacity} plazas
                    </span>
                    <progress
                      aria-label={'Ocupación de ' + z.name}
                      max={Number(z.capacity)}
                      value={Number(z.occupied)}
                    />
                  </article>
                ))}
              </div>
            )}
            {mode === 'setup' && (
              <div className="stock-layout">
                <form
                  className="panel fields"
                  onSubmit={(e) =>
                    submit(e, async (f) => {
                      const r = await api.POST('/api/v1/branches/{branchId}/parking/sites', {
                        params: scope,
                        body: { name: value(f, 'name') },
                      })
                      setSite(Number(r.data))
                    })
                  }
                >
                  <h2>Nuevo estacionamiento</h2>
                  <label>
                    Nombre del estacionamiento
                    <input name="name" maxLength={100} required />
                  </label>
                  <button disabled={busy}>Guardar estacionamiento</button>
                </form>
                {site && (
                  <>
                    <form
                      className="panel fields"
                      key={'zone-' + site.id}
                      onSubmit={(e) =>
                        submit(e, async (f) => {
                          await api.POST('/api/v1/branches/{branchId}/parking/sites/{siteId}/zones', {
                            params,
                            body: { name: value(f, 'name'), capacity: integer(f, 'capacity') },
                          })
                        })
                      }
                    >
                      <h2>Nueva zona</h2>
                      <label>
                        Nombre de la zona
                        <input name="name" maxLength={100} required />
                      </label>
                      <label>
                        Capacidad
                        <input type="number" name="capacity" min="1" max="10000" required />
                      </label>
                      <button disabled={busy}>Guardar zona</button>
                    </form>
                    <form
                      className="panel fields"
                      key={'rate-' + site.id}
                      onSubmit={(e) =>
                        submit(e, async (f) => {
                          await api.POST('/api/v1/branches/{branchId}/parking/sites/{siteId}/rates', {
                            params,
                            body: {
                              serviceId: integer(f, 'service'),
                              blockMinutes: integer(f, 'block'),
                              graceMinutes: integer(f, 'grace'),
                              blockPrice: integer(f, 'price'),
                            },
                          })
                        })
                      }
                    >
                      <h2>Tarifa de ingreso</h2>
                      <label>
                        Servicio para facturación
                        <select name="service" required defaultValue={data.rate?.serviceId}>
                          {data.services.map((s) => (
                            <option key={s.id} value={s.id}>
                              {s.name}
                            </option>
                          ))}
                        </select>
                      </label>
                      {!data.services.length && (
                        <p>Registre un servicio en el catálogo en {site.currency}.</p>
                      )}
                      <label>
                        Minutos por bloque
                        <input
                          type="number"
                          name="block"
                          min="1"
                          max="1440"
                          defaultValue={data.rate?.blockMinutes ?? 60}
                          required
                        />
                      </label>
                      <label>
                        Minutos de tolerancia
                        <input
                          type="number"
                          name="grace"
                          min="0"
                          max="1440"
                          defaultValue={data.rate?.graceMinutes ?? 0}
                          required
                        />
                      </label>
                      <label>
                        Precio por bloque con impuesto
                        <input
                          type="number"
                          name="price"
                          min=".01"
                          step=".01"
                          defaultValue={data.rate?.blockPrice}
                          required
                        />
                      </label>
                      <button disabled={busy || !data.services.length}>Publicar tarifa</button>
                    </form>
                  </>
                )}
              </div>
            )}
            {site && (mode === 'entry' || mode === 'history') && (
              <div className="parking-work">
                <section className="panel fields">
                  <h2>{mode === 'entry' ? 'Vehículos en curso' : 'Últimas salidas'}</h2>
                  {mode === 'entry' ? (
                    <label>
                      Filtrar por placa
                      <input value={find} onChange={(e) => setFind(e.target.value)} />
                    </label>
                  ) : (
                    <form
                      className="fields rent-period"
                      onSubmit={(e) => {
                        e.preventDefault()
                        setStay(undefined)
                        setHistoryPlate(find.trim())
                      }}
                    >
                      <label>
                        Placa en el historial
                        <input maxLength={24} value={find} onChange={(e) => setFind(e.target.value)} />
                      </label>
                      <button className="secondary">Buscar salidas</button>
                    </form>
                  )}
                  <div className="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>Ticket y placa</th>
                          <th>Zona</th>
                          <th>{mode === 'entry' ? 'Ingreso' : 'Salida'}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {data.stays
                          .filter(
                            (s) =>
                              !!s.exited === (mode === 'history') &&
                              (mode === 'history' ||
                                s.plate.includes(find.toUpperCase().replace(/[\s-]/g, ''))),
                          )
                          .map((s) => (
                            <tr key={s.id}>
                              <td>
                                <button className="linkbtn" onClick={() => report(Number(s.id))}>
                                  #{s.id} · {s.plate}
                                </button>
                              </td>
                              <td>{data.zones.find((z) => z.id === s.zoneId)?.name}</td>
                              <td>{localTime(s.exited ?? s.entered)}</td>
                            </tr>
                          ))}
                      </tbody>
                    </table>
                  </div>
                </section>
                {mode === 'entry' && can('parking.enter') && (
                  <form
                    key={'entry-' + site.id}
                    className="panel fields"
                    onSubmit={(e) =>
                      submit(e, async (f) => {
                        const body = { zoneId: integer(f, 'zone'), plate: value(f, 'plate') }
                        const r = await retry({ type: 'entry', site: site.id, ...body }, (requestId) =>
                          api.POST('/api/v1/branches/{branchId}/parking/sites/{siteId}/entries', {
                            params,
                            body: { ...body, requestId },
                          }),
                        )
                        setStay(Number(r.data))
                        setPass(undefined)
                      })
                    }
                  >
                    <h2>Registrar ingreso</h2>
                    <label>
                      Placa del vehículo
                      <input name="plate" maxLength={24} required />
                    </label>
                    <label>
                      Zona de ingreso
                      <select name="zone" required>
                        {data.zones.map((z) => (
                          <option
                            disabled={Number(z.occupied) >= Number(z.capacity)}
                            key={z.id}
                            value={z.id}
                          >
                            {z.name}
                          </option>
                        ))}
                      </select>
                    </label>
                    {data.rate ? (
                      <p>
                        {money(data.rate.blockPrice, site.currency)} cada {data.rate.blockMinutes} min ·{' '}
                        {data.rate.graceMinutes} min de tolerancia
                      </p>
                    ) : (
                      <p>Configure la tarifa del estacionamiento.</p>
                    )}
                    <button
                      disabled={
                        busy ||
                        !data.rate ||
                        !data.zones.some((z) => Number(z.capacity) > Number(z.occupied))
                      }
                    >
                      Emitir ticket
                    </button>
                  </form>
                )}
              </div>
            )}
            {site && mode === 'passes' && (
              <div className="parking-work">
                <section className="panel fields">
                  <h2>Últimos abonos</h2>
                  <ul className="trade-list">
                    {data.passes.map((p) => (
                      <li key={p.id}>
                        <button
                          className="linkbtn"
                          onClick={() => {
                            setPass(Number(p.id))
                            setStay(undefined)
                          }}
                        >
                          #{p.id} · {p.plate}
                        </button>
                        <small>
                          {p.from} a {p.through} · {money(p.price, site.currency)}
                        </small>
                      </li>
                    ))}
                  </ul>
                  {!data.passes.length && <p>Aún no hay abonos.</p>}
                </section>
                {can('parking.collect') && (
                  <form
                    key={'pass-' + site.id}
                    className="panel fields"
                    onSubmit={(e) =>
                      submit(e, async (f) => {
                        const body = {
                          plate: value(f, 'plate'),
                          from: value(f, 'from'),
                          through: value(f, 'through'),
                          price: integer(f, 'price'),
                          sessionId: Number(data.cashSessionId),
                          method: value(f, 'method'),
                          customerId: integer(f, 'customer') || null,
                        }
                        const r = await retry({ type: 'pass', site: site.id, ...body }, (requestId) =>
                          api.POST('/api/v1/branches/{branchId}/parking/sites/{siteId}/passes', {
                            params,
                            body: { ...body, requestId },
                          }),
                        )
                        setPass(Number(r.data))
                        setStay(undefined)
                      })
                    }
                  >
                    <h2>Vender abono</h2>
                    <label>
                      Placa del abono
                      <input name="plate" maxLength={24} required />
                    </label>
                    <label>
                      Desde
                      <input
                        type="date"
                        name="from"
                        min={data.today}
                        defaultValue={data.today}
                        required
                      />
                    </label>
                    <label>
                      Hasta
                      <input
                        type="date"
                        name="through"
                        min={data.today}
                        defaultValue={data.today}
                        required
                      />
                    </label>
                    <label>
                      Precio del abono
                      <input type="number" min=".01" step=".01" name="price" required />
                    </label>
                    <PaymentFields customers={data.customers} />
                    {!data.cashSessionId && (
                      <p>La sucursal necesita una caja abierta para vender abonos.</p>
                    )}
                    <button disabled={busy || !data.cashSessionId || !data.rate}>Cobrar abono</button>
                  </form>
                )}
              </div>
            )}
          </>
        )}
      </div>
      {site && data && toPrint && (
        <article className="panel trade-print parking-ticket" aria-label="Ticket de estacionamiento">
          <h2>
            {stay
              ? stay.exited
                ? 'Comprobante de salida'
                : 'Ticket de ingreso'
              : 'Comprobante de abono'}{' '}
            #{stay?.id ?? pass?.id}
          </h2>
          <p>
            {site.name} · <strong>{stay?.plate ?? pass?.plate}</strong>
          </p>
          {stay && (
            <>
              <p>
                {data.zones.find((z) => z.id === stay.zoneId)?.name} · Ingreso {localTime(stay.entered)}
              </p>
              <p>
                {money(stay.blockPrice, site.currency)} cada {stay.blockMinutes} min · Tolerancia{' '}
                {stay.graceMinutes} min
              </p>
              {stay.passId && <p>Abono vinculado #{stay.passId}</p>}
              {stay.exited && (
                <p>
                  Salida {localTime(stay.exited)} · {stay.blocks} bloques ·{' '}
                  <strong>{money(stay.amount ?? 0, site.currency)}</strong>{' '}
                  {stay.method && '· ' + methods[stay.method]}
                </p>
              )}
            </>
          )}
          {pass && (
            <p>
              {pass.from} a {pass.through} · <strong>{money(pass.price, site.currency)}</strong> ·{' '}
              {methods[pass.method ?? '']}
            </p>
          )}
          {(stay?.paymentId || pass?.paymentId) && (
            <p>Movimiento de caja #{stay?.paymentId ?? pass?.paymentId}</p>
          )}
          <div className="actions screen-only">
            <button className="secondary" onClick={() => window.print()}>
              Imprimir
            </button>
            {(stay?.saleId || pass?.saleId) && can('billing.read') && (
              <Link to={'/facturacion?venta=' + (stay?.saleId ?? pass?.saleId)}>Facturación</Link>
            )}
            <button
              className="quiet"
              onClick={() => {
                setStay(undefined)
                setPass(undefined)
              }}
            >
              Cerrar ticket
            </button>
          </div>
          {stay && !stay.exited && can('parking.collect') && (
            <div className="screen-only">
              <button className="secondary" onClick={() => void quote.refetch()}>
                Recalcular salida
              </button>
              {quote.error && <p role="alert">{quote.error.message}</p>}
              {q && (
                <form
                  className="fields trade-action"
                  key={stay.id}
                  onSubmit={(e) =>
                    submit(e, async (f) => {
                      const paid = Number(q.amount) > 0
                      const body = {
                        expectedAmount: q.amount,
                        sessionId: paid ? data.cashSessionId : null,
                        method: paid ? value(f, 'method') : null,
                        customerId: paid ? integer(f, 'customer') || null : null,
                      }
                      await retry({ type: 'exit', stay: stay.id, ...body }, (requestId) =>
                        api.POST('/api/v1/branches/{branchId}/parking/sites/{siteId}/stays/{id}/exit', {
                          params: ticketParams,
                          body: { ...body, requestId },
                        }),
                      )
                    })
                  }
                >
                  <p>
                    {q.blocks} bloques · <strong>{money(q.amount, q.currency)}</strong>
                  </p>
                  {Number(q.amount) > 0 && <PaymentFields customers={data.customers} />}
                  {Number(q.amount) > 0 && !data.cashSessionId && (
                    <p>
                      La sucursal necesita una caja abierta para cobrar.{' '}
                      {can('cash.write') && <Link to="/ventas">Ventas y caja</Link>}
                    </p>
                  )}
                  <button
                    disabled={busy || quote.isFetching || (Number(q.amount) > 0 && !data.cashSessionId)}
                  >
                    {Number(q.amount) > 0 ? 'Cobrar y cerrar' : 'Registrar salida sin cargo'}
                  </button>
                </form>
              )}
            </div>
          )}
        </article>
      )}
    </>
  )
}
