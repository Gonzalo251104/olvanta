import { randomUUID } from 'node:crypto'
import { expect, test } from '@playwright/test'
import type { APIRequestContext } from '@playwright/test'

async function change(request: APIRequestContext, method: 'POST' | 'PUT', path: string, data: unknown) {
  const token = await request.get('/api/v1/auth/csrf')
  expect(token.status()).toBe(200)
  return request.fetch('/api/v1/' + path, {
    method,
    data,
    headers: { 'X-CSRF-TOKEN': (await token.json()).token },
  })
}

test('isolates branch masters, enforces roles and audits changes on SQL Server', async ({
  page,
  playwright,
  baseURL,
}) => {
  test.skip(!process.env.OLVANTA_TEST_ADMIN_PASSWORD, 'Requiere credenciales de un entorno de pruebas.')
  test.setTimeout(90000)
  const admin = await playwright.request.newContext({ baseURL })
  const reader = await playwright.request.newContext({ baseURL })
  const suffix = randomUUID().slice(0, 8)
  const userName = 'op-' + suffix
  const password = 'Op1' + randomUUID()
  try {
    expect((await page.request.get('/api/v1/admin/users')).status()).toBe(401)
    expect(
      (
        await change(admin, 'POST', 'auth/login', {
          userName: 'admin',
          password: process.env.OLVANTA_TEST_ADMIN_PASSWORD,
        })
      ).status(),
    ).toBe(204)
    const createBranch = async (name: string) => {
      const response = await change(admin, 'POST', 'admin/branches', { name })
      expect(response.status()).toBe(200)
      return (await response.json()).id as number
    }
    const own = await createBranch('Sur-' + suffix)
    const other = await createBranch('Norte-' + suffix)
    const ownPath = 'branches/' + own + '/masters/customers'
    const otherPath = 'branches/' + other + '/masters/customers'
    const person = {
      name: 'Cliente de prueba',
      code: 'DOC-' + suffix,
      active: true,
      personType: 'person',
      documentType: 'Otro',
    }
    const foreign = await change(admin, 'POST', otherPath, person)
    expect(foreign.status()).toBe(201)
    const foreignId = (await foreign.json()).id
    expect(
      (
        await change(admin, 'POST', 'admin/users', {
          userName,
          password,
          grants: [{ branchId: own, role: 'salesperson' }],
        })
      ).status(),
    ).toBe(204)
    await page.goto('/')
    await page.getByLabel('Usuario', { exact: true }).fill(userName)
    await page.getByLabel('Contraseña', { exact: true }).fill(password)
    await page.getByRole('button', { name: 'Ingresar' }).click()
    await expect(page.getByRole('heading', { name: 'Clientes' })).toBeVisible()
    await expect(page.getByLabel('Sucursal activa')).toHaveValue(String(own))
    await expect(page.getByRole('link', { name: 'Administración' })).toHaveCount(0)
    expect((await page.request.get('/api/v1/' + otherPath)).status()).toBe(403)
    expect((await page.request.get('/api/v1/' + ownPath + '/' + foreignId)).status()).toBe(404)
    expect((await page.request.post('/api/v1/' + ownPath, { data: person })).status()).toBe(400)
    await page.getByRole('button', { name: 'Nuevo registro' }).click()
    await page.getByLabel('Nombre', { exact: true }).fill(person.name)
    await page.getByLabel('Número de documento').fill(person.code)
    await page.getByRole('button', { name: 'Guardar', exact: true }).click()
    await expect(page.getByRole('cell', { name: person.name })).toBeVisible()
    const list = await (
      await page.request.get('/api/v1/' + ownPath + '?search=' + person.code + '&pageSize=1')
    ).json()
    expect(list.total).toBe(1)
    const entry = list.items[0]
    await page.getByRole('button', { name: 'Editar', exact: true }).click()
    await page.getByRole('combobox', { name: 'Estado', exact: true }).selectOption('false')
    await page.getByRole('button', { name: 'Guardar', exact: true }).click()
    await expect(page.getByRole('cell', { name: 'Inactivo', exact: true })).toBeVisible()
    expect((await change(page.request, 'PUT', ownPath + '/' + entry.id, entry.data)).status()).toBe(409)
    await page.getByRole('button', { name: 'Ver detalle' }).click()
    await expect(page.getByRole('heading', { name: 'Historial' })).toBeVisible()
    expect((await (await page.request.get('/api/v1/' + ownPath + '?active=true')).json()).total).toBe(0)
    const invalidBatch = [
      { ...person, code: 'VALID' },
      { ...person, code: '' },
    ]
    expect((await change(page.request, 'POST', ownPath + '/import', invalidBatch)).status()).toBe(400)
    expect((await (await page.request.get('/api/v1/' + ownPath + '?search=VALID')).json()).total).toBe(0)
    const product = {
      name: 'Granel',
      code: 'PROD',
      active: true,
      category: 'Olivas',
      unit: 'kg',
      conversion: 1,
      currency: 'PEN',
      price: 12,
      taxRate: 0,
    }
    expect(
      (await change(page.request, 'POST', 'branches/' + own + '/masters/products', product)).status(),
    ).toBe(201)
    expect(
      (
        await change(page.request, 'POST', 'branches/' + own + '/masters/services', {
          ...product,
          name: 'Entrega',
          code: 'SERV',
        })
      ).status(),
    ).toBe(201)
    expect(
      (
        await change(page.request, 'POST', 'branches/' + own + '/masters/suppliers/import', [person])
      ).status(),
    ).toBe(204)
    const operators = (await (await admin.get('/api/v1/admin/users')).json()) as {
      id: string
      userName: string
      version: string
    }[]
    const operator = operators.find((u) => u.userName === userName)!
    expect(
      (
        await change(admin, 'PUT', 'admin/users/' + operator.id + '/grants', {
          version: operator.version,
          grants: [{ branchId: own, role: 'auditor' }],
        })
      ).status(),
    ).toBe(204)
    expect((await change(page.request, 'POST', ownPath, { ...person, code: 'READONLY' })).status()).toBe(
      403,
    )
    expect((await page.request.get('/api/v1/' + ownPath)).status()).toBe(200)
    const audit = (await (await admin.get('/api/v1/admin/audit')).json()) as {
      action: string
      detail: string
    }[]
    expect(
      audit.some(
        (a) =>
          a.action === 'user.grants-set' && a.detail.includes(userName) && a.detail.includes('auditor'),
      ),
    ).toBeTruthy()
    const newPassword = 'Pw2' + randomUUID()
    expect(
      (
        await change(admin, 'POST', 'admin/users/' + operator.id + '/reset-password', { newPassword })
      ).status(),
    ).toBe(204)
    expect((await page.request.get('/api/v1/auth/session')).status()).toBe(401)
    expect(
      (await change(reader, 'POST', 'auth/login', { userName, password: newPassword })).status(),
    ).toBe(204)
    expect((await change(reader, 'POST', 'auth/logout', {})).status()).toBe(204)
    expect((await reader.get('/api/v1/auth/session')).status()).toBe(401)
  } finally {
    await admin.dispose()
    await reader.dispose()
  }
})

