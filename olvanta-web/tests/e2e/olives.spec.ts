import { randomUUID } from 'node:crypto'
import { expect, test } from '@playwright/test'
import { change } from './change'
import type { Models } from '../../src/shared/workspace'

test('reconcilia el peso y el costo desde la recepción hasta el cliente', async ({ page }) => {
  test.skip(!process.env.OLVANTA_TEST_ADMIN_PASSWORD, 'Requiere un entorno de prueba.')
  test.setTimeout(120000)
  const req = page.request
  const suffix = randomUUID().slice(0, 8)
  const post = async (path: string, data: unknown) => {
    const response = await change(req, 'POST', path, data)
    expect(response.ok(), path + ' ' + (await response.text())).toBe(true)
    return response.status() === 204 ? null : response.json()
  }
  await post('auth/login', { userName: 'admin', password: process.env.OLVANTA_TEST_ADMIN_PASSWORD })
  const branch = await post('admin/branches', { name: 'Aceitunas-' + suffix })
  const base = 'branches/' + branch.id + '/'
  const olives = base + 'olives'
  const stock = base + 'inventory'
  const moves = stock + '/movements'
  const supplier = await post(base + 'masters/suppliers', {
    name: 'Productor de prueba',
    code: 'P1',
    active: true,
    personType: 'company',
    documentType: 'Otro',
  })
  const customer = await post(base + 'masters/customers', {
    name: 'Comprador del valle',
    code: 'C1',
    active: true,
    personType: 'company',
    documentType: 'Otro',
  })
  const createProduct = (
    name: string,
    code: string,
    unit: string,
    presentation: string,
    conversion: number,
  ) =>
    post(base + 'masters/products', {
      name,
      code,
      unit,
      presentation,
      conversion,
      active: true,
      category: 'Aceitunas',
      currency: 'PEN',
      taxRate: 18,
      price: 11.8,
    })
  const loose = await createProduct('Aceituna a granel', 'G1', 'kg', 'Granel', 1)
  const bag = await createProduct('Bolsa de aceitunas', 'B1', 'bolsa', 'Bolsa de 500 g', 0.5)
  const bucket = await createProduct('Balde de aceitunas', 'P1', 'balde', 'Balde de 1 kg', 1)
  await post(stock + '/warehouses', { name: 'Recepción' })
  await post(stock + '/warehouses', { name: 'Tienda' })
  const inventory = async (): Promise<Models['InventoryView']> =>
    (await req.get('/api/v1/' + stock)).json()
  const desk = async (): Promise<Models['OliveDesk']> => {
    const r = await req.get('/api/v1/' + olives)
    expect(r.ok(), await r.text()).toBe(true)
    return r.json()
  }
  await post(stock + '/purchases', {
    supplierId: supplier.id,
    reference: 'OC-OLV',
    lines: [{ productId: loose.id, quantity: 20, unitCost: 8 }],
  })
  const inv = await inventory()
  const warehouse = inv.warehouses.find((w) => w.name === 'Recepción')!.id
  const store = inv.warehouses.find((w) => w.name === 'Tienda')!.id
  const line = inv.purchases[0].lines[0].id
  const badWeight = {
    kind: 'receipt',
    operationId: randomUUID(),
    warehouseId: warehouse,
    productId: loose.id,
    purchaseLineId: line,
    quantity: 21,
    lot: 'RAIZ',
    reason: 'Recepción',
    olive: {
      gross: 22,
      tare: 2,
      variety: 'Sevillana',
      grade: 'Extra',
      origin: 'Tacna',
      supplierLot: 'T-07',
    },
  }
  expect((await change(req, 'POST', moves, badWeight)).status()).toBe(400)
  await page.goto('/aceitunas')
  await page.getByLabel('Sucursal activa').selectOption(String(branch.id))
  await page.getByRole('button', { name: 'Recepción pesada', exact: true }).click()
  await page.getByLabel('Lote interno').fill('RAIZ')
  await page.getByLabel('Lote del proveedor').fill('T-07')
  await page.getByLabel('Procedencia').fill('Tacna')
  await page.getByLabel('Variedad').fill('Sevillana')
  await page.getByLabel('Clasificación comercial').fill('Extra')
  await page.getByLabel('Peso bruto (kg)', { exact: true }).fill('22')
  await page.getByLabel('Tara (kg)', { exact: true }).fill('2')
  await page.getByLabel('Vencimiento').fill('2099-12-31')
  await page.getByRole('button', { name: 'Registrar recepción' }).click()
  await expect(page.getByRole('heading', { name: 'Existencias por lote' })).toBeVisible()
  const root = (await desk()).lots[0]
  expect(root).toMatchObject({ net: 20, gross: 22, tare: 2, expiry: '2099-12-31', supplierLot: 'T-07' })
  expect((await inventory()).purchases[0].lines[0].received).toBe(20)
  const repack = {
    operationId: randomUUID(),
    sourceId: root.id,
    warehouseId: warehouse,
    quantity: 12,
    lossKg: 1,
    reason: 'Descartar aceitunas dañadas',
    outputs: [
      { productId: bag.id, code: 'B-7', quantity: 20 },
      { productId: bucket.id, code: 'P-7', quantity: 1 },
    ],
  }
  expect((await change(req, 'POST', olives + '/repacking', { ...repack, lossKg: 0 })).status()).toBe(400)
  const [one, two] = await Promise.all([
    post(olives + '/repacking', repack),
    post(olives + '/repacking', repack),
  ])
  expect(one).toBe(two)
  const packed = (await desk()).lots.find((l) => l.code === 'B-7')!
  expect(packed).toMatchObject({ rootId: root.id, processId: one, kgPerUnit: 0.5, wholeUnits: true })
  const move = {
    kind: 'transfer',
    operationId: randomUUID(),
    warehouseId: warehouse,
    destinationId: store,
    productId: bag.id,
    lot: 'B-7',
    quantity: 4,
    reason: 'Reposición en tienda',
  }
  expect((await change(req, 'POST', moves, { ...move, quantity: 0.5 })).status()).toBe(409)
  await post(moves, move)
  const quote = {
    requestId: randomUUID(),
    customerId: customer.id,
    terms: 'credit',
    dueDate: '2099-12-31',
    lines: [{ itemId: bag.id, warehouseId: store, lot: 'B-7', quantity: 3, discount: 0 }],
  }
  expect(
    (
      await change(req, 'POST', base + 'sales/quotes', {
        ...quote,
        lines: [{ ...quote.lines[0], quantity: 0.5 }],
      })
    ).status(),
  ).toBe(400)
  const sale = (await post(base + 'sales/quotes', quote)).saleId
  const action = (kind: string, fields: Record<string, unknown> = {}) =>
    post(base + 'sales/actions', { requestId: randomUUID(), kind, saleId: sale, ...fields })
  for (const step of ['order', 'confirm', 'dispatch']) await action(step)
  const sales: Models['SaleDetail'][] = await (await req.get('/api/v1/' + base + 'sales')).json()
  const sold = sales.find((s) => s.id === sale)!
  await action('return', { lineId: sold.lines[0].id, quantity: 1, note: 'Paquete íntegro devuelto' })
  expect(
    (await desk()).stock.find((s) => s.lotId === packed.id && s.warehouseId === store),
  ).toMatchObject({ onHand: 2, value: 8 })
  const trace: Models['OliveTrace'] = await (
    await req.get('/api/v1/' + olives + '/lots/' + packed.id + '/trace')
  ).json()
  expect(trace.receipt).toMatchObject({ reference: 'OC-OLV', value: 160 })
  expect(trace.repacking[0]).toMatchObject({ inputKg: 12, lossKg: 1, lossValue: 8 })
  expect(trace.outputs).toEqual(
    expect.arrayContaining([
      expect.objectContaining({ lotId: packed.id, quantity: 20, kg: 10, value: 80 }),
    ]),
  )
  expect(
    trace.outputs.reduce((sum, x) => sum + Number(x.kg), 0) + Number(trace.repacking[0].lossKg),
  ).toBe(12)
  expect(trace.deliveries).toMatchObject([
    { saleId: sale, kind: 'sale-return', kg: 0.5 },
    { saleId: sale, kind: 'sale-dispatch', kg: -1.5 },
  ])
  await page.getByRole('button', { name: 'Reenvasado y merma', exact: true }).click()
  await page
    .getByRole('combobox', { name: 'Lote de origen', exact: true })
    .selectOption(root.id + '-' + warehouse)
  await page.getByLabel('Cantidad consumida (kg)', { exact: true }).fill('1')
  await page
    .getByRole('combobox', { name: 'Producto de salida', exact: true })
    .selectOption(String(bag.id))
  await page.getByLabel('Lote resultante').fill('B-8')
  await page.getByLabel('Cantidad resultante').fill('2')
  await page.getByLabel('Motivo y observaciones').fill('Reenvasado en tienda')
  await page.getByRole('button', { name: 'Registrar reenvasado' }).click()
  await expect(page.getByText('B-8 · Bolsa de aceitunas')).toBeVisible()
  await page.getByRole('button', { name: 'Trazabilidad', exact: true }).click()
  await page.getByRole('combobox', { name: 'Cliente', exact: true }).selectOption(String(customer.id))
  await page.getByRole('button', { name: 'B-7 · Bolsa de aceitunas' }).first().click()
  await expect(page.getByRole('heading', { name: 'RAIZ · Aceituna a granel' })).toBeVisible()
  await expect(page.getByText(/Productor de prueba · P1 · Lote de proveedor T-07/)).toBeVisible()
  await expect(page.getByText(/12 kg consumidos · 1 kg de merma/)).toBeVisible()
  await page.setViewportSize({ width: 390, height: 844 })
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390)
  const other = await post('admin/branches', { name: 'Otra sucursal-' + suffix })
  expect(
    (await req.get('/api/v1/branches/' + other.id + '/olives/lots/' + packed.id + '/trace')).status(),
  ).toBe(404)
})
