import { describe, it, expect } from 'vitest';
import { join } from 'node:path';
import { readCsv } from './csv.js';

const sampleDir = join(import.meta.dirname, '..', '..', '..', 'sample_data', 'rac_advanced');

describe('CSV reader', () => {
  it('reads level.csv with BOM', () => {
    const data = readCsv(join(sampleDir, 'global', 'level.csv'));
    expect(data.headers).toContain('id');
    expect(data.headers).toContain('elevation');
    expect(data.rows.length).toBeGreaterThan(0);
    expect(data.rows[0].id).toMatch(/^lv-/);
  });

  it('reads wall.csv with correct headers', () => {
    const data = readCsv(join(sampleDir, 'lv-3', 'wall.csv'));
    expect(data.headers).toContain('id');
    expect(data.headers).toContain('material');
    expect(data.headers).toContain('thickness');
    expect(data.rows.length).toBeGreaterThan(0);
  });

  it('reads door.csv with position field', () => {
    const data = readCsv(join(sampleDir, 'lv-3', 'door.csv'));
    expect(data.headers).toContain('host_id');
    expect(data.headers).toContain('position');
    expect(data.rows.length).toBeGreaterThan(0);
    // position = distance in meters from host wall start (non-negative float)
    const pos = parseFloat(data.rows[0].position);
    expect(Number.isFinite(pos)).toBe(true);
    expect(pos).toBeGreaterThanOrEqual(0);
  });
});
