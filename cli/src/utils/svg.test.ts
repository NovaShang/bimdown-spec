import { describe, it, expect } from 'vitest';
import { join } from 'node:path';
import { parseSvgFile, extractLineGeometry, extractRectGeometry } from './svg.js';

const mergedDir = join(import.meta.dirname, '..', '..', '..', 'sample_data', 'merged');

describe('SVG parser', () => {
  it('parses wall.svg', () => {
    const svg = parseSvgFile(join(mergedDir, 'lv-2', 'wall.svg'));
    expect(svg.hasYFlip).toBe(true);
    expect(svg.elements.length).toBeGreaterThan(0);
    expect(svg.elements[0].tag).toBe('line');
    expect(svg.elements[0].id).toMatch(/^w-/);
  });

  it('parses structure_column.svg with rect elements', () => {
    const svg = parseSvgFile(join(mergedDir, 'lv-2', 'structure_column.svg'));
    expect(svg.elements.length).toBeGreaterThan(0);
    expect(svg.elements[0].tag).toBe('rect');
  });

  it('extracts line geometry', () => {
    const svg = parseSvgFile(join(mergedDir, 'lv-2', 'wall.svg'));
    const wallEl = svg.elements[0];
    const geo = extractLineGeometry(wallEl);
    expect(geo.length).toBeGreaterThan(0);
    expect(geo.start_x).toBeDefined();
  });

  it('extracts rect geometry', () => {
    const svg = parseSvgFile(join(mergedDir, 'lv-2', 'structure_column.svg'));
    const colEl = svg.elements[0];
    const geo = extractRectGeometry(colEl);
    expect(geo.size_x).toBeGreaterThan(0);
    expect(geo.size_y).toBeGreaterThan(0);
  });

  it('no door.svg exists (doors are CSV-only)', () => {
    const { existsSync } = require('node:fs');
    expect(existsSync(join(mergedDir, 'lv-2', 'door.svg'))).toBe(false);
  });
});
