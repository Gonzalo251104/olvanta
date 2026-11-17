import createClient from 'openapi-fetch'
import type { paths, components } from '../generated/api'

export type Models = components['schemas']
export type Kind = 'customers' | 'suppliers' | 'products' | 'services'
export const kinds: Record<Kind, string> = {
  customers: 'Clientes',
  suppliers: 'Proveedores',
  products: 'Productos',
  services: 'Servicios',
}
export const roles: Record<string, string> = {
  administrator: 'Administrador',
  supervisor: 'Supervisor',
  salesperson: 'Vendedor',
  warehouse: 'Almacenero',
  cashier: 'Cajero',
  rentals: 'Encargado de alquileres',
  parking: 'Operador de estacionamiento',
  billing: 'Responsable de facturación',
  auditor: 'Auditor',
}

export const api = createClient<paths>({ baseUrl: '/', cache: 'no-store' })
api.use({
  async onRequest({ request }) {
    if (request.method === 'GET') return
    const { data } = await api.GET('/api/v1/auth/csrf')
    if (!data?.token) throw new Error('No se pudo verificar la sesión. Recargue la página.')
    request.headers.set('X-CSRF-TOKEN', data.token)
    return request
  },
  async onResponse({ response, schemaPath }) {
    if (response.ok || (schemaPath === '/api/v1/auth/session' && response.status === 401)) return
    if (response.status === 401 && schemaPath !== '/api/v1/auth/login')
      window.dispatchEvent(new Event('olvanta:signed-out'))
    const body = await response
      .clone()
      .json()
      .catch(() => null)
    const fallback: Record<number, string> = {
      400: 'Revise los campos e inténtelo de nuevo.',
      401: 'Usuario o contraseña incorrectos, o cuenta bloqueada.',
      403: 'Su cuenta no tiene permiso para esta operación.',
      404: 'El registro ya no está disponible.',
      409: 'El registro cambió o el identificador ya existe. Actualice la lista.',
    }
    throw new Error(
      body?.detail || fallback[response.status] || 'El servicio no pudo completar la operación.',
    )
  },
})
export async function currentSession() {
  const result = await api.GET('/api/v1/auth/session')
  return result.data ?? null
}
export const collection = '/api/v1/branches/{branchId}/masters/{kind}'
export const item = '/api/v1/branches/{branchId}/masters/{kind}/{id}'
