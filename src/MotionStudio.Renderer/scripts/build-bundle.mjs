import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const rootDir = path.resolve(__dirname, '..');
const distSrcDir = path.join(rootDir, 'dist', 'src');
const outFile = path.resolve(rootDir, '..', 'MotionStudio.Web', 'wwwroot', 'renderer', 'motionstudio-renderer.js');

const moduleOrder = [
  'engine/types.js',
  'vendor/konva.js',
  'engine/interpolation.js',
  'engine/interaction.js',
  'engine/playback.js',
  'workbench.js',
  'index.js',
];

function stripModuleSyntax(code) {
  return code
    .replace(/^\s*import\s+[\s\S]*?from\s+['"][^'"]+['"];\s*$/gm, '')
    .replace(/^\s*import\s+['"][^'"]+['"];\s*$/gm, '')
    .replace(/^\s*export\s+\*\s+from\s+['"][^'"]+['"];\s*$/gm, '')
    .replace(/^\s*export\s*\{[\s\S]*?\}\s*(?:from\s+['"][^'"]+['"])?;\s*$/gm, '')
    .replace(/^\s*export\s+default\s+[\w$]+;\s*$/gm, '')
    .replace(/^\s*export\s+(abstract\s+class|class|async\s+function|function|const|let|var)\s+/gm, '$1 ')
    .replace(/^\s*\/\/# sourceMappingURL=.*$/gm, '');
}

const chunks = [];
for (const rel of moduleOrder) {
  const fullPath = path.join(distSrcDir, rel);
  if (!fs.existsSync(fullPath)) {
    throw new Error(`Missing compiled module: ${fullPath}`);
  }
  const raw = fs.readFileSync(fullPath, 'utf8');
  chunks.push(`// --- Module: ${rel} ---\n${stripModuleSyntax(raw)}`);
}

const bundle = `/**
 * MotionStudio.Renderer Browser Bundle (Generated from TypeScript)
 * Architecture: Native HTML5 Canvas 2D + rAF Interpolation + Single-Commit Pointer Transactions
 */
(function () {
  "use strict";
${chunks.join('\n\n')}
})();
`;

// Verify valid classic script syntax before writing
new vm.Script(bundle, { filename: 'motionstudio-renderer.js' });

fs.mkdirSync(path.dirname(outFile), { recursive: true });
fs.writeFileSync(outFile, bundle, 'utf8');
console.log(`Built & syntax-verified browser bundle -> ${outFile} (${Buffer.byteLength(bundle, 'utf8')} bytes)`);
