import { defineConfig, mergeConfig, type UserConfig } from 'vite'
import web from './vite.config'

export default defineConfig(() => {
  const value = process.env.OLVANTA_API_ORIGIN
  if (!value) throw new Error('Configure OLVANTA_API_ORIGIN con el origen HTTPS de la API.')
  const url = new URL(value)
  if (url.protocol !== 'https:' || url.href !== url.origin + '/') {
    throw new Error('OLVANTA_API_ORIGIN debe ser un origen HTTPS sin credenciales, ruta ni parámetros.')
  }
  return mergeConfig(web, {
    build: { outDir: 'dist-demo' },
    plugins: [{
      name: 'olvanta-vercel',
      generateBundle() {
        this.emitFile({
          type: 'asset',
          fileName: 'vercel.json',
          source: JSON.stringify({
            routes: [
              ...['api', 'health', 'openapi'].map((prefix) => ({
                src: '/' + prefix + '/(.*)',
                dest: url.origin + '/' + prefix + '/$1',
                headers: { 'Cache-Control': 'private, no-store' },
              })),
              { handle: 'filesystem' },
              { src: '/.*', dest: '/index.html' },
            ],
          }),
        })
      },
    }],
  } satisfies UserConfig)
})
