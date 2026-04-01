import { defineConfig } from 'tsup';
import pkg from './package.json' assert { type: 'json' };

export default defineConfig({
  entry: ['src/index.ts'],
  format: ['esm'],
  target: 'node20',
  platform: 'node',
  splitting: false,
  sourcemap: true,
  clean: true,
  banner: { js: '#!/usr/bin/env node' },
  define: {
    'CLI_VERSION': JSON.stringify(pkg.version),
  },
  onSuccess: 'cp -r ../spec dist/spec',
});
