import { randomUUID } from 'node:crypto'
import { expect, test } from '@playwright/test'
import type { Models } from '../../src/shared/workspace'
import { change } from './change'

test('compra, recepción parcial, traslado y dos reservas por la última unidad', async ({
  page,
  playwright,
  baseURL,
}) => {
  test.skip(!process.env.OLVANTA_TEST_ADMIN_PASSWORD, 'Requiere un entorno de pruebas.')
  test.setTimeout(90000)
  const admin = page.request
  const suffix = randomUUID().slice(0, 8)
  const branchName = 'Inventario-' + suffix
  expect(
    (
      await change(admin, 'POST', 'auth/login', {
        userName: 'admin',
        password: process.env.OLVANTA_TEST_ADMIN_PASSWORD,
      })
    ).status(),
  ).toBe(204)
  const branch = await (await change(admin, 'POST', 'admin/branches', { name: branchName })).json()
  const base = 'branches/' + branch.id + '/'
  const path = base + 'inventory'
  const snapshot = async (): Promise<Models['InventoryView']> => {
    const result = await admin.get('/api/v1/' + path)
    expect(result.status()).toBe(200)
    return result.json()
  }
  const product = await (
    await change(admin, 'POST', base + 'masters/products', {
      name: 'Aceitunas verdes',
      code: 'OLV',
      active: true,
      category: 'Granel',
      unit: 'kg',
      conversion: 1,
      currency: 'PEN',
      price: 4,
      taxRate: 0,
    })
  ).json()
  expect(
    (
      await change(admin, 'POST', base + 'masters/suppliers', {
        name: 'Cooperativa',
        code: 'COOP',
        active: true,
        personType: 'company',
        documentType: 'Otro',
      })
    ).status(),
  ).toBe(201)
  expect((await change(admin, 'POST', path + '/warehouses', { name: 'Secundario' })).status()).toBe(204)
  await page.goto('/inventario')
  await page.getByLabel('Sucursal activa').selectOption(String(branch.id))
  await page.getByRole('button', { name: 'Almacenes', exact: true }).click()
  await page.getByLabel('Nombre del almacén').fill('Central')
  await page.getByRole('button', { name: 'Crear almacén' }).click()
  await expect(page.getByRole('listitem').filter({ hasText: 'Central' })).toBeVisible()
  await page.getByRole('button', { name: 'Compras', exact: true }).click()
  await page.getByLabel('Referencia de compra').fill('OC-1')
  await page.getByLabel('Cantidad en unidad base').fill('3')
  await page.getByLabel('Costo por unidad').fill('5')
  await page.getByRole('button', { name: 'Registrar compra' }).click()
  await page.getByRole('button', { name: 'Recibir mercadería' }).click()
  const ordered = await snapshot()
  expect(ordered.balances).toEqual([])
  const [source, dest] = ordered.warehouses
  const line = ordered.purchases[0].lines[0].id
  await page.getByLabel('Cantidad en unidad base').fill('2')
  await page.getByLabel('Lote (opcional)').fill('L-10')
  await page.getByLabel('Vencimiento').fill('2030-12-31')
  await page.getByLabel('Motivo o documento').fill('Guía de remisión 1')
  await page.getByRole('button', { name: 'Confirmar movimiento' }).click()
  await expect(page.getByRole('status')).toHaveText('Operación registrada.')
  expect((await snapshot()).purchases[0].lines[0].received).toBe(2)
  const movement = (
    kind: string,
    extra: Partial<Models['MovementInput']> = {},
  ): Models['MovementInput'] => ({
    kind,
    operationId: randomUUID(),
    quantity: 1,
    reason: 'Guía de prueba',
    warehouseId: source.id,
    productId: product.id,
    lot: 'L-10',
    ...extra,
  })
  const move = (body: Models['MovementInput']) => change(admin, 'POST', path + '/movements', body)
  expect((await move(movement('receipt', { purchaseLineId: line, quantity: 2 }))).status()).toBe(409)
  expect((await move(movement('entry', { unitCost: 20, expiry: '2031-01-01' }))).status()).toBe(409)
  expect((await move(movement('entry', { unitCost: 20, lot: 'L-11' }))).status()).toBe(204)
  const transfer = movement('transfer', { destinationId: dest.id })
  expect((await move(transfer)).status()).toBe(204)
  expect((await move(transfer)).status()).toBe(204)
  expect((await move({ ...transfer, quantity: 2 })).status()).toBe(409)
  expect(
    (
      await change(admin, 'PUT', path + '/minimum', {
        warehouseId: dest.id,
        productId: product.id,
        quantity: 0.5,
      })
    ).status(),
  ).toBe(204)
  const responses = await Promise.all([
    move(movement('reserve', { warehouseId: dest.id })),
    move(movement('reserve', { warehouseId: dest.id })),
  ])
  expect(responses.map((r) => r.status()).sort()).toEqual([204, 409])
  const stock = await snapshot()
  expect(stock.balances.find((b) => b.warehouseId === source.id)).toMatchObject({
    quantity: 2,
    value: 20,
    reserved: 0,
  })
  expect(stock.balances.find((b) => b.warehouseId === dest.id)).toMatchObject({
    quantity: 1,
    value: 10,
    reserved: 1,
    minimum: 0.5,
    lots: [{ lot: 'L-10', expiry: '2030-12-31', quantity: 1, reserved: 1 }],
  })
  expect((await move(movement('issue', { warehouseId: dest.id }))).status()).toBe(409)
  expect(
    (
      await move(movement('release', { warehouseId: dest.id, relatedId: stock.reservations[0].id }))
    ).status(),
  ).toBe(204)
  expect(
    (
      await move(movement('release', { warehouseId: dest.id, relatedId: stock.reservations[0].id }))
    ).status(),
  ).toBe(409)
  expect((await move(movement('issue', { warehouseId: dest.id }))).status()).toBe(204)
  const worker = await playwright.request.newContext({ baseURL })
  try {
    const userName = 'alm-' + suffix
    const password = 'Al1' + randomUUID()
    expect(
      (
        await change(admin, 'POST', 'admin/users', {
          userName,
          password,
          grants: [{ branchId: branch.id, role: 'warehouse' }],
        })
      ).status(),
    ).toBe(204)
    expect((await change(worker, 'POST', 'auth/login', { userName, password })).status()).toBe(204)
    expect((await change(worker, 'POST', path + '/movements', movement('adjust-out'))).status()).toBe(
      403,
    )
    const other = await (
      await change(admin, 'POST', 'admin/branches', { name: 'Ajena-' + suffix })
    ).json()
    expect((await worker.get('/api/v1/branches/' + other.id + '/inventory')).status()).toBe(403)
    expect(
      (
        await change(
          admin,
          'POST',
          'branches/' + other.id + '/inventory/movements',
          movement('entry', { unitCost: 1 }),
        )
      ).status(),
    ).toBe(404)
  } finally {
    await worker.dispose()
  }
  expect((await move(movement('adjust-out', { reason: 'Merma por rotura' }))).status()).toBe(204)
  const history = async (): Promise<Models['MovementView'][]> =>
    (await admin.get('/api/v1/' + path + '/movements')).json()
  const adjustment = (await history()).find((m) => m.kind === 'adjust-out')!
  expect(
    (
      await move(
        movement('adjust-in', {
          reason: 'Corrección del conteo',
          relatedId: adjustment.id,
          unitCost: 10,
        }),
      )
    ).status(),
  ).toBe(204)
  const finalReceipt = movement('receipt', { purchaseLineId: line })
  const receipts = await Promise.all([move(finalReceipt), move(finalReceipt)])
  expect(receipts.map(r => r.status())).toEqual([204, 204])
  expect((await snapshot()).purchases[0].lines[0].received).toBe(3)
  expect((await move(movement('receipt', { purchaseLineId: line }))).status()).toBe(409)
  const ledger = await history()
  expect(ledger.filter(m => m.operationId === finalReceipt.operationId)).toHaveLength(1)
  for (const balance of (await snapshot()).balances) {
    const rows = ledger.filter((m) => m.warehouseId === balance.warehouseId)
    for (const field of ['quantity', 'reserved', 'value'] as const)
      expect(balance[field]).toBe(rows.reduce((sum, m) => sum + Number(m[field]), 0))
  }
  await page.setViewportSize({ width: 390, height: 844 })
  await page.getByRole('button', { name: 'Kardex', exact: true }).click()
  await expect(page.getByRole('cell').filter({ hasText: 'Corrección del conteo' })).toBeVisible()
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390)
})
