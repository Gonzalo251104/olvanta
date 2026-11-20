import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, currentSession } from '../../shared/workspace'

const docTypes: Record<string, string> = {
  invoice: 'Factura',
  receipt: 'Boleta',
  credit: 'Nota de crédito',
  debit: 'Nota de débito',
}

export function OrganizationPage() {
  const cache = useQueryClient()
  const session = useQuery({ queryKey: ['session'], queryFn: currentSession })
  const org = useQuery({
    queryKey: ['admin', 'organization'],
    queryFn: async () => (await api.GET('/api/v1/admin/organization')).data!,
  })
  const [selected, setSelected] = useState<number>()
  const branch =
    session.data?.branches.find((b) => Number(b.id) === selected) ?? session.data?.branches[0]
  const save = useMutation({
    mutationFn: async (form: HTMLFormElement) => {
      const values = new FormData(form)
      const field = (key: string) => String(values.get(key) ?? '').trim()
      switch (form.getAttribute('name')) {
        case 'company':
          await api.PUT('/api/v1/admin/organization/company', {
            body: { name: field('name'), taxId: field('taxId') || null },
          })
          break
        case 'defaults':
          await api.PUT('/api/v1/admin/organization/branches/{branchId}/defaults', {
            params: { path: { branchId: Number(branch!.id) } },
            body: {
              currency: field('currency'),
              taxRate: Number(field('taxRate')),
              maxDiscount: Number(field('maxDiscount')),
            },
          })
          break
        case 'site':
          await api.POST('/api/v1/admin/organization/establishments', {
            body: {
              branchId: Number(branch!.id),
              name: field('name'),
              code: field('code'),
              address: field('address'),
            },
          })
          form.reset()
          break
        case 'series':
          await api.POST('/api/v1/admin/organization/series', {
            body: {
              establishmentId: Number(field('site')),
              documentType: field('type'),
              code: field('code').toUpperCase(),
            },
          })
          form.reset()
          break
      }
    },
    onSuccess: async () => {
      await Promise.all([
        cache.invalidateQueries({ queryKey: ['admin'] }),
        cache.invalidateQueries({ queryKey: ['session'] }),
      ])
    },
  })
  const submit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    save.mutate(event.currentTarget)
  }
  if (org.error) return <p role="alert">{org.error.message}</p>
  if (!org.data || !branch) return <p>Cargando organización…</p>
  const sites = org.data.establishments.filter((s) => s.branchId === branch.id)
  return (
    <div className="organization-grid">
      {save.error && <p role="alert">{save.error.message}</p>}
      {save.isSuccess && <p role="status">Configuración guardada.</p>}
      <form className="panel fields" name="company" onSubmit={submit}>
        <h2>Empresa</h2>
        <label>
          Razón social
          <input name="name" defaultValue={org.data.company.name} required maxLength={180} />
        </label>
        <label>
          RUC
          <input
            name="taxId"
            defaultValue={org.data.company.taxId ?? ''}
            pattern="[0-9]{11}"
            maxLength={11}
          />
        </label>
        <button disabled={save.isPending}>Guardar empresa</button>
      </form>
      <form
        key={branch.id + '-defaults'}
        name="defaults"
        className="panel fields branch-settings"
        onSubmit={submit}
      >
        <h2>Valores iniciales del catálogo</h2>
        <label>
          Sucursal a configurar
          <select value={branch.id} onChange={(e) => setSelected(Number(e.target.value))}>
            {session.data?.branches.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Moneda
          <select name="currency" defaultValue={branch.currency}>
            <option>PEN</option>
            <option>USD</option>
          </select>
        </label>
        <label>
          Impuesto (%)
          <input
            name="taxRate"
            type="number"
            required
            min={0}
            max={100}
            step=".01"
            defaultValue={branch.taxRate}
          />
        </label>
        <label>
          Descuento máximo por partida (%)
          <input
            name="maxDiscount"
            type="number"
            min="0"
            max="100"
            step=".01"
            required
            defaultValue={branch.maxDiscount ?? 100}
          />
        </label>
        <button disabled={save.isPending}>Guardar parámetros</button>
      </form>
      <form key={branch.id + '-site'} name="site" className="panel fields" onSubmit={submit}>
        <h2>Establecimientos</h2>
        {sites.map((s) => (
          <div key={s.id}>
            {s.code} · {s.name} · {s.address}
          </div>
        ))}
        <label>
          Código del establecimiento
          <input name="code" required maxLength={20} />
        </label>
        <label>
          Nombre del establecimiento
          <input name="name" required maxLength={100} />
        </label>
        <label>
          Dirección
          <input name="address" required maxLength={300} />
        </label>
        <button disabled={save.isPending}>Crear establecimiento</button>
      </form>
      <form key={branch.id + '-series'} name="series" className="panel fields" onSubmit={submit}>
        <h2>Series de comprobantes</h2>
        {org.data.series
          .filter((s) => sites.some((site) => site.id === s.establishmentId))
          .map((s) => (
            <div key={s.id}>
              {docTypes[s.documentType]} · {s.code} ·{' '}
              {sites.find((site) => site.id === s.establishmentId)?.name}
            </div>
          ))}
        {sites.length ? (
          <>
            <label>
              Establecimiento
              <select name="site">
                {sites.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Tipo de comprobante
              <select name="type">
                {Object.entries(docTypes).map(([key, label]) => (
                  <option key={key} value={key}>
                    {label}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Serie
              <input name="code" pattern="[A-Za-z0-9]{1,12}" required maxLength={12} />
            </label>
            <button disabled={save.isPending}>Registrar serie</button>
          </>
        ) : (
          <p>Cree un establecimiento para asignarle series.</p>
        )}
      </form>
    </div>
  )
}
