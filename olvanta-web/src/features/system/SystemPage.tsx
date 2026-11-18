import Button from '@mui/material/Button'
import { useQuery } from '@tanstack/react-query'
import { checkSystem } from '../../shared/api'

export function SystemPage() {
  const query = useQuery({ queryKey: ['system-status'], queryFn: ({ signal }) => checkSystem(signal) })
  const known = query.isSuccess
  const ready = known && query.data.databaseAvailable
  const heading = query.isPending
    ? 'Comprobando servicios'
    : ready
      ? 'Sistema operativo'
      : 'Servicio interrumpido'
  const lastCheck = Math.max(query.dataUpdatedAt, query.errorUpdatedAt)

  return (
    <main className="frame content system-page">
      <div className="section-head">
        <h1>Estado del sistema</h1>
      </div>
      <section
        className="panel status-panel"
        aria-label="Disponibilidad de servicios"
        aria-live="polite"
      >
        <div className="summary">
          <div className="summary-title">
            <span
              aria-hidden="true"
              className={'signal ' + (ready ? 'good' : query.isPending ? '' : 'warn')}
            />
            <h2>{heading}</h2>
          </div>
          <Button variant="outlined" disabled={query.isFetching} onClick={() => void query.refetch()}>
            {query.isFetching ? 'Comprobando…' : 'Volver a comprobar'}
          </Button>
        </div>
        <dl className="services">
          <div>
            <dt>API de Olvanta</dt>
            <dd>{known ? 'En línea' : query.isPending ? 'Consultando…' : 'Sin respuesta válida'}</dd>
          </div>
          <div>
            <dt>
              Base de datos <small>SQL Server</small>
            </dt>
            <dd>{known ? (ready ? 'Conectada' : 'Sin conexión') : 'Sin verificar'}</dd>
          </div>
        </dl>
        <footer>
          Última comprobación{' '}
          <strong>{lastCheck ? new Date(lastCheck).toLocaleTimeString('es-PE') : 'Pendiente'}</strong>
        </footer>
      </section>
    </main>
  )
}
