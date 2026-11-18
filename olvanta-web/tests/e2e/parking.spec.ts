import { randomUUID } from 'node:crypto'
import { expect, test } from '@playwright/test'
import { change } from './change'
import type { Models } from '../../src/shared/workspace'

test('opera zonas, cobra una salida y concilia un abono de estacionamiento', async ({
  page,
  playwright,
  baseURL,
}) => {
  test.skip(!process.env.OLVANTA_TEST_ADMIN_PASSWORD, 'Requiere SQL de pruebas.')
  test.setTimeout(120000)
  const req = page.request
  const suffix = randomUUID().slice(0, 8)
  async function post(path: string, data: unknown) {
    const response = await change(req, 'POST', path, data)
    expect(response.ok(), path + ' ' + (await response.text())).toBe(true)
    return response.status() === 204 ? null : response.json()
  }
  await post('auth/login', { userName: 'admin', password: process.env.OLVANTA_TEST_ADMIN_PASSWORD })
  const branch = await post('admin/branches', { name: 'Plazas-' + suffix })
  const base = 'branches/' + branch.id + '/'
  const parking = base + 'parking'
  const service = await post(base + 'masters/services', {
    name: 'Estacionamiento',
    code: 'EST',
    active: true,
    category: 'Servicio',
    unit: 'servicio',
    price: 6,
    currency: 'PEN',
    taxRate: 18,
    conversion: 1,
  })
  const customer = await post(base + 'masters/customers', {
    name: 'Cliente de prueba',
    code: '20000000001',
    active: true,
    personType: 'company',
    documentType: 'RUC',
  })
  const garage = await post(parking + '/sites', { name: 'Playa Central' })
  const path = parking + '/sites/' + garage
  const near = await post(path + '/zones', { name: 'Acceso', capacity: 1 })
  const far = await post(path + '/zones', { name: 'Patio', capacity: 1 })
  const rate = { serviceId: service.id, blockMinutes: 60, graceMinutes: 0, blockPrice: 6 }
  await post(path + '/rates', rate)
  const desk = async (): Promise<Models['ParkingDeskView']> =>
    (await req.get('/api/v1/' + parking)).json()
  const attempt = (plate: string, zoneId: number) =>
    change(req, 'POST', path + '/entries', { requestId: randomUUID(), plate, zoneId })
  const entries = await Promise.all([attempt(' ab-123 ', near), attempt('XY-456', near)])
  expect(entries.map((r) => r.status()).sort()).toEqual([200, 409])
  const first = (await desk()).stays[0]
  expect(first.plate).toMatch(/^(AB123|XY456)$/)
  expect(
    (await attempt(first.plate.toLowerCase().slice(0, 2) + '-' + first.plate.slice(2), far)).status(),
  ).toBe(409)
  await post(path + '/rates', { ...rate, blockPrice: 9 })
  const cashAction = (kind: string, fields: Record<string, unknown>) =>
    post(base + 'sales/actions', { kind, requestId: randomUUID(), ...fields })
  const session = (await cashAction('open', { amount: 20 })).cashSessionId
  const record = path + '/stays/' + first.id
  const exit = {
    requestId: randomUUID(),
    expectedAmount: 6,
    sessionId: session,
    method: 'cash',
    customerId: customer.id,
  }
  expect((await change(req, 'POST', record + '/exit', { ...exit, expectedAmount: 9 })).status()).toBe(
    409,
  )
  expect((await change(req, 'POST', record + '/exit', { ...exit, sessionId: 0 })).status()).toBe(409)
  expect((await desk()).stays.find((s) => s.id === first.id)?.exited).toBeNull()
  const [a, b] = await Promise.all([post(record + '/exit', exit), post(record + '/exit', exit)])
  expect(a).toBe(b)
  expect(
    (await change(req, 'POST', record + '/exit', { ...exit, requestId: randomUUID() })).status(),
  ).toBe(409)
  const closed = (await desk()).stays.find((s) => s.id === first.id)!
  expect(closed).toMatchObject({ blocks: 1, amount: 6 })
  expect(
    (
      await change(req, 'PUT', 'admin/organization/company', {
        name: 'Emisor de simulación',
        taxId: '20000000001',
      })
    ).status(),
  ).toBe(204)
  await post('admin/organization/establishments', {
    branchId: branch.id,
    name: 'Playa fiscal',
    code: String(7000 + Number(branch.id)),
    address: 'Pasaje de prueba 7',
  })
  const org: Models['OrganizationView'] = await (await req.get('/api/v1/admin/organization')).json()
  const establishment = org.establishments.find((s) => s.branchId === branch.id)!
  await post('admin/organization/series', {
    establishmentId: establishment.id,
    documentType: 'invoice',
    code: 'FP' + Number(establishment.id).toString(36).padStart(2, '0').toUpperCase(),
  })
  const billing: Models['BillingDesk'] = await (await req.get('/api/v1/' + base + 'billing')).json()
  expect(billing.sales.find((s) => s.id === closed.saleId)).toMatchObject({ total: 6 })
  expect(billing.mode).toBe('simulation')
  const invoice = await post(base + 'billing', {
    kind: 'invoice',
    requestId: randomUUID(),
    saleId: closed.saleId,
    seriesId: billing.series[0].id,
  })
  const bill: Models['BillDetail'] = await (
    await req.get('/api/v1/' + base + 'billing/' + invoice)
  ).json()
  expect(bill.data).toMatchObject({ total: 6, tax: 0.92, outstanding: 0 })
  const today = (await desk()).today
  const passForm = {
    requestId: randomUUID(),
    plate: 'PQR-321',
    from: today,
    through: '2099-01-01',
    price: 30,
    method: 'transfer',
    sessionId: session,
  }
  const [pass, replayed] = await Promise.all([
    post(path + '/passes', passForm),
    post(path + '/passes', passForm),
  ])
  expect(pass).toBe(replayed)
  expect(
    (await change(req, 'POST', path + '/passes', { ...passForm, requestId: randomUUID() })).status(),
  ).toBe(409)
  await page.goto('/estacionamientos')
  await page.getByLabel('Sucursal activa').selectOption(String(branch.id))
  await expect(page.getByText('0 / 1 plazas').first()).toBeVisible()
  await page.getByLabel('Placa del vehículo').fill('pqr-321')
  await page.getByLabel('Zona de ingreso').selectOption(String(far))
  await page.getByRole('button', { name: 'Emitir ticket' }).click()
  await expect(page.getByRole('heading', { name: /Ticket de ingreso/ })).toBeVisible()
  await expect(page.getByText('Abono vinculado #' + pass)).toBeVisible()
  await page.getByRole('button', { name: 'Registrar salida sin cargo' }).click()
  await expect(page.getByRole('heading', { name: /Comprobante de salida/ })).toBeVisible()
  expect((await desk()).stays.find((s) => s.passId === pass)).toMatchObject({
    blocks: 0,
    amount: 0,
    paymentId: null,
  })
  await page.setViewportSize({ width: 390, height: 844 })
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390)
  await page.emulateMedia({ media: 'print' })
  await expect(page.getByRole('navigation', { name: 'Operaciones' })).toBeHidden()
  await expect(page.getByRole('heading', { name: /Comprobante de salida/ })).toBeVisible()
  await page.emulateMedia({ media: 'screen' })
  await page.getByRole('button', { name: 'Salidas', exact: true }).click()
  await page.getByLabel('Placa en el historial').fill(first.plate.toLowerCase())
  await page.getByRole('button', { name: 'Buscar salidas' }).click()
  await expect(page.getByRole('button', { name: '#' + first.id + ' · ' + first.plate })).toBeVisible()
  await expect(page.getByRole('button', { name: /· PQR321$/ })).toHaveCount(0)
  await cashAction('close', { cashSessionId: session, amount: 26 })
  const cash: Models['CashDetail'][] = await (await req.get('/api/v1/' + base + 'sales/cash')).json()
  expect(cash[0]).toMatchObject({ expected: 26, counted: 26, difference: 0 })
  expect(cash[0].history.filter((e) => e.kind === 'pay' || e.kind === 'parking-pay')).toHaveLength(2)
  const other = await post('admin/branches', { name: 'Otra playa-' + suffix })
  expect((await req.get('/api/v1/branches/' + other.id + '/parking?siteId=' + garage)).status()).toBe(
    404,
  )
  const operator = await playwright.request.newContext({ baseURL })
  try {
    const userName = 'est-' + suffix,
      password = 'Es1' + randomUUID()
    await post('admin/users', { userName, password, grants: [{ branchId: branch.id, role: 'parking' }] })
    expect((await change(operator, 'POST', 'auth/login', { userName, password })).status()).toBe(204)
    expect((await operator.get('/api/v1/' + parking)).status()).toBe(200)
    expect((await change(operator, 'POST', path + '/rates', rate)).status()).toBe(403)
    expect((await operator.get('/api/v1/branches/' + other.id + '/parking')).status()).toBe(403)
  } finally {
    await operator.dispose()
  }
})
