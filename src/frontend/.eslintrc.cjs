module.exports = {
  root: true,
  env: { browser: true, es2020: true },
  extends: [
    'eslint:recommended',
    'plugin:@typescript-eslint/recommended-type-checked',
    'plugin:react-hooks/recommended',
    'plugin:react-refresh/recommended',
  ],
  ignorePatterns: ['dist', '.eslintrc.cjs'],
  parser: '@typescript-eslint/parser',
  parserOptions: {
    ecmaVersion: 'latest',
    sourceType: 'module',
    project: ['./tsconfig.json', './tsconfig.node.json'],
    tsconfigRootDir: __dirname,
  },
  plugins: ['react-refresh'],
  rules: {
    // Feature module boundary: only import from a feature's index.ts public API
    'no-restricted-imports': [
      'error',
      {
        patterns: [
          // Block apiSlice direct use in components — use typed hooks instead
          {
            group: ['*/shared/api/apiSlice'],
            message:
              'Do not import apiSlice directly. Use the typed hooks exported from each feature endpoint file.',
          },
          // Block cross-feature internal imports
          {
            group: ['@/features/*/components/*', '@/features/*/hooks/*'],
            message:
              'Cross-feature internal imports are not allowed. Import from the feature index.ts only.',
          },
        ],
      },
    ],
    // Never use raw fetch() — RTK Query only
    'no-restricted-globals': [
      'error',
      {
        name: 'fetch',
        message:
          'Use RTK Query hooks for all API calls. Direct fetch() is not permitted in application code.',
      },
    ],
    '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],
    '@typescript-eslint/consistent-type-imports': ['error', { prefer: 'type-imports' }],
    'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
  },
};
