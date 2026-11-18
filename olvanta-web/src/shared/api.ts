import createClient from 'openapi-fetch'
import type { paths } from '../generated/api'

const client = createClient<paths>({ baseUrl: '/' })

export async function checkSystem(signal: AbortSignal) {
  const { data, error, response } = await client.GET('/api/v1/system/status', {
    signal,
    cache: 'no-store',
  })

  if (response.status === 200 && data?.status === 'ready' && data.database === 'connected') {
    return { databaseAvailable: true }
  }
  if (response.status === 503 && error?.status === 'degraded' && error.database === 'unavailable') {
    return { databaseAvailable: false }
  }
  throw new Error('La API no devolvió un estado válido.')
}