test('administers branch defaults and establishments from the browser', async ({ page }) => {
  test.skip(!process.env.OLVANTA_TEST_ADMIN_PASSWORD, 'Requiere un entorno de pruebas.')
  const suffix = randomUUID().slice(0, 8)
  const branchName = 'Oficina-' + suffix
  await page.goto('/')
  await page.getByLabel('Usuario', { exact: true }).fill('admin')
  await page.getByLabel('Contraseña', { exact: true }).fill(process.env.OLVANTA_TEST_ADMIN_PASSWORD!)
  await page.getByRole('button', { name: 'Ingresar' }).click()
  await page.getByRole('link', { name: 'Administración' }).click()
  await page.getByRole('button', { name: 'Sucursales', exact: true }).click()
  await page.getByLabel('Nombre de la sucursal').fill(branchName)
  await page.getByRole('button', { name: 'Crear sucursal' }).click()
  await expect(page.getByRole('status')).toHaveText('Cambios guardados.')
  await page.getByRole('button', { name: 'Organización', exact: true }).click()
  await page.getByRole('combobox', { name: 'Sucursal a configurar' }).selectOption({ label: branchName })
  await page.getByRole('combobox', { name: 'Moneda' }).selectOption('USD')
  await page.getByLabel('Impuesto (%)').fill('12.5')
  await page.getByRole('button', { name: 'Guardar parámetros' }).click()
  await expect(page.getByText('Configuración guardada.')).toBeVisible()
  await page.getByLabel('Código del establecimiento').fill(suffix)
  await page.getByLabel('Nombre del establecimiento').fill('Local ' + suffix)
  await page.getByLabel('Dirección', { exact: true }).fill('Calle principal')
  await page.getByRole('button', { name: 'Crear establecimiento' }).click()
  await page.getByLabel('Serie', { exact: true }).fill(suffix.toUpperCase())
  await page.getByRole('button', { name: 'Registrar serie' }).click()
  await expect(page.getByText('Factura · ' + suffix.toUpperCase() + ' · Local ' + suffix)).toBeVisible()
  await page.getByRole('combobox', { name: 'Sucursal activa' }).selectOption({ label: branchName })
  await page.getByRole('link', { name: 'Productos' }).click()
  await page.getByRole('button', { name: 'Nuevo registro' }).click()
  await expect(page.getByRole('combobox', { name: 'Moneda' })).toHaveValue('USD')
  await expect(page.getByLabel('Impuesto (%)')).toHaveValue('12.5')
  await page.getByRole('button', { name: 'Cerrar sesión' }).click()
  await expect(page.getByRole('heading', { name: 'Iniciar sesión' })).toBeVisible()
})
