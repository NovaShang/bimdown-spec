#!/usr/bin/env python3
"""Offset all coordinates in Architecture SVGs by (dx=110, dy=180)."""

import re
import glob

DX = 110
DY = 180

def offset_number(match, offset):
    return str(round(float(match.group()) + offset, 3))

def offset_attr(svg, attr, offset):
    """Offset a named numeric attribute like x1="..." """
    return re.sub(
        rf'({attr}=")([^"]+)(")',
        lambda m: m.group(1) + str(round(float(m.group(2)) + offset, 3)) + m.group(3),
        svg
    )

def offset_points(svg):
    """Offset polygon points="x1,y1 x2,y2 ..." """
    def replace_points(m):
        pairs = m.group(1).strip().split()
        new_pairs = []
        for pair in pairs:
            x, y = pair.split(',')
            nx = round(float(x) + DX, 3)
            ny = round(float(y) + DY, 3)
            new_pairs.append(f"{nx},{ny}")
        return 'points="' + ' '.join(new_pairs) + '"'
    return re.sub(r'points="([^"]+)"', replace_points, svg)

def offset_viewbox(svg):
    """Offset viewBox min-x and min-y."""
    def replace_vb(m):
        parts = m.group(1).split()
        min_x = round(float(parts[0]) + DX, 3)
        min_y = round(float(parts[1]) - DY, 3)
        return f'viewBox="{min_x} {min_y} {parts[2]} {parts[3]}"'
    return re.sub(r'viewBox="([^"]+)"', replace_vb, svg)

files = glob.glob('sample_data/Structure/**/*.svg', recursive=True)
for f in sorted(files):
    with open(f) as fh:
        svg = fh.read()

    svg = offset_attr(svg, 'x1', DX)
    svg = offset_attr(svg, 'x2', DX)
    svg = offset_attr(svg, 'y1', DY)
    svg = offset_attr(svg, 'y2', DY)
    svg = offset_points(svg)
    svg = offset_viewbox(svg)

    with open(f, 'w') as fh:
        fh.write(svg)

    print(f"Updated: {f}")
