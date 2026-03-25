import { describe, it, expect } from 'vitest';
import { join } from 'node:path';
import { parseSvgFile, extractLineGeometry, extractRectGeometry } from './svg.js';

const sampleRoot = join(import.meta.dirname, '..', '..', '..', 'sample_data');
const archDir = join(sampleRoot, 'Architecture');
const structDir = join(sampleRoot, 'Structure');

describe('SVG parser', () => {
  it('parses wall.svg', () => {
    const svg = parseSvgFile(join(archDir, 'lv-2', 'wall.svg'));
    expect(svg.hasYFlip).toBe(true);
    expect(svg.elements.length).toBeGreaterThan(0);
    expect(svg.elements[0].tag).toBe('line');
    expect(svg.elements[0].id).toMatch(/^w-/);
  });

  it('parses structure_column.svg with rect elements', () => {
    const svg = parseSvgFile(join(structDir, 'lv-3', 'structure_column.svg'));
    expect(svg.elements.length).toBeGreaterThan(0);
    expect(svg.elements[0].tag).toBe('rect');
  });

  it('extracts line geometry', () => {
    const svg = parseSvgFile(join(archDir, 'lv-2', 'wall.svg'));
    const wallEl = svg.elements[0];
    const geo = extractLineGeometry(wallEl);
    expect(geo.length).toBeGreaterThan(0);
    expect(geo.thickness).toBeGreaterThan(0);
  });

  it('extracts rect geometry', () => {
    const svg = parseSvgFile(join(structDir, 'lv-3', 'structure_column.svg'));
    const colEl = svg.elements[0];
    const geo = extractRectGeometry(colEl);
    expect(geo.size_x).toBeGreaterThan(0);
    expect(geo.size_y).toBeGreaterThan(0);
  });

  it('detects data-host on doors', () => {
    const svg = parseSvgFile(join(archDir, 'lv-2', 'door.svg'));
    for (const el of svg.elements) {
      expect(el.attrs['data-host']).toBeDefined();
    }
  });
});
