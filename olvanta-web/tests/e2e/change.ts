import { expect, type APIRequestContext } from '@playwright/test'

export async function change(
  request: APIRequestContext,
  method: 'POST' | 'PUT',
  path: string,
  data: unknown,
) {
  const token = await request.get('/api/v1/auth/csrf')
  expect(token.status()).toBe(200)
  return request.fetch('/api/v1/' + path, {
    method,
    data,
    headers: { 'X-CSRF-TOKEN': (await token.json()).token },
  })
}
