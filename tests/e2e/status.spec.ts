import { expect, test } from '@playwright/test'

test('displays the live API and database status', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Sistema operativo' })).toBeVisible()
  const nextStatus = page.waitForResponse(response => response.url().endsWith('/api/v1/system/status'))
  await page.getByRole('button', { name: 'Volver a comprobar' }).click()
  expect((await nextStatus).status()).toBe(200)
  await expect(page.getByText('Conectada', { exact: true })).toBeVisible()
})
