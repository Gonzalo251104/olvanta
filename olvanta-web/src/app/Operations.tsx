import { lazy, Suspense, useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, NavLink, Navigate, Route, Routes, useLocation } from 'react-router'
import { api, currentSession, kinds, roles } from '../shared/workspace'
import type { Kind } from '../shared/workspace'
import { MasterPage } from '../features/masters/MasterPage'
import { InventoryPage } from '../features/inventory/InventoryPage'
import { SalesPage } from '../features/sales/SalesPage'
import { BillingPage } from '../features/billing/BillingPage'
import { AdminPage } from '../features/admin/AdminPage'
import { SystemPage } from '../features/system/SystemPage'
import { Brand } from './Brand'

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
    <main className="login">
      <div className="login-cover">
        <img src="/seleccion-aceitunas.webp" alt="" fetchPriority="high" />
        <div className="cover-brand">
          <Brand />
          <p>Gestión comercial y operativa</p>
        </div>
      </div>
      <div className="login-body">
        <form
          className="login-form fields"
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
          <button disabled={login.isPending}>{login.isPending ? 'Ingresando…' : 'Ingresar'}</button>
        </form>
        <Link className="login-status" to="/estado">
          Estado del sistema <span aria-hidden="true">↗</span>
        </Link>
      </div>
    </main>
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
    <section className="panel account-panel">
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

const RentalPage = lazy(() => import('../features/rentals/RentalPage'))
const ParkingPage = lazy(() => import('../features/parking/ParkingPage'))
const OlivePage = lazy(() => import('../features/olives/OlivePage'))

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
  const branch = user.branches.find((b) => Number(b.id) === selected) ?? user.branches[0]
  return (
    <>
      <header className="masthead">
        <div className="frame bar">
          <Link to="/" className="brand-link">
            <Brand />
          </Link>
          {branch && (
            <label className="branch-control">
              <span>Sucursal activa</span>
              <select value={branch.id} onChange={(e) => setSelected(Number(e.target.value))}>
                {user.branches.map((b) => (
                  <option key={b.id} value={b.id}>
                    {b.name}
                  </option>
                ))}
              </select>
            </label>
          )}
          <div className="account-tools">
            <NavLink className="account-link" to="/cuenta">
              <strong>{user.userName}</strong>
              <small>{branch ? 'Mi cuenta · ' + roles[branch.role] : 'Mi cuenta'}</small>
            </NavLink>
            <button className="quiet" disabled={logout.isPending} onClick={() => logout.mutate()}>
              Cerrar sesión
            </button>
          </div>
        </div>
      </header>
      <nav className="work-nav" aria-label="Operaciones">
        <div className="frame nav-groups">
          {branch && (
            <>
              <MasterLinks label="Directorio" entries={['customers', 'suppliers']} />
              <MasterLinks label="Catálogo" entries={['products', 'services']} />
              {branch.permissions.includes('parking.read') && (
                <div className="nav-group">
                  <span>Movilidad</span>
                  <NavLink to="/estacionamientos">Estacionamientos</NavLink>
                </div>
              )}
              {branch.permissions.includes('rentals.read') && (
                <div className="nav-group">
                  <span>Inmuebles</span>
                  <NavLink to="/alquileres">Alquileres</NavLink>
                </div>
              )}
              {branch.permissions.includes('billing.read') && (
                <div className="nav-group">
                  <span>Emisión</span>
                  <NavLink to="/facturacion">Facturación</NavLink>
                </div>
              )}
              {branch.permissions.includes('sales.read') && (
                <div className="nav-group">
                  <span>Comercial</span>
                  <NavLink to="/ventas">Ventas y caja</NavLink>
                </div>
              )}
              {branch.permissions.includes('inventory.read') && (
                <div className="nav-group">
                  <span>Abastecimiento</span>
                  <NavLink to="/inventario">Compras e inventario</NavLink>
                  <NavLink to="/aceitunas">Aceitunas</NavLink>
                </div>
              )}
            </>
          )}
          {user.administrator && (
            <div className="nav-group">
              <span>Gestión</span>
              <NavLink to="/administracion">Administración</NavLink>
            </div>
          )}
          <Link className="system-link" to="/estado">
            Estado del sistema <span aria-hidden="true">↗</span>
          </Link>
        </div>
      </nav>
      <main className="frame content">
        {logout.error && <p role="alert">{logout.error.message}</p>}
        <Routes>
          <Route path="/cuenta" element={<PasswordForm />} />
          {branch?.permissions.includes('inventory.read') && (
            <Route
              path="/aceitunas"
              element={
                <Suspense fallback={<p>Cargando lotes de aceitunas…</p>}>
                  <OlivePage key={branch.id} branch={branch} />
                </Suspense>
              }
            />
          )}
          {branch?.permissions.includes('parking.read') && (
            <Route
              path="/estacionamientos"
              element={
                <Suspense fallback={<p>Cargando estacionamientos…</p>}>
                  <ParkingPage key={branch.id} branch={branch} />
                </Suspense>
              }
            />
          )}
          {branch?.permissions.includes('rentals.read') && (
            <Route
              path="/alquileres"
              element={
                <Suspense fallback={<p>Cargando alquileres…</p>}>
                  <RentalPage key={branch.id} branch={branch} />
                </Suspense>
              }
            />
          )}
          {branch?.permissions.includes('billing.read') && (
            <Route path="/facturacion" element={<BillingPage key={branch.id} branch={branch} />} />
          )}
          {branch?.permissions.includes('sales.read') && (
            <Route path="/ventas" element={<SalesPage key={branch.id} branch={branch} />} />
          )}
          {branch?.permissions.includes('inventory.read') && (
            <Route path="/inventario" element={<InventoryPage key={branch.id} branch={branch} />} />
          )}
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
    </>
  )
}

function MasterLinks({ label, entries }: { label: string; entries: Kind[] }) {
  return (
    <div className="nav-group">
      <span>{label}</span>
      {entries.map((entry) => (
        <NavLink key={entry} to={'/maestros/' + entry}>
          {kinds[entry]}
        </NavLink>
      ))}
    </div>
  )
}

export function Operations() {
  return (
    <Routes>
      <Route
        path="/estado"
        element={
          <>
            <header className="masthead">
              <div className="frame bar">
                <Link to="/" className="brand-link" aria-label="Volver a Olvanta">
                  <Brand />
                </Link>
              </div>
            </header>
            <SystemPage />
          </>
        }
      />
      <Route path="*" element={<Workspace />} />
    </Routes>
  )
}
