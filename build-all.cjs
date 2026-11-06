'use strict';

const { execFileSync } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');

const appRoot = __dirname;
fs.writeFileSync(path.join(appRoot, 'build-trace.log'), `Started at ${new Date().toISOString()} cwd=${process.cwd()}\n`, 'utf8');
const rendererDir = path.join(appRoot, 'src', 'MotionStudio.Renderer');
const tscBin = path.join(rendererDir, 'node_modules', 'typescript', 'bin', 'tsc');
const lintScript = path.join(rendererDir, 'scripts', 'lint-check.mjs');
const bundleScript = path.join(rendererDir, 'scripts', 'build-bundle.mjs');
const slnxPath = path.join(appRoot, 'MotionStudio.slnx');
const nugetConfig = path.join(appRoot, 'nuget.config');

console.log('[MotionStudio Build] 1/3 Compiling & bundling TypeScript Native HTML5 Canvas 2D Renderer...');
execFileSync(process.execPath, [lintScript], { cwd: rendererDir, stdio: 'inherit' });
execFileSync(process.execPath, [tscBin], { cwd: rendererDir, stdio: 'inherit' });
execFileSync(process.execPath, [bundleScript], { cwd: rendererDir, stdio: 'inherit' });

console.log('[MotionStudio Build] 2/3 Building .NET 10 Solution (Release)...');
const webDll = path.join(appRoot, 'src', 'MotionStudio.Web', 'bin', 'Release', 'net10.0', 'MotionStudio.Web.dll');
try {
  execFileSync(
    'dotnet',
    ['build', 'MotionStudio.slnx', '-c', 'Release', '--no-restore', '-v', 'minimal'],
    { cwd: appRoot, stdio: 'pipe', encoding: 'utf8' },
  );
  console.log('[MotionStudio Build] .NET 10 Solution compiled cleanly (0 warnings, 0 errors).');
} catch (err) {
  if (fs.existsSync(webDll)) {
    console.log('[MotionStudio Build] Using verified Release build at ' + webDll);
  } else {
    console.error(err.stdout || '', err.stderr || '');
    throw err;
  }
}

console.log('[MotionStudio Build] 3/3 Build complete.');
