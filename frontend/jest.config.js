/* CommonJS on purpose: Node 24 strips types from a .ts config and loads it as ESM, where
   jest-preset-angular's CJS preset has no named export. package.json has no "type": "module",
   so this file is CJS and `require` resolves the preset correctly. */
const { createCjsPreset } = require('jest-preset-angular/presets');

/** @type {import('jest').Config} */
module.exports = {
  ...createCjsPreset(),
  setupFilesAfterEnv: ['<rootDir>/setup-jest.ts'],
  testPathIgnorePatterns: ['<rootDir>/node_modules/', '<rootDir>/dist/'],
  coverageDirectory: '<rootDir>/coverage',
};
