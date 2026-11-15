import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';

import type { Plugin } from 'vite';

/**
 * Writes sw.js into the build. It fills in the list of built files to cache, and a version that
 * changes when any of them changes, so an update replaces the old cache.
 */
export function serviceWorker(source: string): Plugin {
  return {
    name: 'fibotrail-service-worker',
    apply: 'build',
    generateBundle(_options, bundle) {
      const files = Object.keys(bundle)
        .filter((name) => name.startsWith('assets/') && !name.endsWith('.map'))
        .sort()
        .map((name) => `/${name}`);
      const template = readFileSync(source, 'utf8');
      const version = createHash('sha256')
        .update(template)
        .update(files.join('\n'))
        .digest('hex')
        .slice(0, 12);
      this.emitFile({
        type: 'asset',
        fileName: 'sw.js',
        source: template
          .replace('__FIBOTRAIL_VERSION__', version)
          .replace('self.__FIBOTRAIL_PRECACHE__ ?? []', JSON.stringify(files)),
      });
    },
  };
}
