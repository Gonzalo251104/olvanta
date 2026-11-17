import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { Providers } from './app/providers'
import { SystemPage } from './features/system/SystemPage'
import './styles.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode><Providers><SystemPage /></Providers></StrictMode>,
)
