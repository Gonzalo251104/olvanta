import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { Providers } from './app/providers'
import { BrowserRouter } from 'react-router'
import { Operations } from './app/Operations'
import './styles.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode><Providers><BrowserRouter><Operations /></BrowserRouter></Providers></StrictMode>,
)
