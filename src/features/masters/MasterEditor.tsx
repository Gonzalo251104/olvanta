import { useMutation } from '@tanstack/react-query'
import { api, collection, item } from '../../shared/workspace'
import type { Kind, Models } from '../../shared/workspace'

export function MasterEditor({
  branch,
  kind,
  entry,
  close,
}: {
  branch: Models['BranchAccess']
  kind: Kind
  entry?: Models['MasterView']
  close: (saved: boolean) => void
}) {
  const branchId = Number(branch.id)
  const original = entry?.data
  const party = kind === 'customers' || kind === 'suppliers'
  const save = useMutation({
    mutationFn: async (form: FormData) => {
      const value = (key: string) => String(form.get(key) ?? '').trim()
      const number = (key: string) => Number(value(key))
      const body: Models['MasterInput'] = {
        name: value('name'),
        code: value('code'),
        active: value('active') === 'true',
        version: original?.version,
        ...(party
          ? {
              personType: value('personType'),
              documentType: value('documentType'),
              address: value('address'),
              contact: value('contact'),
              terms: value('terms'),
            }
          : {
              category: value('category'),
              unit: value('unit'),
              price: number('price'),
              currency: value('currency'),
              taxRate: number('taxRate'),
              ...(kind === 'products' && {
                presentation: value('presentation'),
                barcode: value('barcode'),
                conversion: number('conversion'),
              }),
            }),
      }
      if (entry)
        await api.PUT(item, { params: { path: { branchId, kind, id: Number(entry.id) } }, body })
      else await api.POST(collection, { params: { path: { branchId, kind } }, body })
    },
    onSuccess: () => close(true),
  })
  return (
    <form
      className="panel fields"
      onSubmit={(e) => {
        e.preventDefault()
        save.mutate(new FormData(e.currentTarget))
      }}
    >
      <h2>{entry ? 'Editar registro' : 'Nuevo registro'}</h2>
      <label>
        Nombre
        <input name="name" autoFocus required maxLength={180} defaultValue={original?.name} />
      </label>
      <label>
        {party ? 'Número de documento' : 'Código interno'}
        <input name="code" required maxLength={40} defaultValue={original?.code} />
      </label>
      {party ? (
        <>
          <label>
            Clase de persona
            <select name="personType" defaultValue={original?.personType ?? 'person'}>
              <option value="person">Persona natural</option>
              <option value="company">Empresa</option>
            </select>
          </label>
          <label>
            Tipo de documento
            <select name="documentType" defaultValue={original?.documentType ?? 'DNI'}>
              {['DNI', 'RUC', 'CE', 'Pasaporte', 'Otro'].map((x) => (
                <option key={x}>{x}</option>
              ))}
            </select>
          </label>
          <label>
            Dirección
            <input name="address" maxLength={300} defaultValue={original?.address ?? ''} />
          </label>
          <label>
            Contacto
            <input name="contact" maxLength={180} defaultValue={original?.contact ?? ''} />
          </label>
          <label>
            Condiciones comerciales
            <input name="terms" maxLength={300} defaultValue={original?.terms ?? ''} />
          </label>
        </>
      ) : (
        <>
          <label>
            Categoría
            <input name="category" required maxLength={80} defaultValue={original?.category ?? ''} />
          </label>
          <label>
            Unidad
            <input name="unit" required maxLength={40} defaultValue={original?.unit ?? ''} />
          </label>
          <label>
            Precio
            <input
              name="price"
              required
              type="number"
              min="0"
              step=".01"
              defaultValue={original?.price ?? ''}
            />
          </label>
          <label>
            Moneda
            <select name="currency" defaultValue={original?.currency ?? branch.currency}>
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
              min="0"
              max="100"
              step=".01"
              defaultValue={original?.taxRate ?? branch.taxRate}
            />
          </label>
          {kind === 'products' && (
            <>
              <label>
                Presentación
                <input name="presentation" maxLength={80} defaultValue={original?.presentation ?? ''} />
              </label>
              <label>
                Unidades por presentación
                <input
                  required
                  name="conversion"
                  type="number"
                  min=".000001"
                  step=".000001"
                  defaultValue={original?.conversion ?? 1}
                />
              </label>
              <label>
                Código de barras
                <input name="barcode" maxLength={64} defaultValue={original?.barcode ?? ''} />
              </label>
            </>
          )}
        </>
      )}
      <label>
        Estado
        <select name="active" defaultValue={String(original?.active ?? true)}>
          <option value="true">Activo</option>
          <option value="false">Inactivo</option>
        </select>
      </label>
      {save.error && <p role="alert">{save.error.message}</p>}
      <div className="actions">
        <button disabled={save.isPending}>Guardar</button>
        <button className="secondary" type="button" onClick={() => close(false)}>
          Cancelar
        </button>
      </div>
    </form>
  )
}
