import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { Providers } from './app/providers'
import { BrowserRouter } from 'react-router'
import { Operations } from './app/Operations'
import '@fontsource/instrument-serif/latin-400.css'
import '@fontsource-variable/geist'
import './styles.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Providers>
      <BrowserRouter>
        <Operations />
      </BrowserRouter>
    </Providers>
  </StrictMode>,
)
