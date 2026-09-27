// @ts-check
const eslint = require('@eslint/js');
const { defineConfig } = require('eslint/config');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');

module.exports = defineConfig([
  {
    files: ['**/*.ts'],
    extends: [
      eslint.configs.recommended,
      tseslint.configs.recommended,
      tseslint.configs.stylistic,
      angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      '@angular-eslint/directive-selector': [
        'error',
        {
          type: 'attribute',
          prefix: 'app',
          style: 'camelCase',
        },
      ],
      '@angular-eslint/component-selector': [
        'error',
        {
          type: 'element',
          prefix: 'app',
          style: 'kebab-case',
        },
      ],
      // Every component is OnPush (CLAUDE.md phase 3 conventions).
      '@angular-eslint/prefer-on-push-component-change-detection': 'error',
      '@angular-eslint/prefer-standalone': 'error',
      // Native browser dialogs are banned: they cannot be styled or translated, they block the
      // page, and on a phone they appear at the top of the screen instead of near the thumb.
      // Use ConfirmDialogService.confirm() for questions and NotifyService for messages.
      'no-restricted-globals': [
        'error',
        {
          name: 'confirm',
          message: 'Use ConfirmDialogService.confirm() instead of window.confirm.',
        },
        {
          name: 'alert',
          message: 'Use NotifyService instead of window.alert.',
        },
        {
          name: 'prompt',
          message: 'Ask for input in a dialog component instead of window.prompt.',
        },
      ],
      'no-restricted-properties': [
        'error',
        { object: 'window', property: 'confirm', message: 'Use ConfirmDialogService.confirm().' },
        { object: 'window', property: 'alert', message: 'Use NotifyService.' },
        { object: 'window', property: 'prompt', message: 'Use a dialog component.' },
      ],
      // CLAUDE.md rule 2: features never import HttpClient. Everything goes through ApiClient.
      'no-restricted-imports': [
        'error',
        {
          paths: [
            {
              name: '@angular/common/http',
              importNames: ['HttpClient'],
              message:
                'HttpClient may only be used inside src/app/core/http. Inject ApiClient instead (CLAUDE.md rule 2).',
            },
          ],
        },
      ],
    },
  },
  {
    // The one place HttpClient is allowed.
    files: ['src/app/core/http/**/*.ts'],
    rules: {
      'no-restricted-imports': 'off',
    },
  },
  {
    files: ['**/*.html'],
    extends: [angular.configs.templateRecommended, angular.configs.templateAccessibility],
    rules: {},
  },
]);
