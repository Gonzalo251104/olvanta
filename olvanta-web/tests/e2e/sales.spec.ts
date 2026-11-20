import { randomUUID } from 'node:crypto'
import { expect, test } from '@playwright/test'
import type { Models } from '../../src/shared/workspace'
import { change } from './change'

test('reconcilia ventas, devoluciones, abonos y cierre de caja', async ({
  page,
  playwright,
  baseURL,
}) => {
  test.skip(!process.env.OLVANTA_TEST_ADMIN_PASSWORD, 'Requiere un entorno de prueba.')
  test.setTimeout(120000)
  const admin = page.request
  const suffix = randomUUID().slice(0, 8)
  expect(
    (
      await change(admin, 'POST', 'auth/login', {
        userName: 'admin',
        password: process.env.OLVANTA_TEST_ADMIN_PASSWORD,
      })
    ).status(),
  ).toBe(204)
  const make = async (path: string, data: unknown) => {
    const r = await change(admin, 'POST', path, data)
    expect(r.ok(), await r.text()).toBe(true)
    return r.status() === 204 ? null : r.json()
  }
  const branch = await make('admin/branches', { name: 'Ventas-' + suffix })
  const base = 'branches/' + branch.id + '/'
  const customer = await make(base + 'masters/customers', {
    name: 'Comprador',
    code: 'C1',
    active: true,
    personType: 'company',
    documentType: 'Otro',
  })
  const common = {
    name: 'Aceituna',
    code: 'K1',
    active: true,
    category: 'Mercadería',
    unit: 'kg',
    price: 11.8,
    currency: 'PEN',
    taxRate: 18,
    conversion: 1,
  }
  const product = await make(base + 'masters/products', common)
  const service = await make(base + 'masters/services', {
    ...common,
    name: 'Envío',
    code: 'S1',
    unit: 'servicio',
    price: 50,
  })
  await make(base + 'inventory/warehouses', { name: 'Bodega' })
  const inventory = async (): Promise<Models['InventoryView']> =>
    (await admin.get('/api/v1/' + base + 'inventory')).json()
  const warehouse = (await inventory()).warehouses[0].id
  await make(base + 'inventory/movements', {
    kind: 'entry',
    operationId: randomUUID(),
    warehouseId: warehouse,
    productId: product.id,
    quantity: 5,
    unitCost: 8,
    lot: 'A',
    reason: 'Guía',
  })
  const act = async (kind: string, extra: Record<string, unknown> = {}) =>
    change(admin, 'POST', base + 'sales/actions', { requestId: randomUUID(), kind, ...extra })
  const good = async (kind: string, extra: Record<string, unknown> = {}) => {
    const r = await act(kind, extra)
    expect(r.status(), await r.text()).toBe(200)
    return r.json()
  }
  const sales = async (): Promise<Models['SaleDetail'][]> =>
    (await admin.get('/api/v1/' + base + 'sales')).json()
  const drawer = async (): Promise<Models['CashDetail'][]> =>
    (await admin.get('/api/v1/' + base + 'sales/cash')).json()
  await page.goto('/ventas')
  await page.getByLabel('Sucursal activa').selectOption(String(branch.id))
  await page.getByRole('button', { name: 'Caja', exact: true }).click()
  await page.getByLabel('Fondo inicial').fill('100')
  await page.getByRole('button', { name: 'Abrir caja' }).click()
  await expect(page.getByRole('heading', { name: /Caja abierta/ })).toBeVisible()
  const sessionId = (await drawer())[0].id
  await page.getByRole('button', { name: 'Cotizar', exact: true }).click()
  await page.getByLabel('Artículo').selectOption(String(product.id))
  await page.getByLabel('Cantidad', { exact: true }).fill('2')
  await page.getByRole('combobox', { name: 'Lote', exact: true }).selectOption('A')
  await page.getByRole('button', { name: 'Guardar cotización' }).click()
  await page.getByRole('button', { name: 'Convertir en pedido' }).click()
  await page.getByRole('button', { name: 'Confirmar venta' }).click()
  await expect(page.getByRole('heading', { name: /Confirmada/ })).toBeVisible()
  const cashSale = (await sales())[0]
  expect(cashSale).toMatchObject({ total: 23.6, balance: 23.6, lines: [{ tax: 3.6 }] })
  expect((await inventory()).balances[0]).toMatchObject({ quantity: 5, reserved: 2 })
  expect((await act('dispatch', { saleId: cashSale.id })).status()).toBe(409)
  await page.getByRole('button', { name: 'Registrar cobro' }).click()
  await page.getByRole('button', { name: 'Guardar operación' }).click()
  await page.getByRole('button', { name: 'Despachar' }).click()
  await expect(page.getByRole('heading', { name: /Despachada/ })).toBeVisible()
  expect((await inventory()).balances[0]).toMatchObject({ quantity: 3, reserved: 0, value: 24 })
  await page.getByRole('button', { name: 'Registrar devolución' }).click()
  await page.getByLabel('Cantidad devuelta').fill('1')
  await page.getByLabel('Motivo', { exact: true }).fill('Envase dañado')
  await page.getByRole('button', { name: 'Guardar operación' }).click()
  await page.getByRole('button', { name: 'Reembolsar' }).click()
  await page.getByLabel('Motivo', { exact: true }).fill('Reembolso de una unidad')
  await page.getByRole('button', { name: 'Guardar operación' }).click()
  await expect(page.getByText(/Reembolso de una unidad/)).toBeVisible()
  expect((await inventory()).balances[0]).toMatchObject({ quantity: 4, reserved: 0, value: 32 })
  const quote = {
    requestId: randomUUID(),
    customerId: customer.id,
    terms: 'credit',
    dueDate: '2099-01-01',
    lines: [{ itemId: service.id, quantity: 1, discount: 10 }],
  }
  const credit = await make(base + 'sales/quotes', quote)
  const saleId = credit.saleId
  await good('order', { saleId })
  await good('confirm', { saleId })
  await good('dispatch', { saleId })
  const payment = {
    requestId: randomUUID(),
    saleId,
    cashSessionId: sessionId,
    method: 'card',
    amount: 15,
  }
  const [a, b] = await Promise.all([good('pay', payment), good('pay', payment)])
  expect(a.eventId).toBe(b.eventId)
  expect((await act('pay', { ...payment, amount: 16 })).status()).toBe(409)
  expect((await sales()).find((s) => s.id === saleId)).toMatchObject({ total: 45, balance: 30 })
  await good('cash-in', { cashSessionId: sessionId, amount: 5, note: 'Cambio' })
  await good('cash-out', { cashSessionId: sessionId, amount: 3, note: 'Traslado a bóveda' })
  expect((await drawer())[0].expected).toBe(113.8)
  await page.getByRole('button', { name: 'Caja', exact: true }).click()
  await page.getByLabel('Movimiento de caja').selectOption('close')
  await page.getByLabel('Efectivo contado').fill('113.50')
  await page.getByRole('button', { name: 'Cerrar caja' }).click()
  await expect(page.getByText(/Diferencia/)).toBeVisible()
  expect((await drawer())[0]).toMatchObject({ expected: 113.8, counted: 113.5, difference: -0.3 })
  expect((await act('pay', { ...payment, requestId: randomUUID(), amount: 1 })).status()).toBe(409)
  const duplicate = await make(base + 'sales/quotes', {
    ...quote,
    requestId: randomUUID(),
    lines: [1, 4].map((quantity) => ({
      itemId: product.id,
      warehouseId: warehouse,
      lot: 'A',
      discount: 0,
      quantity,
    })),
  })
  await good('order', { saleId: duplicate.saleId })
  expect((await act('confirm', { saleId: duplicate.saleId })).status()).toBe(409)
  expect((await inventory()).balances[0].reserved).toBe(0)
  const other = await make('admin/branches', { name: 'Otra-' + suffix })
  expect(
    (
      await change(admin, 'POST', 'branches/' + other.id + '/sales/actions', {
        requestId: randomUUID(),
        kind: 'order',
        saleId,
      })
    ).status(),
  ).toBe(404)
  const worker = await playwright.request.newContext({ baseURL })
  try {
    const userName = 'v-' + suffix
    const password = 'Ve1' + randomUUID()
    await make('admin/users', {
      userName,
      password,
      grants: [{ branchId: branch.id, role: 'salesperson' }],
    })
    expect((await change(worker, 'POST', 'auth/login', { userName, password })).status()).toBe(204)
    expect(
      (
        await change(worker, 'POST', base + 'sales/quotes', { ...quote, requestId: randomUUID() })
      ).status(),
    ).toBe(403)
    expect((await worker.get('/api/v1/' + base + 'sales/cash')).status()).toBe(403)
    expect(
      (
        await change(worker, 'POST', base + 'sales/actions', {
          kind: 'open',
          amount: 0,
          requestId: randomUUID(),
        })
      ).status(),
    ).toBe(403)
  } finally {
    await worker.dispose()
  }
  await page.setViewportSize({ width: 390, height: 844 })
  await page.getByRole('button', { name: 'Por cobrar', exact: true }).click()
  await page.getByRole('button', { name: 'Actualizar', exact: true }).click()
  await expect(page.getByRole('button', { name: '#' + saleId + ' · Comprador · C1' })).toBeVisible()
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390)
  await page.emulateMedia({ media: 'print' })
  await expect(page.getByRole('navigation', { name: 'Operaciones' })).toBeHidden()
  await expect(page.getByRole('heading', { name: /Despachada/ })).toBeVisible()
})
