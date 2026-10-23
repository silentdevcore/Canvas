import { readPvadEnvironment } from '../../../configuration/environment.mjs';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';

import {
  CATEGORY_ORDER,
  ELEMENT_CATALOG,
  getElementDoc,
  toDesign,
  type ElementDoc,
} from '../../../../pvad-designer/src/docs/elementCatalog.ts';

const MODULE_DIRECTORY = path.dirname(fileURLToPath(import.meta.url));
const PACKAGED_CONTENT_ROOT = path.resolve(MODULE_DIRECTORY, 'resources');
const REPOSITORY_ROOT = path.resolve(MODULE_DIRECTORY, '../../../..');

export const elementCatalog = ELEMENT_CATALOG;
export const categoryOrder = CATEGORY_ORDER;

export function findElement(type: string): ElementDoc | undefined {
  return getElementDoc(type as ElementDoc['type']);
}

export function createExample(element: Record<string, unknown>, name?: string): Record<string, unknown> {
  return toDesign(element, name);
}

export class PvadMcpContentRepository {
  private readonly root: string;

  constructor(contentRoot = readPvadEnvironment('PVAD_MCP_CONTENT_ROOT')) {
    if (contentRoot) {
      this.root = path.resolve(contentRoot);
    } else if (fs.existsSync(path.join(PACKAGED_CONTENT_ROOT, 'docs', 'schema', 'design-export.schema.json'))) {
      this.root = PACKAGED_CONTENT_ROOT;
    } else {
      this.root = REPOSITORY_ROOT;
    }
  }

  read(relativePath: string): string {
    const resolved = path.resolve(this.root, relativePath);
    const relative = path.relative(this.root, resolved);
    if (relative.startsWith('..') || path.isAbsolute(relative)) {
      throw new Error(`Content path escapes the configured root: ${relativePath}`);
    }
    return fs.readFileSync(resolved, 'utf8');
  }

  readJson(relativePath: string): unknown {
    return JSON.parse(this.read(relativePath));
  }
}
