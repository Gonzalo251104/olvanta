import { randomUUID } from 'node:crypto'
import { expect, test } from '@playwright/test'
import { change } from './change'
import type { Models } from '../../src/shared/workspace'

test('alquila un local, concilia cuotas y garantías y conserva la renovación', async ({
  page,
  playwright,
  baseURL,
}) => {
  test.skip(!process.env.OLVANTA_TEST_ADMIN_PASSWORD, 'Requiere SQL de pruebas.')
  test.setTimeout(120000)
  const req = page.request
  const suffix = randomUUID().slice(0, 8)
  async function post(path: string, data: unknown) {
    const res = await change(req, 'POST', path, data)
    expect(res.ok(), path + ' ' + (await res.text())).toBe(true)
    return res.status() === 204 ? null : res.json()
  }
  await post('auth/login', { userName: 'admin', password: process.env.OLVANTA_TEST_ADMIN_PASSWORD })
  const branch = await post('admin/branches', { name: 'Locales-' + suffix })
  const base = 'branches/' + branch.id + '/'
  const rent = base + 'rentals'
  const desk = async (): Promise<Models['RentalDeskView']> => (await req.get('/api/v1/' + rent)).json()
  const customer = await post(base + 'masters/customers', {
    code: '20000000001',
    name: 'Arrendatario de prueba',
    active: true,
    personType: 'company',
    documentType: 'RUC',
  })
  const service = await post(base + 'masters/services', {
    code: 'ALQ',
    name: 'Alquiler',
    category: 'Servicio',
    unit: 'servicio',
    active: true,
    currency: 'PEN',
    price: 310,
    taxRate: 18,
    conversion: 1,
  })
  await page.goto('/alquileres')
  await page.getByLabel('Sucursal activa').selectOption(String(branch.id))
  await page.getByRole('button', { name: 'Registrar local' }).click()
  await page.getByLabel('Código del local').fill('L-01')
  await page.getByLabel('Nombre del local').fill('Galería')
  await page.getByLabel('Dirección', { exact: true }).fill('Pasaje de prueba 12')
  await page.getByLabel('Características').fill('Primer piso, 40 m²')
  await page.getByRole('button', { name: 'Guardar local' }).click()
  await expect(page.getByRole('heading', { name: 'L-01 · Galería' })).toBeVisible()
  const unit = (await desk()).units[0]
  const terms = {
    unitId: unit.id,
    customerId: customer.id,
    serviceId: service.id,
    start: '2028-01-16',
    end: '2028-03-31',
    monthlyRent: 310,
    deposit: 100,
    dueDay: 31,
    conditions: 'Alquiler mensual de la galería.',
  }
  const conflicts = await Promise.all([1, 2].map(() => change(req, 'POST', rent + '/contracts', terms)))
  expect(conflicts.map((r) => r.status()).sort()).toEqual([200, 409])
  const lease = Number((await desk()).contracts[0].id)
  expect(
    (
      await change(req, 'PUT', 'admin/organization/branches/' + branch.id + '/defaults', {
        currency: 'USD',
        taxRate: 18,
      })
    ).status(),
  ).toBe(409)
  const path = rent + '/contracts/' + lease
  const months = await Promise.all([
    post(path + '/months', { month: '2028-01-01' }),
    post(path + '/months', { month: '2028-01-20' }),
  ])
  expect(months[0]).toBe(months[1])
  expect((await desk()).charges).toHaveLength(1)
  const charge = (await desk()).charges[0]
  expect(charge).toMatchObject({ amount: 160, balance: 160, due: '2028-01-31' })
  const extra = await post(path + '/extras', {
    amount: 20,
    reference: 'ACTA1',
    reason: 'Limpieza acordada',
    due: '2028-01-30',
  })
  expect(
    await post(path + '/extras', {
      amount: 20,
      reference: 'ACTA1',
      reason: 'Limpieza acordada',
      due: '2028-01-30',
    }),
  ).toBe(extra)
  expect(
    (
      await change(req, 'POST', base + 'sales/actions', {
        requestId: randomUUID(),
        saleId: charge.saleId,
        kind: 'cancel',
        note: 'Prueba',
      })
    ).status(),
  ).toBe(409)
  const act = (kind: string, fields: Record<string, unknown>) =>
    post(base + 'sales/actions', { kind, requestId: randomUUID(), ...fields })
  const session = (await act('open', { amount: 50 })).cashSessionId
  const deposit = {
    requestId: randomUUID(),
    sessionId: session,
    method: 'cash',
    kind: 'deposit-in',
    amount: 100,
    note: 'Entrega de llaves',
  }
  await Promise.all([post(path + '/deposits', deposit), post(path + '/deposits', deposit)])
  expect((await desk()).contracts[0].held).toBe(100)
  expect((await desk()).charges.find((c) => c.id === charge.id)?.balance).toBe(160)
  await page.getByRole('button', { name: 'Actualizar' }).click()
  const row = page.getByRole('row').filter({ hasText: 'Renta 2028-01' })
  await row.getByRole('button', { name: 'Cobrar' }).click()
  await page.getByLabel('Importe del movimiento').fill('60')
  await page.getByRole('button', { name: 'Registrar cobro', exact: true }).click()
  await expect(row).toContainText('100.00')
  expect((await desk()).charges.find((c) => c.id === charge.id)?.balance).toBe(100)
  await row.getByRole('link', { name: 'Facturación' }).click()
  await expect(page.getByRole('combobox', { name: 'Venta', exact: true })).toHaveValue(
    String(charge.saleId),
  )
  // Verify the common issuer can sign the rent account in the isolated simulation.
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
    name: 'Galería fiscal',
    code: String(5000 + Number(branch.id)),
    address: 'Pasaje 12',
  })
  const org: Models['OrganizationView'] = await (await req.get('/api/v1/admin/organization')).json()
  const site = org.establishments.find((s) => s.branchId === branch.id)!
  const seriesCode = 'FR' + Number(site.id).toString(36).padStart(2, '0').toUpperCase()
  await post('admin/organization/series', {
    establishmentId: site.id,
    documentType: 'invoice',
    code: seriesCode,
  })
  const billing: Models['BillingDesk'] = await (await req.get('/api/v1/' + base + 'billing')).json()
  expect(billing.mode).toBe('simulation')
  const doc = await post(base + 'billing', {
    kind: 'invoice',
    requestId: randomUUID(),
    saleId: charge.saleId,
    seriesId: billing.series[0].id,
  })
  const bill: Models['BillDetail'] = await (
    await req.get('/api/v1/' + base + 'billing/' + doc)
  ).json()
  expect(bill.data).toMatchObject({ total: 160, outstanding: 100 })
  await post(path + '/deposits', {
    ...deposit,
    requestId: randomUUID(),
    kind: 'deposit-out',
    amount: 40,
  })
  await act('pay', { saleId: charge.saleId, cashSessionId: session, method: 'transfer', amount: 100 })
  expect((await desk()).charges.find((c) => c.id === charge.id)?.balance).toBe(0)
  await act('close', { cashSessionId: session, amount: 170 })
  const cash: Models['CashDetail'][] = await (await req.get('/api/v1/' + base + 'sales/cash')).json()
  expect(cash[0]).toMatchObject({ expected: 170, counted: 170, difference: 0 })
  expect(
    (
      await change(req, 'POST', path + '/deposits', {
        ...deposit,
        requestId: randomUUID(),
        kind: 'deposit-out',
        amount: 1,
      })
    ).status(),
  ).toBe(409)
  expect(
    (
      await change(req, 'POST', path + '/end', { date: '2028-01-25', reason: 'Entrega anticipada' })
    ).status(),
  ).toBe(409)
  await post(path + '/end', { date: '2028-02-10', reason: 'Entrega anticipada' })
  await post(path + '/months', { month: '2028-02-01' })
  expect((await desk()).charges.find((c) => c.reference === '2028-02')).toMatchObject({
    amount: 106.9,
    due: '2028-02-10',
  })
  expect((await change(req, 'POST', path + '/months', { month: '2028-03-01' })).status()).toBe(409)
  await page.getByRole('link', { name: 'Alquileres', exact: true }).click()
  await page.getByRole('button', { name: 'Actualizar' }).click()
  await expect(page.getByText(/Finalización 2028-02-10/)).toBeVisible()
  await page.getByRole('button', { name: 'Renovar', exact: true }).click()
  await expect(page.getByLabel('Inicio de vigencia')).toHaveValue('2028-02-11')
  await page.getByLabel('Fin pactado').fill('2028-07-31')
  await page.getByLabel('Renta mensual con impuestos').fill('350')
  await page.getByRole('button', { name: 'Guardar contrato' }).click()
  await expect(page.getByText('Renovación del contrato #' + lease)).toBeVisible()
  await page.setViewportSize({ width: 390, height: 844 })
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390)
  const final = await desk()
  expect(final.contracts.find((c) => c.id === lease)).toMatchObject({
    monthlyRent: 310,
    held: 60,
    endedOn: '2028-02-10',
  })
  expect(final.contracts.find((c) => c.previousId === lease)).toMatchObject({
    monthlyRent: 350,
    held: 0,
  })
  for (const [day, occupied] of [
    ['2028-02-10', true],
    ['2028-08-01', false],
  ] as const) {
    const result: Models['RentalDeskView'] = await (
      await req.get('/api/v1/' + rent + '?date=' + day)
    ).json()
    expect(result.units[0].occupied).toBe(occupied)
  }
  const other = await post('admin/branches', { name: 'Sin locales-' + suffix })
  expect(
    (
      await change(req, 'POST', 'branches/' + other.id + '/rentals/contracts/' + lease + '/months', {
        month: '2028-01-01',
      })
    ).status(),
  ).toBe(404)
  const manager = await playwright.request.newContext({ baseURL })
  try {
    const userName = 'alq-' + suffix,
      password = 'Al1' + randomUUID()
    await post('admin/users', { userName, password, grants: [{ branchId: branch.id, role: 'rentals' }] })
    expect((await change(manager, 'POST', 'auth/login', { userName, password })).status()).toBe(204)
    expect((await manager.get('/api/v1/' + rent)).status()).toBe(200)
    expect((await manager.get('/api/v1/branches/' + other.id + '/rentals')).status()).toBe(403)
    expect(
      (
        await change(manager, 'POST', path + '/extras', {
          amount: 20,
          reference: 'X',
          reason: 'Prueba',
          due: '2028-01-30',
        })
      ).status(),
    ).toBe(403)
    expect((await change(manager, 'POST', path + '/deposits', deposit)).status()).toBe(403)
  } finally {
    await manager.dispose()
  }
})
