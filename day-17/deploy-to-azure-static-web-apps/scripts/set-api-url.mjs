// Stamps the resolved API base URL into src/environments/environment.production.ts.
//
// Called by deploy.sh before the production build, so the shipped bundle always points
// at whatever the deployment actually resolved rather than at a hostname someone typed
// months ago. Rewrites only the two URL literals; the surrounding comment — which is
// where the reasoning lives — is left alone.
//
// Usage: node scripts/set-api-url.mjs <apiBaseUrl> [quotesBaseUrl]

import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const [apiBaseUrl, quotesBaseUrl = apiBaseUrl] = process.argv.slice(2);

if (!apiBaseUrl) {
  console.error('usage: node scripts/set-api-url.mjs <apiBaseUrl> [quotesBaseUrl]');
  process.exit(2);
}

const file = resolve(
  dirname(fileURLToPath(import.meta.url)),
  '../frontend/src/environments/environment.production.ts'
);

const before = readFileSync(file, 'utf8');
const after = before
  .replace(/(\bapiBaseUrl:\s*\n?\s*)'[^']*'/, `$1'${apiBaseUrl}'`)
  .replace(/(\bquotesBaseUrl:\s*\n?\s*)'[^']*'/, `$1'${quotesBaseUrl}'`);

if (after === before) {
  console.error(`set-api-url: nothing replaced in ${file} — check the file's shape.`);
  process.exit(1);
}

writeFileSync(file, after);
console.log(`set-api-url: apiBaseUrl=${apiBaseUrl} quotesBaseUrl=${quotesBaseUrl}`);
