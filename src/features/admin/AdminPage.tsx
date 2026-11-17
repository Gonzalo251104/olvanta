import { useState } from 'react'
import { OrganizationPage } from './OrganizationPage'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, roles } from '../../shared/workspace'
import type { Models } from '../../shared/workspace'

function UserForm({
  user,
  branches,
  done,
}: {
  user?: Models['OperatorView']
  branches: Models['BranchView'][]
  done: () => void
}) {
  const save = useMutation({
    mutationFn: async (form: FormData) => {
      const grants = branches.flatMap((b) => {
        const role = String(form.get('role-' + b.id) ?? '')
        return role ? [{ branchId: Number(b.id), role }] : []
      })
      if (user)
        await api.PUT('/api/v1/admin/users/{id}/grants', {
          params: { path: { id: user.id } },
          body: { version: user.version, grants },
        })
      else
        await api.POST('/api/v1/admin/users', {
          body: { userName: String(form.get('name')), password: String(form.get('password')), grants },
        })
    },
    onSuccess: done,
  })
  return (
    <form
      className="panel fields"
      onSubmit={(e) => {
        e.preventDefault()
        save.mutate(new FormData(e.currentTarget))
      }}
    >
      <h2>{user ? 'Permisos de ' + user.userName : 'Nuevo usuario'}</h2>
      {!user && (
        <>
          <label>
            Nombre de usuario
            <input name="name" autoComplete="off" required maxLength={100} />
          </label>
          <label>
            Contraseña inicial
            <input
              name="password"
              autoComplete="new-password"
              type="password"
              minLength={12}
              maxLength={128}
              required
            />
          </label>
        </>
      )}
      <fieldset>
        <legend>Acceso por sucursal</legend>
        {branches.map((b) => (
          <label key={b.id}>
            {b.name}
            <select
              name={'role-' + b.id}
              defaultValue={user?.grants.find((g) => g.branchId === b.id)?.role ?? ''}
            >
              <option value="">Sin acceso</option>
              {Object.entries(roles)
                .filter(([k]) => k !== 'administrator')
                .map(([k, v]) => (
                  <option key={k} value={k}>
                    {v}
                  </option>
                ))}
            </select>
          </label>
        ))}
      </fieldset>
      <small>
        Supervisor y vendedor pueden editar maestros. Los demás roles asignables tienen permiso de
        consulta.
      </small>
      {save.error && <p role="alert">{save.error.message}</p>}
      <button disabled={save.isPending}>Guardar permisos</button>
    </form>
  )
}

