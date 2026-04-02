import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { existsSync, mkdirSync, rmSync, writeFileSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { computeSpaceBoundaries } from './space-boundary.js';

const testDir = join(__dirname, 'test-space-boundary');

function setupLevel(
  walls: { id: string; x1: number; y1: number; x2: number; y2: number }[],
  spaces: { id: string; x: number; y: number; name?: string }[],
) {
  const levelPath = join(testDir, 'lv-1');
  mkdirSync(levelPath, { recursive: true });

  // Write wall.svg
  if (walls.length > 0) {
    const paths = walls
      .map((w) => `    <path id="${w.id}" d="M ${w.x1},${w.y1} L ${w.x2},${w.y2}" />`)
      .join('\n');
    writeFileSync(
      join(levelPath, 'wall.svg'),
      `<?xml version="1.0" encoding="utf-8"?>
<svg xmlns="http://www.w3.org/2000/svg">
  <g transform="scale(1,-1)">
${paths}
  </g>
</svg>`,
    );

    // Write wall.csv
    const csvRows = walls.map((w) => `${w.id},,0.2,concrete`).join('\n');
    writeFileSync(
      join(levelPath, 'wall.csv'),
      `id,number,thickness,material\n${csvRows}\n`,
    );
  }

  // Write space.csv
  const spaceRows = spaces
    .map((s) => `${s.id},,${s.x},${s.y},${s.name ?? ''}`)
    .join('\n');
  writeFileSync(
    join(levelPath, 'space.csv'),
    `id,number,x,y,name\n${spaceRows}\n`,
  );

  return { name: 'lv-1', path: levelPath };
}

describe('computeSpaceBoundaries', () => {
  beforeEach(() => {
    if (existsSync(testDir)) rmSync(testDir, { recursive: true, force: true });
    mkdirSync(testDir, { recursive: true });
  });

  afterEach(() => {
    if (existsSync(testDir)) rmSync(testDir, { recursive: true, force: true });
  });

  it('computes boundary for a simple rectangular room', () => {
    // 4 walls forming a 10x5 rectangle
    const levelDir = setupLevel(
      [
        { id: 'w-1', x1: 0, y1: 0, x2: 10, y2: 0 },
        { id: 'w-2', x1: 10, y1: 0, x2: 10, y2: 5 },
        { id: 'w-3', x1: 10, y1: 5, x2: 0, y2: 5 },
        { id: 'w-4', x1: 0, y1: 5, x2: 0, y2: 0 },
      ],
      [{ id: 'sp-1', x: 5, y: 2.5, name: 'Room 1' }],
    );

    const result = computeSpaceBoundaries(levelDir);
    expect(result.svgWritten).toBe(true);
    expect(result.warnings.filter((w) => w.includes('no enclosing'))).toHaveLength(0);

    // Verify space.svg was created
    const svgPath = join(levelDir.path, 'space.svg');
    expect(existsSync(svgPath)).toBe(true);

    const svg = readFileSync(svgPath, 'utf-8');
    expect(svg).toContain('id="sp-1"');
    expect(svg).toContain('<polygon');
    expect(svg).toContain('scale(1,-1)');
  });

  it('computes boundaries for two adjacent rooms', () => {
    // Two rooms sharing a wall at x=5
    const levelDir = setupLevel(
      [
        { id: 'w-1', x1: 0, y1: 0, x2: 10, y2: 0 },  // bottom
        { id: 'w-2', x1: 10, y1: 0, x2: 10, y2: 5 },  // right
        { id: 'w-3', x1: 10, y1: 5, x2: 0, y2: 5 },   // top
        { id: 'w-4', x1: 0, y1: 5, x2: 0, y2: 0 },    // left
        { id: 'w-5', x1: 5, y1: 0, x2: 5, y2: 5 },    // middle divider
      ],
      [
        { id: 'sp-1', x: 2.5, y: 2.5, name: 'Room A' },
        { id: 'sp-2', x: 7.5, y: 2.5, name: 'Room B' },
      ],
    );

    const result = computeSpaceBoundaries(levelDir);
    expect(result.svgWritten).toBe(true);
    expect(result.warnings.filter((w) => w.includes('no enclosing'))).toHaveLength(0);

    const svg = readFileSync(join(levelDir.path, 'space.svg'), 'utf-8');
    expect(svg).toContain('id="sp-1"');
    expect(svg).toContain('id="sp-2"');
  });

  it('warns when seed point has no enclosing boundary', () => {
    // Seed point outside the room
    const levelDir = setupLevel(
      [
        { id: 'w-1', x1: 0, y1: 0, x2: 10, y2: 0 },
        { id: 'w-2', x1: 10, y1: 0, x2: 10, y2: 5 },
        { id: 'w-3', x1: 10, y1: 5, x2: 0, y2: 5 },
        { id: 'w-4', x1: 0, y1: 5, x2: 0, y2: 0 },
      ],
      [{ id: 'sp-1', x: 20, y: 20 }],
    );

    const result = computeSpaceBoundaries(levelDir);
    expect(result.warnings.some((w) => w.includes('no enclosing boundary'))).toBe(true);
  });

  it('handles walls with gap (dangling endpoints)', () => {
    // Three walls forming an open L (not closed)
    const levelDir = setupLevel(
      [
        { id: 'w-1', x1: 0, y1: 0, x2: 10, y2: 0 },
        { id: 'w-2', x1: 10, y1: 0, x2: 10, y2: 5 },
        { id: 'w-3', x1: 10, y1: 5, x2: 0, y2: 5 },
        // Missing w-4 (left wall), so room is not closed
      ],
      [{ id: 'sp-1', x: 5, y: 2.5 }],
    );

    const result = computeSpaceBoundaries(levelDir);
    // Should have dangling endpoint warnings
    expect(result.warnings.some((w) => w.includes('no connected line element'))).toBe(true);
  });

  it('merges endpoints within tolerance', () => {
    // Four walls with endpoints slightly off (within 0.01m)
    const levelDir = setupLevel(
      [
        { id: 'w-1', x1: 0, y1: 0, x2: 10, y2: 0 },
        { id: 'w-2', x1: 10.005, y1: 0.003, x2: 10, y2: 5 },  // slightly off
        { id: 'w-3', x1: 10, y1: 5, x2: 0, y2: 5 },
        { id: 'w-4', x1: 0.002, y1: 4.998, x2: 0, y2: 0 },    // slightly off
      ],
      [{ id: 'sp-1', x: 5, y: 2.5 }],
    );

    const result = computeSpaceBoundaries(levelDir);
    expect(result.svgWritten).toBe(true);
    expect(result.warnings.filter((w) => w.includes('no enclosing'))).toHaveLength(0);
  });

  it('is idempotent - running twice produces same result', () => {
    const levelDir = setupLevel(
      [
        { id: 'w-1', x1: 0, y1: 0, x2: 10, y2: 0 },
        { id: 'w-2', x1: 10, y1: 0, x2: 10, y2: 5 },
        { id: 'w-3', x1: 10, y1: 5, x2: 0, y2: 5 },
        { id: 'w-4', x1: 0, y1: 5, x2: 0, y2: 0 },
      ],
      [{ id: 'sp-1', x: 5, y: 2.5 }],
    );

    computeSpaceBoundaries(levelDir);
    const svg1 = readFileSync(join(levelDir.path, 'space.svg'), 'utf-8');

    computeSpaceBoundaries(levelDir);
    const svg2 = readFileSync(join(levelDir.path, 'space.svg'), 'utf-8');

    expect(svg1).toBe(svg2);
  });

  it('returns no artifacts when no space.csv exists', () => {
    const levelPath = join(testDir, 'lv-1');
    mkdirSync(levelPath, { recursive: true });
    const result = computeSpaceBoundaries({ name: 'lv-1', path: levelPath });
    expect(result.svgWritten).toBe(false);
    expect(result.warnings).toHaveLength(0);
  });

  it('warns when no boundary elements exist', () => {
    const levelPath = join(testDir, 'lv-1');
    mkdirSync(levelPath, { recursive: true });
    writeFileSync(
      join(levelPath, 'space.csv'),
      'id,number,x,y,name\nsp-1,,5,2.5,Room\n',
    );
    const result = computeSpaceBoundaries({ name: 'lv-1', path: levelPath });
    expect(result.svgWritten).toBe(false);
    expect(result.warnings.some((w) => w.includes('no boundary elements'))).toBe(true);
  });
});
