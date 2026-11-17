import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { api, collection, kinds } from '../../shared/workspace'
import type { Kind, Models } from '../../shared/workspace'
import { MasterEditor } from './MasterEditor'

function History({
  branchId,
  kind,
  entry,
}: {
  branchId: number
  kind: Kind
  entry: Models['MasterView']
}) {
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
    <section className="panel">
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
        <ul>
          {query.data?.map((a, i) => (
            <li key={i}>
              {new Date(a.time).toLocaleString('es-PE')} · {a.actor} · {actions[a.action] ?? a.action} ·{' '}
              {a.detail}
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

function ImportForm({ branchId, kind, done }: { branchId: number; kind: Kind; done: () => void }) {
  const upload = useMutation({
    mutationFn: async (form: FormData) => {
      const file = form.get('file')
      if (!(file instanceof File) || file.size > 512000)
        throw new Error('Seleccione un JSON de hasta 500 KB.')
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
    },
    onSuccess: done,
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
        Archivo JSON (hasta 200 registros)
        <input name="file" type="file" accept=".json,application/json" required />
      </label>
      <a href="/plantilla-personas.json" download>
        Descargar plantilla
      </a>
      {upload.error && <p role="alert">{upload.error.message}</p>}
      <button disabled={upload.isPending}>Validar e importar</button>
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
      <div className="section-head">
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
        <>
          <section className="panel">
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
                  <table>
                    <thead>
                      <tr>
                        <th>Nombre</th>
                        <th>{party ? 'Documento' : 'Código'}</th>
                        <th>Estado</th>
                        <th>Acciones</th>
                      </tr>
                    </thead>
                    <tbody>
                      {query.data.items.map((row) => (
                        <tr key={row.id}>
                          <td>{row.data.name}</td>
                          <td>{row.data.code}</td>
                          <td>{row.data.active ? 'Activo' : 'Inactivo'}</td>
                          <td>
                            <div className="actions">
                              <button className="linkbtn" onClick={() => setSelected(row)}>
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
                {!query.data.items.length && <p>No se encontraron registros.</p>}
                <div className="pager">
                  <span>
                    {query.data.total} registros · Página {page}
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
          {selected && query.isSuccess && <History branchId={branchId} kind={kind} entry={selected} />}
        </>
      )}
    </>
  )
}