export function AdminPage() {
  const cache = useQueryClient()
  const [tab, setTab] = useState('Usuarios')
  const [editing, setEditing] = useState<Models['OperatorView'] | 'new'>()
  const [reset, setReset] = useState<Models['OperatorView']>()
  const [rename, setRename] = useState<Models['BranchView']>()
  const [notice, setNotice] = useState('')
  const branches = useQuery({
    queryKey: ['admin', 'branches'],
    queryFn: async () => (await api.GET('/api/v1/admin/branches')).data!,
  })
  const users = useQuery({
    queryKey: ['admin', 'users'],
    queryFn: async () => (await api.GET('/api/v1/admin/users')).data!,
  })
  const audit = useQuery({
    queryKey: ['admin', 'audit'],
    enabled: tab === 'Auditoría',
    queryFn: async () => (await api.GET('/api/v1/admin/audit')).data!,
  })
  const done = () => {
    setEditing(undefined)
    setRename(undefined)
    setReset(undefined)
    setNotice('Cambios guardados.')
    void cache.invalidateQueries({ queryKey: ['admin'] })
    void cache.invalidateQueries({ queryKey: ['session'] })
  }
  const branchSave = useMutation({
    mutationFn: async (form: HTMLFormElement) => {
      const body = { name: String(new FormData(form).get('name')) }
      if (rename)
        await api.PUT('/api/v1/admin/branches/{id}', {
          params: { path: { id: Number(rename.id) } },
          body,
        })
      else await api.POST('/api/v1/admin/branches', { body })
      form.reset()
    },
    onSuccess: done,
  })
  const password = useMutation({
    mutationFn: async (form: FormData) => {
      await api.POST('/api/v1/admin/users/{id}/reset-password', {
        params: { path: { id: reset!.id } },
        body: { newPassword: String(form.get('password')) },
      })
    },
    onSuccess: done,
  })
  return (
    <>
      <h1>Administración</h1>
      <div className="tabs" aria-label="Secciones">
        {['Usuarios', 'Sucursales', 'Organización', 'Auditoría'].map((t) => (
          <button
            key={t}
            className={tab === t ? '' : 'secondary'}
            onClick={() => {
              setTab(t)
              setNotice('')
              setEditing(undefined)
              setReset(undefined)
            }}
          >
            {t}
          </button>
        ))}
      </div>
      {notice && <p role="status">{notice}</p>}
      {(branches.error || users.error || audit.error) && (
        <p role="alert">No se pudo cargar la administración.</p>
      )}
      {tab === 'Usuarios' && (
        <>
          {editing ? (
            <>
              <UserForm
                key={editing === 'new' ? 'new' : editing.id}
                user={editing === 'new' ? undefined : editing}
                branches={branches.data ?? []}
                done={done}
              />
              <button className="secondary" onClick={() => setEditing(undefined)}>
                Cancelar
              </button>
            </>
          ) : reset ? (
            <form
              className="panel fields"
              onSubmit={(e) => {
                e.preventDefault()
                password.mutate(new FormData(e.currentTarget))
              }}
            >
              <h2>Restablecer contraseña de {reset.userName}</h2>
              <label>
                Nueva contraseña
                <input
                  name="password"
                  autoComplete="new-password"
                  type="password"
                  minLength={12}
                  maxLength={128}
                  required
                />
              </label>
              <small>Se cerrarán las sesiones existentes de esta cuenta.</small>
              {password.error && <p role="alert">{password.error.message}</p>}
              <div className="actions">
                <button disabled={password.isPending}>Confirmar restablecimiento</button>
                <button type="button" className="secondary" onClick={() => setReset(undefined)}>
                  Cancelar
                </button>
              </div>
            </form>
          ) : (
            <section className="panel">
              <div className="section-head">
                <h2>Usuarios</h2>
                <button
                  disabled={!branches.isSuccess}
                  onClick={() => {
                    setNotice('')
                    setEditing('new')
                  }}
                >
                  Nuevo usuario
                </button>
              </div>
              {users.isPending ? (
                <p>Cargando usuarios…</p>
              ) : (
                users.data?.map((u) => (
                  <div className="admin-row" key={u.id}>
                    <div>
                      <strong>{u.userName}</strong>
                      <small>
                        {u.administrator
                          ? 'Administrador'
                          : u.grants
                              .map(
                                (g) =>
                                  (branches.data?.find((b) => b.id === g.branchId)?.name ?? '') +
                                  ' · ' +
                                  roles[g.role],
                              )
                              .join(', ') || 'Sin sucursales'}
                      </small>
                    </div>
                    <div className="actions">
                      {!u.administrator && (
                        <button
                          className="linkbtn"
                          onClick={() => {
                            setNotice('')
                            setEditing(u)
                          }}
                        >
                          Permisos
                        </button>
                      )}
                      <button
                        className="linkbtn"
                        onClick={() => {
                          setNotice('')
                          setReset(u)
                        }}
                      >
                        Restablecer contraseña
                      </button>
                    </div>
                  </div>
                ))
              )}
            </section>
          )}
        </>
      )}
      {tab === 'Sucursales' && (
        <section className="panel">
          <h2>Sucursales</h2>
          {branches.data?.map((b) => (
            <div key={b.id} className="admin-row">
              {b.name}
              <button
                className="linkbtn"
                onClick={() => {
                  setNotice('')
                  setRename(b)
                }}
              >
                Renombrar
              </button>
            </div>
          ))}
          <form
            key={rename?.id ?? 'add'}
            className="fields"
            onSubmit={(e) => {
              e.preventDefault()
              branchSave.mutate(e.currentTarget)
            }}
          >
            <label>
              {rename ? 'Nuevo nombre' : 'Nombre de la sucursal'}
              <input name="name" defaultValue={rename?.name} maxLength={100} required />
            </label>
            {branchSave.error && <p role="alert">{branchSave.error.message}</p>}
            <div className="actions">
              <button disabled={branchSave.isPending}>
                {rename ? 'Guardar nombre' : 'Crear sucursal'}
              </button>
              {rename && (
                <button type="button" className="secondary" onClick={() => setRename(undefined)}>
                  Cancelar
                </button>
              )}
            </div>
          </form>
        </section>
      )}
      {tab === 'Organización' && <OrganizationPage />}
      {tab === 'Auditoría' && (
        <section className="panel">
          <h2>Últimas operaciones</h2>
          {audit.isPending ? (
            <p>Cargando…</p>
          ) : (
            audit.data?.map((a, i) => (
              <div key={i} className="admin-row">
                <div>
                  <strong>
                    {a.actor} · {a.action}
                  </strong>
                  <small>{a.detail}</small>
                </div>
                <time>{new Date(a.time).toLocaleString('es-PE')}</time>
              </div>
            ))
          )}
        </section>
      )}
    </>
  )
}
