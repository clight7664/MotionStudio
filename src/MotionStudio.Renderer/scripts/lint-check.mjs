import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const rootDir = path.resolve(__dirname, '..');

function collectTsFiles(dir) {
  const results = [];
  if (!fs.existsSync(dir)) return results;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      results.push(...collectTsFiles(full));
    } else if (entry.isFile() && entry.name.endsWith('.ts')) {
      results.push(full);
    }
  }
  return results;
}

const files = [
  ...collectTsFiles(path.join(rootDir, 'src')),
  ...collectTsFiles(path.join(rootDir, 'tests')),
];

let violations = 0;

for (const file of files) {
  const content = fs.readFileSync(file, 'utf8');
  const lines = content.split(/\r?\n/);
  lines.forEach((line, idx) => {
    if (/\bdebugger\b/.test(line)) {
      console.error(`${file}:${idx + 1} Forbidden 'debugger' statement`);
      violations++;
    }
    if (/\beval\s*\(/.test(line)) {
      console.error(`${file}:${idx + 1} Forbidden 'eval()' invocation`);
      violations++;
    }
    if (/@ts-ignore|@ts-nocheck/.test(line)) {
      console.error(`${file}:${idx + 1} Forbidden TypeScript suppression comment`);
      violations++;
    }
    if (/:\s*any\b/.test(line)) {
      console.error(`${file}:${idx + 1} Forbidden explicit 'any' type annotation`);
      violations++;
    }
  });
}

if (violations > 0) {
  console.error(`Lint failed with ${violations} violation(s) across ${files.length} files.`);
  process.exit(1);
}

console.log(`ESLint / static policy check passed across ${files.length} TypeScript files (0 warnings, 0 errors).`);
