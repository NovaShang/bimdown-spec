/**
 * bimdown-cli library entry — for programmatic use by AI agents and scripts.
 *
 * Usage:
 *   import { readBimDownGeometry, writeBimDownGeometry, svgToJsts, jstsToSvg } from 'bimdown-cli';
 */

export {
  readBimDownGeometry,
  writeBimDownGeometry,
  svgToJsts,
  jstsToSvg,
  type BimDownGeometry,
  type GeometryMap,
} from './geo/index.js';

// Re-export JSTS classes so consumers don't need a separate jsts dependency
export {
  Coordinate,
  GeometryFactory,
} from './geo/jsts-exports.js';

// Low-level SVG parsing utilities (already used internally)
export {
  parseSvgFile,
  extractLineGeometry,
  extractRectGeometry,
  extractPolygonGeometry,
  extractCircleGeometry,
  type SvgElement,
  type SvgFile,
} from './utils/svg.js';
