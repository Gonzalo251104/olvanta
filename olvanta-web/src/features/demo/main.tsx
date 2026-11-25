import { useState } from 'react'
import { createRoot } from 'react-dom/client'
import { Brand } from '../../app/Brand'
import reportData from './reports.json'
import '@fontsource/instrument-serif/latin-400.css'
import '@fontsource-variable/geist'
import '../../styles.css'
import './demo.css'

type ReportKey = keyof typeof reportData
const reports = Object.keys(reportData) as ReportKey[]
const photo = new URL('../../../public/seleccion-aceitunas.webp', import.meta.url).href

function downloadReport(key: ReportKey) {
  const { columns, rows } = reportData[key]
  const csv = [columns, ...rows]
    .map((row) => row.map((cell) => '"' + cell.replaceAll('"', '""') + '"').join(','))
    .join('\r\n')
  const url = URL.createObjectURL(new Blob(['\uFEFF', csv], { type: 'text/csv;charset=utf-8' }))
  const link = document.createElement('a')
  link.href = url
  link.download = 'olvanta-demo-' + key + '.csv'
  link.click()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

function Demo() {
  const [selected, setSelected] = useState<ReportKey>('sales')
  const report = reportData[selected]

  return (
    <>
      <header className="masthead">
        <div className="frame bar">
          <a className="brand-link" href="#inicio" aria-label="Olvanta, inicio de la demo">
            <Brand />
          </a>
          <nav className="demo-nav" aria-label="Demo">
            <a href="#reportes">Reportes</a>
            <a href="#facturacion">Facturación</a>
          </nav>
        </div>
      </header>
      <main className="frame content" id="inicio">
        <header className="demo-intro">
          <div>
            <p className="demo-version">Demo de Olvanta 1.0.0</p>
            <h1>Reportes de operación</h1>
            <p>Recorre una operación de prueba, desde la recepción de aceitunas en Tacna hasta la venta, el cierre de caja y la factura simulada.</p>
          </div>
          <img src={photo} alt="" />
        </header>
        <section className="panel fields demo-reports" id="reportes" aria-labelledby="report-title">
          <div className="demo-report-controls">
            <div className="demo-report-choice">
              <label htmlFor="demo-report">Reporte</label>
              <select id="demo-report" value={selected} onChange={(event) => setSelected(event.target.value as ReportKey)}>
                {reports.map((key) => <option key={key} value={key}>{reportData[key].title}</option>)}
              </select>
            </div>
            <button type="button" onClick={() => downloadReport(selected)}>Descargar CSV</button>
          </div>
          <div aria-live="polite">
            <h2 id="report-title">{report.title}</h2>
            <p className="demo-context">{report.scope === 'Corte al consultar' ? 'Corte de la demo' : report.scope} · PEN</p>
          </div>
          <div className="table-wrap" tabIndex={0} role="region" aria-labelledby="report-title">
            <table>
              <thead>
                <tr>{report.columns.map((column) => <th key={column} scope="col">{column}</th>)}</tr>
              </thead>
              <tbody>
                {report.rows.map((row, index) => (
                  <tr key={index}>{row.map((cell, position) => (
                    <td key={position}>{cell || '—'}</td>
                  ))}</tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
        <section className="panel demo-billing" id="facturacion" aria-labelledby="billing-title">
          <h2 id="billing-title">Facturación en simulación</h2>
          <p className="demo-context">Ejemplos de comprobantes procesados con datos ficticios. Ambos incluyen una constancia de recepción simulada.</p>
          <dl className="demo-outcomes">
            <div><dt>Factura FI01-1</dt><dd className="demo-accepted">Aceptada</dd></div>
            <div><dt>Factura FI01-2</dt><dd className="demo-rejected">Rechazada</dd></div>
          </dl>
        </section>
      </main>
      <footer className="frame demo-footer">Datos de prueba · Solo lectura</footer>
    </>
  )
}

createRoot(document.getElementById('root')!).render(<Demo />)
