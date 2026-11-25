import eslint from '@eslint/js'
import tseslint from 'typescript-eslint'
import globals from 'globals'

export default tseslint.config(
  { ignores: ['node_modules/**', 'dist/**', 'dist-demo/**', 'src/generated/**', 'test-results/**', 'playwright-report/**'] },
  eslint.configs.recommended,
  ...tseslint.configs.recommended,
  { languageOptions: { globals: { ...globals.browser, ...globals.node } } },
)
