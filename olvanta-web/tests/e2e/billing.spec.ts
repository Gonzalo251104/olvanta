import { randomUUID } from 'node:crypto'
import { writeFile } from 'node:fs/promises'
import { expect, test } from '@playwright/test'
import { change } from './change'
import type { Models } from '../../src/shared/workspace'

test('emite documentos y concilia respuestas en la simulación fiscal', async ({ page }, testInfo) => {
  test.skip(!process.env.OLVANTA_TEST_ADMIN_PASSWORD, 'Requiere un entorno de prueba.')
  test.setTimeout(120000)
  const request = page.request
  const suffix = randomUUID().slice(0, 8)
  const post = async (path: string, data: unknown) => {
    const r = await change(request, 'POST', path, data)
    expect(r.ok(), path + ' ' + (await r.text())).toBe(true)
    return r.status() === 204 ? null : r.json()
  }
  await post('auth/login', { userName: 'admin', password: process.env.OLVANTA_TEST_ADMIN_PASSWORD })
  const branch = await post('admin/branches', { name: 'Fiscal-' + suffix })
  const base = 'branches/' + branch.id + '/'
  const billing = base + 'billing'
  const get = async (path: string) => {
    const r = await request.get('/api/v1/' + path)
    expect(r.ok(), path + ' ' + (await r.text())).toBe(true)
    return r.json()
  }
  const desk = async (): Promise<Models['BillingDesk']> => get(billing)
  expect((await desk()).mode).toBe('simulation')
  const ruc = '20000000001'
  expect(
    (
      await change(request, 'PUT', 'admin/organization/company', {
        name: 'Emisor de simulación',
        taxId: ruc,
      })
    ).status(),
  ).toBe(204)
  await post('admin/organization/establishments', {
    branchId: branch.id,
    name: 'Local fiscal',
    code: String(branch.id).padStart(4, '0'),
    address: 'Calle de prueba 12',
  })
  const org: Models['OrganizationView'] = await get('admin/organization')
  const establishmentId = org.establishments.find((s) => s.branchId === branch.id)!.id
  const code = Number(establishmentId).toString(36).toUpperCase().padStart(3, '0')
  for (const [kind, prefix] of [
    ['invoice', 'F'],
    ['receipt', 'B'],
    ['credit', 'F'],
    ['debit', 'F'],
  ]) {
    await post('admin/organization/series', { establishmentId, documentType: kind, code: prefix + code })
  }
  const customer = await post(base + 'masters/customers', {
    name: 'Cliente de prueba',
    code: ruc,
    active: true,
    personType: 'company',
    documentType: 'RUC',
  })
  const service = await post(base + 'masters/services', {
    name: 'Selección de aceitunas',
    code: 'S1',
    active: true,
    category: 'Servicio',
    unit: 'servicio',
    price: 118,
    currency: 'PEN',
    taxRate: 18,
    conversion: 1,
  })
  const action = (kind: string, saleId: number, extra: Record<string, unknown> = {}) =>
    post(base + 'sales/actions', { kind, saleId, requestId: randomUUID(), ...extra })
  async function sale() {
    const { saleId } = await post(base + 'sales/quotes', {
      requestId: randomUUID(),
      customerId: customer.id,
      terms: 'credit',
      dueDate: '2099-01-01',
      lines: [{ itemId: service.id, quantity: 2, discount: 0 }],
    })
    await action('order', saleId)
    await action('confirm', saleId)
    await action('dispatch', saleId)
    return saleId
  }
  const saleId = await sale()
  const series = (await desk()).series
  const prepare = (kind: string, id: number, extra: Record<string, unknown> = {}) =>
    post(billing, {
      kind,
      saleId: id,
      seriesId: series.find((s) => s.kind === kind)?.id,
      requestId: randomUUID(),
      ...extra,
    })
  const submit = (id: number, action = 'send', scenario = 'accept') =>
    post(billing + '/' + id + '/submission', { action, scenario })
  const detail = async (id: number): Promise<Models['BillDetail']> => get(billing + '/' + id)
  await page.goto('/facturacion')
  await page.getByLabel('Sucursal activa').selectOption(String(branch.id))
  await page.getByRole('button', { name: 'Nuevo comprobante' }).click()
  await page
    .getByLabel('Serie fiscal')
    .selectOption(String(series.find((s) => s.kind === 'invoice')!.id))
  await page.getByRole('button', { name: 'Preparar y firmar' }).click()
  await expect(page.getByRole('heading', { name: 'Factura F' + code + '-1' })).toBeVisible()
  const invoice = Number((await desk()).documents[0].id)
  await page.getByLabel('Respuesta simulada').selectOption('lost')
  await page.getByRole('button', { name: 'Simular envío' }).click()
  await expect(page.getByRole('button', { name: 'Consultar resultado' })).toBeVisible()
  expect(
    (
      await change(request, 'POST', billing + '/' + invoice + '/submission', { action: 'send' })
    ).status(),
  ).toBe(409)
  await page.getByRole('button', { name: 'Programar consulta' }).click()
  await expect(page.getByRole('link', { name: 'CDR', exact: true })).toBeVisible({ timeout: 20000 })
  const original = await detail(invoice)
  expect(original).toMatchObject({
    data: { tax: 36, total: 236 },
    document: { status: 'accepted' },
    attempts: [
      { action: 'send', outcome: 'uncertain' },
      { action: 'query', outcome: 'accepted', actor: 'Conciliación programada' },
    ],
  })
  for (const ext of ['pdf', 'xml', 'zip', 'cdr']) {
    const res = await request.get('/api/v1/' + billing + '/' + invoice + '/files/' + ext)
    expect(res.ok()).toBe(true)
    const bytes = await res.body()
    if (ext === 'pdf') {
      expect(bytes.subarray(0, 4).toString()).toBe('%PDF')
      await writeFile(testInfo.outputPath('factura.pdf'), bytes)
    } else if (ext === 'xml') expect(bytes.toString()).toContain(original.digest)
    else expect(bytes.subarray(0, 2).toString()).toBe('PK')
  }
  const lineId = original.data.lines[0].saleLineId
  const returned = await action('return', saleId, { lineId, quantity: 1, note: 'Devolución parcial' })
  const credit = await prepare('credit', saleId, {
    parentId: invoice,
    returnId: returned.eventId,
    reason: 'Devolución parcial',
  })
  await submit(credit)
  expect((await detail(credit)).data.total).toBe(118)
  const charge = {
    parentId: invoice,
    saleLineId: lineId,
    reason: 'Aumento de valor',
    chargeReference: 'A1',
    charge: 11.8,
  }
  const rejected = await prepare('debit', saleId, charge)
  await submit(rejected, 'send', 'reject')
  expect((await detail(rejected)).document.status).toBe('rejected')
  const debit = await prepare('debit', saleId, charge)
  await submit(debit)
  expect((await detail(debit)).data.tax).toBe(1.8)
  const receiptSale = await sale()
  const key = randomUUID()
  const [receipt, copy] = await Promise.all([
    prepare('receipt', receiptSale, { requestId: key }),
    prepare('receipt', receiptSale, { requestId: key }),
  ])
  expect(copy).toBe(receipt)
  for (const [kind, state] of [
    ['summary', 'accepted'],
    ['void', 'voided'],
  ]) {
    const report = await prepare(kind, receiptSale, {
      parentId: receipt,
      notDelivered: true,
      reason: 'Sin otorgar',
    })
    await submit(report)
    expect((await detail(report)).document.status).toBe('processing')
    await submit(report, 'query')
    expect((await detail(receipt)).document.status).toBe(state)
  }
  const replacement = await prepare('invoice', receiptSale)
  await submit(replacement)
  const cancellation = await prepare('void', receiptSale, {
    parentId: replacement,
    notDelivered: true,
    reason: 'Sin otorgar',
  })
  await submit(cancellation)
  await submit(cancellation, 'query')
  expect((await detail(cancellation)).document.type).toBe('RA')
  expect((await detail(replacement)).document.status).toBe('voided')
  expect((await detail(invoice)).digest).toBe(original.digest)
  const other = await post('admin/branches', { name: 'Aislada-' + suffix })
  expect(
    (await request.get('/api/v1/branches/' + other.id + '/billing/' + invoice + '/files/xml')).status(),
  ).toBe(404)
})
