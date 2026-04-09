import { describe, it, expect } from 'vitest';
import { join } from 'node:path';
import { existsSync } from 'node:fs';
import { parseSvgFile, extractLineGeometry, extractRectGeometry } from './svg.js';

const sampleBase = join(import.meta.dirname, '..', '..', '..', 'sample_data');
const sampleDir = join(sampleBase, 'rac_advanced');

describe('SVG parser', () => {
  it('parses wall.svg with line or path elements', () => {
    const svg = parseSvgFile(join(sampleDir, 'lv-3', 'wall.svg'));
    expect(svg.hasYFlip).toBe(true);
    expect(svg.elements.length).toBeGreaterThan(0);
    expect(['path', 'line']).toContain(svg.elements[0].tag);
    expect(svg.elements[0].id).toMatch(/^w-/);
  });

  it('parses terminal.svg with rect elements', () => {
    const svg = parseSvgFile(join(sampleDir, 'lv-3', 'terminal.svg'));
    expect(svg.elements.length).toBeGreaterThan(0);
    expect(svg.elements[0].tag).toBe('rect');
  });

  it('extracts line geometry from path', () => {
    const svg = parseSvgFile(join(sampleDir, 'lv-3', 'wall.svg'));
    const wallEl = svg.elements[0];
    const geo = extractLineGeometry(wallEl);
    expect(geo.length).toBeGreaterThan(0);
  });

  it('extracts rect geometry', () => {
    const svg = parseSvgFile(join(sampleDir, 'lv-3', 'terminal.svg'));
    const rectEl = svg.elements[0];
    const geo = extractRectGeometry(rectEl);
    expect(geo.size_x).toBeGreaterThan(0);
    expect(geo.size_y).toBeGreaterThan(0);
  });

  it('no door.svg exists (doors are CSV-only)', () => {
    expect(existsSync(join(sampleDir, 'lv-3', 'door.svg'))).toBe(false);
  });
});
