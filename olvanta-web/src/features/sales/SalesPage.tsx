import { useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/workspace'
import { cashPath, salesPath, stages, money, type Branch, type Command, type Quote } from './sales'
import { QuoteForm } from './QuoteForm'
import { SaleDetail } from './SaleDetail'
import { CashPage } from './CashPage'

export function SalesPage({ branch }: { branch: Branch }) {
  const [tab, setTab] = useState('Ventas')
  const [id, setId] = useState('')
  const [search, setSearch] = useState('')
  const cache = useQueryClient()
  const params = { path: { branchId: Number(branch.id) } }
  const cashRead = branch.permissions.includes('cash.read')
  const sales = useQuery({
    queryKey: ['sales', branch.id],
    queryFn: async () => (await api.GET(salesPath, { params })).data!,
  })
  const cash = useQuery({
    queryKey: ['cash', branch.id],
    enabled: cashRead,
    queryFn: async () => (await api.GET(cashPath, { params })).data!,
  })
  const session = cash.data?.find((s) => !s.closed)
  const retry = useRef(new Map<string, string>())
  const refresh = () =>
    Promise.all(
      ['sales', 'cash', 'sales-catalog', 'inventory'].map((k) =>
        cache.invalidateQueries({ queryKey: [k, branch.id] }),
      ),
    )
  const mutation = useMutation({
    mutationFn: async (input: Command | Quote) => {
      const key = JSON.stringify(input)
      const requestId = retry.current.get(key) ?? crypto.randomUUID()
      retry.current.set(key, requestId)
      const result =
        'lines' in input
          ? await api.POST('/api/v1/branches/{branchId}/sales/quotes', {
              params,
              body: { ...input, requestId },
            })
          : await api.POST('/api/v1/branches/{branchId}/sales/actions', {
              params,
              body: { ...input, requestId },
            })
      retry.current.delete(key)
      return result.data!
    },
    onSuccess: async (receipt, input) => {
      await refresh()
      if ('lines' in input) {
        setTab('Ventas')
        setSearch('')
        setId(String(receipt.saleId))
      }
    },
  })
  const tabs = [
    'Ventas',
    'Por cobrar',
    ...(branch.permissions.includes('sales.write') ? ['Cotizar'] : []),
    ...(cashRead ? ['Caja'] : []),
  ]
  const list =
    sales.data?.filter(
      (s) =>
        (tab !== 'Por cobrar' || Number(s.balance) > 0) &&
        (s.customer + ' ' + s.id).toLowerCase().includes(search.toLowerCase()),
    ) ?? []
  const selected = list.find((s) => String(s.id) === id) ?? list[0]
  return (
    <>
      <header className="section-head directory-heading screen-only">
        <h1>Ventas y caja</h1>
        <button className="secondary" disabled={mutation.isPending} onClick={() => void refresh()}>
          Actualizar
        </button>
      </header>
      <div className="actions stock-tabs screen-only">
        {tabs.map((t) => (
          <button
            key={t}
            className={tab === t ? '' : 'secondary'}
            aria-pressed={tab === t}
            onClick={() => {
              setTab(t)
              mutation.reset()
            }}
          >
            {t}
          </button>
        ))}
      </div>
      {mutation.error && <p role="alert">{mutation.error.message}</p>}
      {mutation.isSuccess && (
        <p role="status" className="screen-only">
          Operación registrada.
        </p>
      )}
      {sales.error && <p role="alert">{sales.error.message}</p>}
      {cash.error && <p role="alert">{cash.error.message}</p>}
      {tab === 'Cotizar' ? (
        <QuoteForm branch={branch} busy={mutation.isPending} submit={mutation.mutate} />
      ) : tab === 'Caja' ? (
        cash.data ? (
          <CashPage
            key={cash.data[0]?.history.at(-1)?.id}
            branch={branch}
            sessions={cash.data}
            run={mutation.mutate}
            busy={mutation.isPending}
          />
        ) : (
          cash.isPending && <p role="status">Consultando caja…</p>
        )
      ) : (
        <>
          {sales.isPending && <p role="status">Consultando ventas…</p>}
          <div className="trade-layout">
            <aside className="panel screen-only">
              <h2>{tab === 'Por cobrar' ? 'Cuentas por cobrar' : 'Documentos'}</h2>
              <label>
                Buscar por cliente o número
                <input value={search} onChange={(e) => setSearch(e.target.value)} />
              </label>
              {!list.length && (
                <p>
                  {tab === 'Por cobrar'
                    ? 'Sin deudas pendientes para esta consulta.'
                    : 'Sin documentos para esta consulta.'}
                </p>
              )}
              <ul className="trade-list">
                {list.map((s) => (
                  <li key={s.id}>
                    <button
                      aria-pressed={selected?.id === s.id}
                      className="linkbtn"
                      onClick={() => setId(String(s.id))}
                    >
                      #{s.id} · {s.customer}
                    </button>
                    <small>
                      {stages[s.stage]} · {money(tab === 'Por cobrar' ? s.balance : s.total, s.currency)}
                      {s.dueDate && ' · vence ' + s.dueDate}
                    </small>
                  </li>
                ))}
              </ul>
            </aside>
            {selected && (
              <SaleDetail
                key={String(selected.id) + '-' + selected.history.at(-1)?.id}
                sale={selected}
                session={session}
                branch={branch}
                run={mutation.mutate}
                busy={mutation.isPending}
              />
            )}
          </div>
        </>
      )}
    </>
  )
}
