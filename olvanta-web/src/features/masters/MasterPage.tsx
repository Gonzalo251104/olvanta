import { useEffect, useRef, useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { api, collection, kinds } from '../../shared/workspace'
import type { Kind, Models } from '../../shared/workspace'
import { MasterEditor } from './MasterEditor'

function History({
  branchId,
  kind,
  entry,
  close,
}: {
  branchId: number
  kind: Kind
  entry: Models['MasterView']
  close: () => void
}) {
  const sheet = useRef<HTMLElement>(null)
  useEffect(() => {
    sheet.current?.focus()
  }, [entry.id])
  const query = useQuery({
    queryKey: ['history', branchId, kind, entry.id, entry.data.version],
    queryFn: async () =>
      (
        await api.GET('/api/v1/branches/{branchId}/masters/{kind}/{id}/history', {
          params: { path: { branchId, kind, id: Number(entry.id) } },
        })
      ).data,
  })
  const d = entry.data
  const details = {
    Dirección: d.address,
    Contacto: d.contact,
    Condiciones: d.terms,
    Documento: d.documentType,
    Categoría: d.category,
    Unidad: d.unit,
    Presentación: d.presentation,
    Conversión: d.conversion,
    Precio: d.price === undefined || d.price === null ? null : d.currency + ' ' + d.price,
    'Impuesto (%)': d.taxRate,
    'Código de barras': d.barcode,
  }
  const actions: Record<string, string> = {
    'master.created': 'Alta',
    'master.updated': 'Actualización',
    'master.imported': 'Importación',
  }
  return (
    <section ref={sheet} tabIndex={-1} className="panel record-sheet" aria-label="Detalle del registro">
      <div className="record-sheet-top">
        <span>{d.code}</span>
        <button className="linkbtn" onClick={close}>
          Cerrar detalle
        </button>
      </div>
      <h2>{d.name}</h2>
      <dl className="details">
        {Object.entries(details)
          .filter(([, v]) => v != null && v !== '')
          .map(([k, v]) => (
            <div key={k}>
              <dt>{k}</dt>
              <dd>{v}</dd>
            </div>
          ))}
      </dl>
      <h3>Historial</h3>
      {query.isPending ? (
        <p>Cargando…</p>
      ) : query.error ? (
        <p role="alert">{query.error.message}</p>
      ) : (
        <ol className="history-list">
          {query.data?.map((a, i) => (
            <li key={i}>
              <strong>{actions[a.action] ?? a.action}</strong>
              <time dateTime={a.time}>
                {new Date(a.time).toLocaleString('es-PE')} · {a.actor}
              </time>
              <span>{a.detail}</span>
            </li>
          ))}
        </ol>
      )}
    </section>
  )
}

function ImportForm({ branchId, kind, done }: { branchId: number; kind: Kind; done: () => void }) {
  const [csv, setCsv] = useState<{ content: string; result: Models['PartyCsvResult'] }>()
  const sendCsv = async (content: string, commit: boolean) => {
    const { data } = await api.POST('/api/v1/branches/{branchId}/masters/{kind}/import/csv', {
      params: { path: { branchId, kind } },
      body: { content, commit },
    })
    setCsv({ content, result: data! })
    if (Number(data!.imported) > 0) done()
  }
  const upload = useMutation({
    mutationFn: async (form: FormData | 'confirm') => {
      if (form === 'confirm' && csv) return sendCsv(csv.content, true)
      if (form === 'confirm') return
      const file = form.get('file')
      if (!(file instanceof File) || file.size > 512000)
        throw new Error('Seleccione un CSV o JSON de hasta 500 KB.')
      if (file.name.toLowerCase().endsWith('.csv')) {
        let content
        try {
          content = new TextDecoder('utf-8', { fatal: true }).decode(await file.arrayBuffer())
        } catch {
          throw new Error('Guarde el archivo CSV con codificación UTF-8.')
        }
        return sendCsv(content, false)
      }
      let rows
      try {
        rows = JSON.parse(await file.text())
      } catch {
        throw new Error('El archivo debe contener JSON válido.')
      }
      if (!Array.isArray(rows)) throw new Error('El archivo debe contener una lista de registros.')
      await api.POST('/api/v1/branches/{branchId}/masters/{kind}/import', {
        params: { path: { branchId, kind } },
        body: rows,
      })
      done()
    },
  })
  return (
    <form
      className="panel fields"
      onSubmit={(e) => {
        e.preventDefault()
        upload.mutate(new FormData(e.currentTarget))
      }}
    >
      <h2>Importar registros</h2>
      <label>
        Archivo CSV o JSON (hasta 200 registros)
        <input
          name="file"
          type="file"
          accept=".csv,text/csv,.json,application/json"
          required
          disabled={upload.isPending}
          onChange={() => {
            setCsv(undefined)
            upload.reset()
          }}
        />
      </label>
      <a href="/plantilla-personas.json" download>
        Descargar plantilla JSON
      </a>
      <a href="/plantilla-personas.csv" download>
        Descargar plantilla CSV
      </a>
      {upload.error && <p role="alert">{upload.error.message}</p>}
      <button disabled={upload.isPending}>Procesar archivo</button>
      {csv && (
        <section className="fields">
          <h3>Vista previa del CSV</h3>
          {csv.result.issues.length > 0 && (
            <ul className="trade-list" role="alert">
              {csv.result.issues.map((issue, index) => (
                <li key={index}>
                  Fila {issue.row}. {issue.message}
                </li>
              ))}
            </ul>
          )}
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Fila</th>
                  <th>Identificador</th>
                  <th>Nombre</th>
                </tr>
              </thead>
              <tbody>
                {csv.result.rows.map((row) => (
                  <tr key={row.row}>
                    <td>{row.row}</td>
                    <td>{row.code}</td>
                    <td>{row.name}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <button
            type="button"
            disabled={upload.isPending || csv.result.issues.length > 0}
            onClick={() => upload.mutate('confirm')}
          >
            Confirmar importación
          </button>
        </section>
      )}
    </form>
  )
}

export function MasterPage({ branch, kind }: { branch: Models['BranchAccess']; kind: Kind }) {
  const branchId = Number(branch.id)
  const [search, setSearch] = useState('')
  const [state, setState] = useState('')
  const [page, setPage] = useState(1)
  const [mode, setMode] = useState<'list' | 'edit' | 'import'>('list')
  const [selected, setSelected] = useState<Models['MasterView']>()
  const detailTrigger = useRef<HTMLButtonElement>(null)
  const [notice, setNotice] = useState('')
  const canWrite = branch.permissions.includes('masters.write')
  const party = kind === 'customers' || kind === 'suppliers'
  const query = useQuery({
    queryKey: ['masters', branchId, kind, search, state, page],
    queryFn: async ({ signal }) =>
      (
        await api.GET(collection, {
          signal,
          params: {
            path: { branchId, kind },
            query: { search, active: state === '' ? undefined : state === 'active', page, pageSize: 20 },
          },
        })
      ).data!,
  })
  const finish = (saved: boolean) => {
    setSelected(undefined)
    setMode('list')
    if (saved) {
      setNotice('Cambios guardados.')
      void query.refetch()
    }
  }
  return (
    <>
      <div className="section-head directory-heading">
        <h1>{kinds[kind]}</h1>
        {canWrite && mode === 'list' && (
          <div className="actions">
            {party && (
              <button
                className="secondary"
                onClick={() => {
                  setNotice('')
                  setMode('import')
                }}
              >
                Importar
              </button>
            )}
            <button
              onClick={() => {
                setSelected(undefined)
                setNotice('')
                setMode('edit')
              }}
            >
              Nuevo registro
            </button>
          </div>
        )}
      </div>
      {mode === 'edit' ? (
        <MasterEditor branch={branch} kind={kind} entry={selected} close={finish} />
      ) : mode === 'import' ? (
        <>
          <ImportForm branchId={branchId} kind={kind} done={() => finish(true)} />
          <button className="secondary" onClick={() => finish(false)}>
            Cancelar
          </button>
        </>
      ) : (
        <div className={selected && query.isSuccess ? 'directory with-record' : 'directory'}>
          <section className="panel record-list">
            <div className="filters">
              <label>
                Buscar por nombre o {party ? 'documento' : 'código'}
                <input
                  type="search"
                  value={search}
                  onChange={(e) => {
                    setSearch(e.target.value)
                    setPage(1)
                    setSelected(undefined)
                  }}
                />
              </label>
              <label>
                Estado
                <select
                  value={state}
                  onChange={(e) => {
                    setState(e.target.value)
                    setPage(1)
                    setSelected(undefined)
                  }}
                >
                  <option value="">Todos</option>
                  <option value="active">Activos</option>
                  <option value="inactive">Inactivos</option>
                </select>
              </label>
            </div>
            {notice && <p role="status">{notice}</p>}
            {query.isPending ? (
              <p>Cargando registros…</p>
            ) : query.error ? (
              <p role="alert">{query.error.message}</p>
            ) : (
              <>
                <div className="table-wrap">
                  <table role="table">
                    <thead role="rowgroup">
                      <tr role="row">
                        <th role="columnheader" scope="col">
                          Nombre
                        </th>
                        <th role="columnheader" scope="col">
                          {party ? 'Documento' : 'Código'}
                        </th>
                        <th role="columnheader" scope="col">
                          Estado
                        </th>
                        <th role="columnheader" scope="col">
                          Acciones
                        </th>
                      </tr>
                    </thead>
                    <tbody role="rowgroup">
                      {query.data.items.map((row) => (
                        <tr
                          role="row"
                          key={row.id}
                          className={selected?.id === row.id ? 'selected-row' : undefined}
                        >
                          <td role="cell">{row.data.name}</td>
                          <td role="cell" className="record-code">
                            {row.data.code}
                          </td>
                          <td role="cell">
                            <span className={row.data.active ? 'record-state' : 'record-state inactive'}>
                              {row.data.active ? 'Activo' : 'Inactivo'}
                            </span>
                          </td>
                          <td role="cell">
                            <div className="actions">
                              <button
                                className="linkbtn"
                                aria-pressed={selected?.id === row.id}
                                onClick={(e) => {
                                  detailTrigger.current = e.currentTarget
                                  setSelected(row)
                                }}
                              >
                                Ver detalle
                              </button>
                              {canWrite && (
                                <button
                                  className="linkbtn"
                                  onClick={() => {
                                    setSelected(row)
                                    setMode('edit')
                                    setNotice('')
                                  }}
                                >
                                  Editar
                                </button>
                              )}
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                {!query.data.items.length && (
                  <div className="empty-records">
                    <span aria-hidden="true" />
                    <p>
                      {search || state
                        ? 'No se encontraron registros.'
                        : 'Aún no hay registros en esta sucursal.'}
                    </p>
                  </div>
                )}
                <div className="pager">
                  <span>
                    {query.data.total} {Number(query.data.total) === 1 ? 'registro' : 'registros'} ·
                    Página {page}
                  </span>
                  <button
                    className="secondary"
                    disabled={page === 1}
                    onClick={() => {
                      setPage(page - 1)
                      setSelected(undefined)
                    }}
                  >
                    Anterior
                  </button>
                  <button
                    className="secondary"
                    disabled={page * 20 >= Number(query.data.total)}
                    onClick={() => {
                      setPage(page + 1)
                      setSelected(undefined)
                    }}
                  >
                    Siguiente
                  </button>
                </div>
              </>
            )}
          </section>
          {selected && query.isSuccess && (
            <History
              branchId={branchId}
              kind={kind}
              entry={selected}
              close={() => {
                setSelected(undefined)
                detailTrigger.current?.focus()
              }}
            />
          )}
        </div>
      )}
    </>
  )
}
