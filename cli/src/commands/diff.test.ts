import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { existsSync, rmSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { diffProjects } from './diff.js';

const testDirA = join(__dirname, 'test-diff-A');
const testDirB = join(__dirname, 'test-diff-B');

describe('diffProjects', () => {
  beforeEach(() => {
    if (existsSync(testDirA)) rmSync(testDirA, { recursive: true, force: true });
    if (existsSync(testDirB)) rmSync(testDirB, { recursive: true, force: true });
    mkdirSync(join(testDirA, 'global'), { recursive: true });
    mkdirSync(join(testDirB, 'global'), { recursive: true });
  });

  afterEach(() => {
    if (existsSync(testDirA)) rmSync(testDirA, { recursive: true, force: true });
    if (existsSync(testDirB)) rmSync(testDirB, { recursive: true, force: true });
    vi.restoreAllMocks();
  });

  it('detects added, removed, and modified elements', () => {
    // Project A
    writeFileSync(join(testDirA, 'global', 'test.csv'), 'id,val\n1,A\n2,A\n', 'utf-8');
    // Project B: 1 modified, 2 removed, 3 added
    writeFileSync(join(testDirB, 'global', 'test.csv'), 'id,val\n1,B\n3,B\n', 'utf-8');

    // Spy on console.log
    const consoleLogSpy = vi.spyOn(console, 'log').mockImplementation(() => {});

    diffProjects(testDirA, testDirB);

    const logs = consoleLogSpy.mock.calls.map(call => call.join(' ')).join('\n');
    
    expect(logs).toContain('~ 1'); // modified
    expect(logs).toContain('- 2'); // removed
    expect(logs).toContain('+ 3'); // added
  });
});
