import { describe, it, expect } from 'vitest';
import { join } from 'node:path';
import { readCsv } from './csv.js';

const sampleDir = join(import.meta.dirname, '..', '..', '..', 'sample_data', 'Architecture');

describe('CSV reader', () => {
  it('reads level.csv with BOM', () => {
    const data = readCsv(join(sampleDir, 'global', 'level.csv'));
    expect(data.headers).toContain('id');
    expect(data.headers).toContain('elevation');
    expect(data.rows.length).toBeGreaterThan(0);
    expect(data.rows[0].id).toBe('lv-1');
  });

  it('reads wall.csv with correct headers', () => {
    const data = readCsv(join(sampleDir, 'lv-2', 'wall.csv'));
    expect(data.headers).toContain('id');
    expect(data.headers).toContain('material');
    expect(data.rows.length).toBeGreaterThan(0);
  });

  it('handles quoted CSV values', () => {
    const data = readCsv(join(sampleDir, 'lv-2', 'slab.csv'));
    const row = data.rows.find((r) => r.material?.includes(','));
    expect(row).toBeDefined();
  });
});
