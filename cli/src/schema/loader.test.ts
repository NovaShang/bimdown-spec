import { describe, it, expect } from 'vitest';
import { join } from 'node:path';
import { loadAllSchemas, resolveFields } from './loader.js';
import { buildRegistry, getSpecDir, ID_PREFIXES } from './registry.js';

const specDir = join(import.meta.dirname, '..', '..', '..', 'spec');
const schemaDir = join(specDir, 'csv-schema');

describe('schema loader', () => {
  it('loads all YAML schemas', () => {
    const schemas = loadAllSchemas(schemaDir);
    expect(schemas.size).toBeGreaterThan(20);
    expect(schemas.has('wall')).toBe(true);
    expect(schemas.has('element')).toBe(true);
    expect(schemas.has('door')).toBe(true);
  });

  it('resolves wall fields with inheritance', () => {
    const schemas = loadAllSchemas(schemaDir);
    const fields = resolveFields('wall', schemas);
    const names = fields.map((f) => f.name);

    // From element base
    expect(names).toContain('id');
    expect(names).toContain('number');
    // From line_element
    expect(names).toContain('start_x');
    expect(names).toContain('end_x');
    // From vertical_span
    expect(names).toContain('top_level_id');
    // From materialized
    expect(names).toContain('material');
    // Own field
    expect(names).toContain('thickness');
  });

  it('marks computed fields correctly', () => {
    const schemas = loadAllSchemas(schemaDir);
    const fields = resolveFields('wall', schemas);
    const startX = fields.find((f) => f.name === 'start_x');
    expect(startX?.computed).toBe(true);
    const id = fields.find((f) => f.name === 'id');
    expect(id?.computed).toBe(false);
    expect(id?.required).toBe(true);
  });
});

describe('registry', () => {
  it('builds registry with all concrete tables', () => {
    const registry = buildRegistry(specDir);
    expect(registry.size).toBe(Object.keys(ID_PREFIXES).length);

    for (const tableName of Object.keys(ID_PREFIXES)) {
      expect(registry.has(tableName), `missing table: ${tableName}`).toBe(true);
    }
  });

  it('splits CSV and computed fields for door', () => {
    const registry = buildRegistry(specDir);
    const door = registry.get('door')!;
    expect(door.prefix).toBe('d');
    expect(door.hostType).toBe('wall');

    const csvNames = door.csvFields.map((f) => f.name);
    expect(csvNames).toContain('id');
    expect(csvNames).toContain('width');
    expect(csvNames).toContain('host_id');
    expect(csvNames).not.toContain('start_x'); // computed
    expect(csvNames).not.toContain('location_param'); // computed
  });
});
