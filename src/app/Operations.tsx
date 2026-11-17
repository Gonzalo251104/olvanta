import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, NavLink, Navigate, Route, Routes, useLocation } from 'react-router'
import { api, currentSession, kinds, roles } from '../shared/workspace'
import type { Kind } from '../shared/workspace'
import { MasterPage } from '../features/masters/MasterPage'
import { AdminPage } from '../features/admin/AdminPage'
import { SystemPage } from '../features/system/SystemPage'

function Brand() {
  return (
    <div className="brand">
      <span aria-hidden="true" className="brand-mark" />
      Olvanta
    </div>
  )
}

function Login({ entered }: { entered: () => void }) {
  const login = useMutation({
    mutationFn: async (form: FormData) => {
      await api.POST('/api/v1/auth/login', {
        body: { userName: String(form.get('user')), password: String(form.get('password')) },
      })
    },
    onSuccess: entered,
  })
  return (
    <>
      <header className="masthead">
        <div className="frame">
          <Brand />
        </div>
      </header>
      <main className="frame login">
        <form
          className="panel fields"
          onSubmit={(e) => {
            e.preventDefault()
            login.mutate(new FormData(e.currentTarget))
          }}
        >
          <h1>Iniciar sesión</h1>
          <label>
            Usuario
            <input name="user" autoComplete="username" required autoFocus />
          </label>
          <label>
            Contraseña
            <input name="password" type="password" autoComplete="current-password" required />
          </label>
          {login.error && <p role="alert">{login.error.message}</p>}
          <button disabled={login.isPending}>Ingresar</button>
          <Link to="/estado">Estado del sistema</Link>
        </form>
      </main>
    </>
  )
}

function PasswordForm() {
  const change = useMutation({
    mutationFn: async (form: HTMLFormElement) => {
      const fields = new FormData(form)
      await api.POST('/api/v1/auth/password', {
        body: { currentPassword: String(fields.get('old')), newPassword: String(fields.get('new')) },
      })
      form.reset()
    },
  })
  return (
    <section className="panel">
      <h1>Mi cuenta</h1>
      <form
        className="fields"
        onSubmit={(e: FormEvent<HTMLFormElement>) => {
          e.preventDefault()
          change.mutate(e.currentTarget)
        }}
      >
        <label>
          Contraseña actual
          <input type="password" autoComplete="current-password" name="old" required />
        </label>
        <label>
          Nueva contraseña
          <input
            type="password"
            autoComplete="new-password"
            name="new"
            minLength={12}
            maxLength={128}
            required
          />
        </label>
        <small>Use al menos 12 caracteres, mayúsculas, minúsculas y números.</small>
        <button disabled={change.isPending}>Cambiar contraseña</button>
        {change.error && <p role="alert">{change.error.message}</p>}
        {change.isSuccess && <p role="status">Contraseña actualizada.</p>}
      </form>
    </section>
  )
}

function Workspace() {
  const cache = useQueryClient()
  const session = useQuery({ queryKey: ['session'], queryFn: currentSession })
  const [selected, setSelected] = useState<number>()
  const location = useLocation()
  useEffect(() => {
    const clear = () => {
      cache.removeQueries({ predicate: (query) => query.queryKey[0] !== 'session' })
      cache.setQueryData(['session'], null)
      setSelected(undefined)
    }
    window.addEventListener('olvanta:signed-out', clear)
    return () => window.removeEventListener('olvanta:signed-out', clear)
  }, [cache])
  const logout = useMutation({
    mutationFn: () => api.POST('/api/v1/auth/logout'),
    onSuccess: () => {
      window.dispatchEvent(new Event('olvanta:signed-out'))
    },
  })
  if (session.isPending)
    return (
      <main className="frame" role="status">
        Cargando sesión…
      </main>
    )
  if (session.error)
    return (
      <main className="frame">
        <p role="alert">No se pudo consultar la sesión.</p>
        <button onClick={() => void session.refetch()}>Reintentar</button>
      </main>
    )
  if (!session.data) return <Login entered={() => void session.refetch()} />
  const user = session.data
  const branch = user.branches.find((b) => b.id === selected) ?? user.branches[0]
  return (
    <>
      <header className="masthead">
        <div className="frame bar">
          <Brand />
          <span>{user.userName}</span>
          <button className="quiet" disabled={logout.isPending} onClick={() => logout.mutate()}>
            Cerrar sesión
          </button>
        </div>
      </header>
      <div className="frame workspace">
        <aside>
          <label>
            Sucursal activa
            <select value={branch?.id ?? ''} onChange={(e) => setSelected(Number(e.target.value))}>
              {user.branches.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
            </select>
          </label>
          {branch && <small>{roles[branch.role]}</small>}
          <nav aria-label="Operaciones">
            {branch &&
              Object.entries(kinds).map(([key, label]) => (
                <NavLink key={key} to={'/maestros/' + key}>
                  {label}
                </NavLink>
              ))}
            {user.administrator && <NavLink to="/administracion">Administración</NavLink>}
            <NavLink to="/cuenta">Mi cuenta</NavLink>
            <Link to="/estado">Estado del sistema</Link>
          </nav>
        </aside>
        <main className="content">
          {logout.error && <p role="alert">{logout.error.message}</p>}
          <Routes>
            <Route path="/cuenta" element={<PasswordForm />} />
            {user.administrator && <Route path="/administracion" element={<AdminPage />} />}
            {branch &&
              (Object.keys(kinds) as Kind[]).map((kind) => (
                <Route
                  key={kind}
                  path={'/maestros/' + kind}
                  element={<MasterPage key={branch.id + kind} branch={branch} kind={kind} />}
                />
              ))}
            <Route
              path="*"
              element={
                branch ? (
                  <Navigate to="/maestros/customers" replace state={{ from: location.pathname }} />
                ) : (
                  <p>Su cuenta aún no tiene una sucursal asignada.</p>
                )
              }
            />
          </Routes>
        </main>
      </div>
    </>
  )
}

export function Operations() {
  return (
    <Routes>
      <Route
        path="/estado"
        element={
          <>
            <SystemPage />
            <div className="frame">
              <Link to="/">Volver a Olvanta</Link>
            </div>
          </>
        }
      />
      <Route path="*" element={<Workspace />} />
    </Routes>
  )
}
