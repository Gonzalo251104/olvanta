import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { checkSystem } from '../../shared/api'
import { SystemPage } from './SystemPage'

vi.mock('../../shared/api', () => ({ checkSystem: vi.fn() }))
afterEach(cleanup)

it('distinguishes a reachable API from an unavailable database', async () => {
  vi.mocked(checkSystem).mockResolvedValue({ databaseAvailable: false })
  render(<QueryClientProvider client={new QueryClient()}><SystemPage /></QueryClientProvider>)
  expect(await screen.findByText('Sin conexión')).toBeTruthy()
  expect(screen.getByText('En línea')).toBeTruthy()
})
