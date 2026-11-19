import { randomUUID } from 'node:crypto'
import { expect, test } from '@playwright/test'
import type { Models } from '../../src/shared/workspace'
import { change } from './change'

test('previsualiza CSV, verifica duplicados al confirmar y preserva descuentos históricos', async ({
  page,
}) => {
  test.skip(!process.env.OLVANTA_TEST_ADMIN_PASSWORD, 'Requiere un entorno de prueba.')
  const request = page.request
  async function post(path: string, data: unknown) {
    const response = await change(request, 'POST', path, data)
    expect(response.ok(), path + ' ' + (await response.text())).toBe(true)
    return response.status() === 204 ? null : response.json()
  }
  await post('auth/login', { userName: 'admin', password: process.env.OLVANTA_TEST_ADMIN_PASSWORD })
  const branch = await post('admin/branches', { name: 'Mantenimiento-' + randomUUID().slice(0, 8) })
  const base = 'branches/' + branch.id + '/'
  const customers = base + 'masters/customers'
  const people = async (): Promise<Models['MasterPage']> =>
    (await request.get('/api/v1/' + customers)).json()
  await page.goto('/maestros/customers')
  await page.getByLabel('Sucursal activa').selectOption(String(branch.id))
  await page.getByRole('button', { name: 'Importar', exact: true }).click()
  const upload = async (text: string) => {
    await page
      .locator('input[type=file]')
      .setInputFiles({ name: 'clientes.csv', mimeType: 'text/csv', buffer: Buffer.from(text) })
    await page.getByRole('button', { name: 'Procesar archivo' }).click()
    await expect(page.getByRole('heading', { name: 'Vista previa del CSV' })).toBeVisible()
  }
  const header = 'code;name;personType;documentType;active\n'
  const sample = '0008;"Tienda; Centro";company;Otro;true\n'
  await upload(header + sample + '9;Fila errada;empresa;Otro;true\n')
  await expect(page.getByRole('alert')).toContainText('Fila 3.')
  await expect(page.getByRole('button', { name: 'Confirmar importación' })).toBeDisabled()
  expect((await people()).total).toBe(0)
  await upload(header + sample + '0009;Comprador;person;Otro;1\n')
  await expect(page.getByRole('cell', { name: 'Tienda; Centro' })).toBeVisible()
  expect((await people()).total).toBe(0)
  await post(customers, {
    name: 'Registro concurrente',
    code: '0009',
    active: true,
    personType: 'person',
    documentType: 'Otro',
  })
  await page.getByRole('button', { name: 'Confirmar importación' }).click()
  await expect(page.getByRole('alert')).toContainText('Fila 3. El identificador ya existe')
  expect((await people()).total).toBe(1)
  await upload(header + sample)
  await page.getByRole('button', { name: 'Confirmar importación' }).click()
  await expect(page.getByRole('status')).toContainText('Cambios guardados')
  const customer = (await people()).items.find((m) => m.data.code === '0008')!
  expect(customer.data.name).toBe('Tienda; Centro')
  const service = await post(base + 'masters/services', {
    name: 'Pesaje',
    code: 'SV',
    active: true,
    category: 'Servicios',
    unit: 'servicio',
    currency: 'PEN',
    price: 100,
    taxRate: 18,
  })
  const defaults = 'admin/organization/branches/' + branch.id + '/defaults'
  async function cap(maxDiscount: number) {
    expect(
      (await change(request, 'PUT', defaults, { currency: 'PEN', taxRate: 18, maxDiscount })).status(),
    ).toBe(204)
  }
  function quote(discount: number) {
    return {
      requestId: randomUUID(),
      customerId: customer.id,
      terms: 'credit',
      dueDate: '2099-01-01',
      lines: [{ itemId: service.id, quantity: 1, discount }],
    }
  }
  await cap(10)
  const original = await post(base + 'sales/quotes', quote(10))
  await cap(5)
  expect((await change(request, 'POST', base + 'sales/quotes', quote(10))).status()).toBe(400)
  await post(base + 'sales/actions', { requestId: randomUUID(), kind: 'order', saleId: original.saleId })
  await post(base + 'sales/actions', {
    requestId: randomUUID(),
    kind: 'confirm',
    saleId: original.saleId,
  })
  const sales: Models['SaleDetail'][] = await (await request.get('/api/v1/' + base + 'sales')).json()
  expect(sales.find((x) => x.id === original.saleId)).toMatchObject({
    total: 90,
    lines: [{ discount: 10 }],
  })
})
