// ESLint rules shared by every TypeScript package (docs/conventions/typescript.md).
import eslint from '@eslint/js';
import reactHooks from 'eslint-plugin-react-hooks';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  {
    ignores: [
      '**/dist/**',
      '**/dist-tools/**',
      'artifacts/**',
      '**/out/**',
      '**/gen/**',
      '**/node_modules/**',
      'src/dotnet/**',
      'tests/**',
    ],
  },
  eslint.configs.recommended,
  tseslint.configs.strictTypeChecked,
  tseslint.configs.stylisticTypeChecked,
  {
    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    rules: {
      '@typescript-eslint/consistent-type-imports': 'error',
      '@typescript-eslint/naming-convention': [
        'error',
        // Module-level constants holding fixed values: UPPER_SNAKE_CASE.
        {
          selector: 'variable',
          modifiers: ['const', 'global'],
          types: ['boolean', 'number', 'string'],
          format: ['UPPER_CASE'],
        },
        { selector: 'variable', format: ['camelCase', 'PascalCase', 'UPPER_CASE'] },
        { selector: 'function', format: ['camelCase', 'PascalCase'] },
        { selector: 'typeLike', format: ['PascalCase'] },
        {
          selector: 'interface',
          format: ['PascalCase'],
          custom: { regex: '^I[A-Z]', match: false },
        },
      ],
      'no-restricted-syntax': [
        'error',
        {
          selector: 'ExportDefaultDeclaration',
          message: 'Use named exports only (docs/conventions/typescript.md).',
        },
      ],
    },
  },
  {
    files: ['**/*.tsx'],
    extends: [reactHooks.configs.flat['recommended-latest']],
  },
  {
    files: ['**/*.{js,mjs,cjs}'],
    extends: [tseslint.configs.disableTypeChecked],
    languageOptions: { globals: globals.node },
  },
  {
    // Tool configuration files must use a default export.
    files: ['*.config.{js,ts}', '**/*.config.{js,ts}'],
    rules: { 'no-restricted-syntax': 'off' },
  },
);
