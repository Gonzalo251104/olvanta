import type { PropsWithChildren } from 'react'
import { createTheme, ThemeProvider } from '@mui/material/styles'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'

const theme = createTheme({
  palette: { primary: { main: '#284d39' } },
  typography: { fontFamily: 'Inter, "Segoe UI", sans-serif', button: { textTransform: 'none', fontWeight: 600 } },
  shape: { borderRadius: 8 },
})
const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })

export function Providers({ children }: PropsWithChildren) {
  return <ThemeProvider theme={theme}><QueryClientProvider client={client}>{children}</QueryClientProvider></ThemeProvider>
}
