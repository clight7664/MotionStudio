export default [
  {
    files: ['src/**/*.ts', 'tests/**/*.ts'],
    rules: {
      'no-debugger': 'error',
      'no-eval': 'error',
      'prefer-const': 'error',
      eqeqeq: ['error', 'always'],
    },
  },
];
