import { randomUUID, createHash } from 'node:crypto'
import { writeFile } from 'node:fs/promises'
import { expect, test } from '@playwright/test'
import type { Models } from '../../src/shared/workspace'
import { change } from './change'

test('reconcilia la jornada integrada y exporta los reportes de la sucursal', async ({
  page,
  playwright,
  baseURL,
}, info) => {
  test.skip(!process.env.OLVANTA_TEST_ADMIN_PASSWORD, 'Requiere una base desechable.')
  test.setTimeout(120000)
  const request = page.request
  const suffix = randomUUID().slice(0, 8)
  const post = async (path: string, data: unknown) => {
    const response = await change(request, 'POST', path, data)
    expect(response.ok(), path + ' ' + (await response.text())).toBe(true)
    return response.status() === 204 ? null : response.json()
  }
  const get = async <T>(path: string): Promise<T> => {
    const res = await request.get('/api/v1/' + path)
    expect(res.ok(), path + ' ' + (await res.text())).toBe(true)
    return res.json()
  }
  await post('auth/login', { userName: 'admin', password: process.env.OLVANTA_TEST_ADMIN_PASSWORD })
  const branch = await post('admin/branches', { name: 'Demostración integrada-' + suffix })
  const base = 'branches/' + branch.id + '/'
  const { today } = await get<Models['ParkingDeskView']>(base + 'parking')
  const month = today.slice(0, 7) + '-01'
  const next = new Date(month + 'T00:00:00Z')
  next.setUTCMonth(next.getUTCMonth() + 1)
  next.setUTCDate(0)
  const monthEnd = next.toISOString().slice(0, 10)
  const ruc = '20000000001'
  const supplier = await post(base + 'masters/suppliers', {
    name: 'Productor del valle',
    code: 'P-01',
    active: true,
    personType: 'company',
    documentType: 'Otro',
  })
  const customer = await post(base + 'masters/customers', {
    name: 'Cliente demostración',
    code: ruc,
    active: true,
    personType: 'company',
    documentType: 'RUC',
  })
  const common = { active: true, currency: 'PEN', taxRate: 18, category: 'Demostración', conversion: 1 }
  const olives = await post(base + 'masters/products', {
    ...common,
    name: 'Aceituna a granel',
    code: 'ACE',
    unit: 'kg',
    presentation: 'Granel',
    price: 11.8,
  })
  const service = await post(base + 'masters/services', {
    ...common,
    name: 'Servicio local',
    code: 'LOC',
    unit: 'servicio',
    price: 100,
  })
  await post(base + 'inventory/warehouses', { name: 'Recepción' })
  await post(base + 'inventory/purchases', {
    supplierId: supplier.id,
    reference: 'OC-INTEG',
    lines: [{ productId: olives.id, quantity: 20, unitCost: 8 }],
  })
  const inventory = await get<Models['InventoryView']>(base + 'inventory')
  const warehouseId = inventory.warehouses[0].id
  await post(base + 'inventory/movements', {
    kind: 'receipt',
    operationId: randomUUID(),
    warehouseId,
    productId: olives.id,
    purchaseLineId: inventory.purchases[0].lines[0].id,
    quantity: 20,
    lot: 'ACE-INTEG',
    reason: 'Recepción pesada',
    olive: {
      gross: 22,
      tare: 2,
      variety: 'Sevillana',
      grade: 'Extra',
      origin: 'Tacna',
      supplierLot: 'T-42',
    },
  })
  const act = (kind: string, fields: Record<string, unknown>) =>
    post(base + 'sales/actions', { kind, requestId: randomUUID(), ...fields })
  const { cashSessionId } = await act('open', { amount: 100 })
  const { saleId } = await post(base + 'sales/quotes', {
    requestId: randomUUID(),
    customerId: customer.id,
    terms: 'cash',
    lines: [{ itemId: olives.id, warehouseId, lot: 'ACE-INTEG', quantity: 2, discount: 0 }],
  })
  await act('order', { saleId })
  await act('confirm', { saleId })
  await act('pay', { saleId, cashSessionId, amount: 23.6, method: 'cash' })
  await act('dispatch', { saleId })

  const company = await change(request, 'PUT', 'admin/organization/company', {
    name: 'Emisor de simulación',
    taxId: ruc,
  })
  expect(company.status()).toBe(204)
  await post('admin/organization/establishments', {
    branchId: branch.id,
    name: 'Tienda integrada',
    code: String(8000 + Number(branch.id)),
    address: 'Ejemplo 123',
  })
  const org = await get<Models['OrganizationView']>('admin/organization')
  const siteId = org.establishments.find((s) => s.branchId === branch.id)!.id
  await post('admin/organization/series', {
    establishmentId: siteId,
    documentType: 'invoice',
    code: 'FI' + Number(siteId).toString(36).padStart(2, '0').toUpperCase(),
  })
  const billing = base + 'billing'
  const { mode, series } = await get<Models['BillingDesk']>(billing)
  expect(mode).toBe('simulation')
  const prepare = (target: number | string) =>
    post(billing, { requestId: randomUUID(), kind: 'invoice', saleId: target, seriesId: series[0].id })
  const accepted = await prepare(saleId)
  await post(billing + '/' + accepted + '/submission', { action: 'send' })
  const bill = await get<Models['BillDetail']>(billing + '/' + accepted)
  expect(bill).toMatchObject({
    document: { status: 'accepted', hasCdr: true },
    data: { total: 23.6, tax: 3.6 },
  })
  const cdrPath = billing + '/' + accepted + '/files/cdr'
  const cdr = await request.get('/api/v1/' + cdrPath)
  expect(cdr.ok()).toBe(true)
  const cdrHash = createHash('sha256')
    .update(await cdr.body())
    .digest('hex')

  const unit = await post(base + 'rentals/units', {
    code: 'L01',
    name: 'Galería',
    address: 'Pasaje 1',
    features: 'Primer piso',
  })
  const lease = await post(base + 'rentals/contracts', {
    unitId: unit,
    customerId: customer.id,
    serviceId: service.id,
    start: month,
    end: monthEnd,
    monthlyRent: 100,
    deposit: 0,
    dueDay: 1,
    conditions: 'Alquiler mensual',
  })
  await post(base + 'rentals/contracts/' + lease + '/months', { month })
  const rent = (await get<Models['RentalDeskView']>(base + 'rentals')).charges[0]
  await act('pay', { saleId: rent.saleId, cashSessionId, amount: 40, method: 'cash' })
  const rejected = await prepare(rent.saleId)
  await post(billing + '/' + rejected + '/submission', { action: 'send', scenario: 'reject' })

  const garage = await post(base + 'parking/sites', { name: 'Playa central' })
  const parking = base + 'parking/sites/' + garage
  const zoneId = await post(parking + '/zones', { name: 'Acceso', capacity: 2 })
  await post(parking + '/rates', {
    serviceId: service.id,
    blockMinutes: 60,
    graceMinutes: 0,
    blockPrice: 7.5,
  })
  const stay = await post(parking + '/entries', { requestId: randomUUID(), zoneId, plate: 'PRK-112' })
  await post(parking + '/stays/' + stay + '/exit', {
    requestId: randomUUID(),
    expectedAmount: 7.5,
    method: 'transfer',
    sessionId: cashSessionId,
  })
  await act('close', { cashSessionId, amount: 163.6 })

  const dateFilter = '?from=' + month + '&through=' + today
  const reportRoot = base + 'reports/'
  const kinds = [
    'sales',
    'collections',
    'debt',
    'cash',
    'stock',
    'ledger',
    'lots',
    'rentals',
    'parking',
    'parking-income',
    'billing',
  ]
  const snapshots: Record<string, Models['BranchReport']> = {}
  for (const kind of kinds) snapshots[kind] = await get(reportRoot + kind + dateFilter)
  expect(snapshots.cash.rows[0].slice(3)).toEqual(['100.00', '63.60', '163.60', '163.60', '0.00'])
  expect(snapshots.sales.rows).toHaveLength(1)
  expect(snapshots.sales.rows[0]).toEqual(expect.arrayContaining(['admin', '23.60']))
  expect(
    (await get<Models['BranchReport']>(reportRoot + 'sales' + dateFilter + '&seller=ausente')).rows,
  ).toHaveLength(0)
  expect(snapshots.collections.rows.map((r) => r[5])).toEqual(['23.60', '40.00', '7.50'])
  expect(snapshots.debt.rows[0].at(-1)).toBe('60.00')
  expect(snapshots.stock.rows[0].slice(2)).toEqual(['ACE-INTEG', 'kg', '18.00', '18.00', '144.00'])
  expect(snapshots.ledger.rows.map((r) => r[2])).toEqual(['receipt', 'sale-confirm', 'sale-dispatch'])
  expect(snapshots.lots.rows[0]).toEqual(expect.arrayContaining(['ACE-INTEG', 'T-42', 'Tacna']))
  expect(snapshots.rentals.rows[0].slice(2)).toEqual(['Ocupado', '60.00'])
  expect(snapshots.parking.rows[0].slice(2)).toEqual(['2', '0', '2'])
  expect(snapshots['parking-income'].rows[0].slice(3)).toEqual(['Salida', 'transfer', '7.50'])
  expect(snapshots.billing.rows[0]).toEqual(expect.arrayContaining(['simulation', 'rejected']))

  await page.goto('/reportes')
  await page.getByLabel('Sucursal activa').selectOption(String(branch.id))
    await page.getByRole('combobox', { name: 'Reporte', exact: true }).selectOption('cash')
  await expect(page.getByRole('heading', { name: 'Arqueos de caja' })).toBeVisible()
  await expect(page.getByRole('cell', { name: '163.60', exact: true })).toHaveCount(2)
  const csvPromise = page.waitForEvent('download')
  await page.getByRole('link', { name: 'Descargar CSV' }).click()
  const csv = await csvPromise
  await csv.saveAs(info.outputPath('arqueos.csv'))
  const pdfPromise = page.waitForEvent('download')
  await page.getByRole('link', { name: 'Descargar PDF' }).click()
  const pdf = await pdfPromise
  await pdf.saveAs(info.outputPath('arqueos.pdf'))
  const { readFile } = await import('node:fs/promises')
  expect((await readFile(info.outputPath('arqueos.pdf'))).subarray(0, 4).toString()).toBe('%PDF')
  const csvText = await readFile(info.outputPath('arqueos.csv'), 'utf8')
  expect(csvText).toContain('"163.60","163.60","0.00"')
  const scoped = await playwright.request.newContext({ baseURL })
  try {
    const userName = 'op-' + suffix,
      password = 'Re1' + randomUUID()
    await post('admin/users', { userName, password, grants: [{ branchId: branch.id, role: 'parking' }] })
    expect((await change(scoped, 'POST', 'auth/login', { userName, password })).status()).toBe(204)
    expect((await scoped.get('/api/v1/' + reportRoot + 'cash/export/csv')).status()).toBe(403)
    const other = await post('admin/branches', { name: 'Separada-' + suffix })
    expect((await scoped.get('/api/v1/branches/' + other.id + '/reports/parking')).status()).toBe(403)
  } finally {
    await scoped.dispose()
  }
  const version = (await (await request.get('/openapi/v1.json')).json()).info.version
  await writeFile(
    info.outputPath('release-evidence.json'),
    JSON.stringify(
      { version, branchId: branch.id, dateFilter, cdrPath, cdrHash, reports: snapshots },
      null,
      2,
    ),
  )
})
