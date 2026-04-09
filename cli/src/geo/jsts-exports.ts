/**
 * Re-export commonly used JSTS classes so consumers don't need a separate
 * `jsts` dependency or figure out deep import paths.
 */
// IMPORTANT: Importing monkey.js triggers Geometry.prototype patching with
// intersection/union/difference/buffer/etc. Without this, these methods are missing.
// @ts-ignore - jsts has no type definitions
import 'jsts/org/locationtech/jts/monkey.js';

// @ts-ignore
import Coordinate from 'jsts/org/locationtech/jts/geom/Coordinate.js';
// @ts-ignore
import GeometryFactory from 'jsts/org/locationtech/jts/geom/GeometryFactory.js';

export { Coordinate, GeometryFactory };
