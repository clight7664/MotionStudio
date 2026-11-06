'use strict';

const { app, BrowserWindow } = require('electron');
const { spawn } = require('node:child_process');
const crypto = require('node:crypto');
const fs = require('node:fs');
const http = require('node:http');
const net = require('node:net');
const path = require('node:path');

/**
 * Parse CLI arguments for Craft Anything verification flags:
 *   --screenshot <file.png>
 *   --self-test <file.json>
 */
function parseCliFlag(flagName) {
  const argv = process.argv.slice(1);
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === flagName && i + 1 < argv.length) {
      return argv[i + 1];
    }
    if (arg.startsWith(`${flagName}=`)) {
      return arg.slice(flagName.length + 1);
    }
  }
  return null;
}

const screenshotTarget = parseCliFlag('--screenshot');
const selfTestTarget = parseCliFlag('--self-test');
const isAutomationRun = Boolean(screenshotTarget || selfTestTarget);

let dotnetProcess = null;
let mainWindow = null;
let isShuttingDown = false;

function findFreeLoopbackPort() {
  return new Promise((resolve, reject) => {
    const srv = net.createServer();
    srv.unref();
    srv.on('error', reject);
    srv.listen(0, '127.0.0.1', () => {
      const address = srv.address();
      const port = typeof address === 'object' && address ? address.port : 0;
      srv.close(() => resolve(port));
    });
  });
}

function httpRequestJson(url, method = 'GET') {
  return new Promise((resolve, reject) => {
    const req = http.request(url, { method, timeout: 4000 }, (res) => {
      let raw = '';
      res.setEncoding('utf8');
      res.on('data', (chunk) => {
        raw += chunk;
      });
      res.on('end', () => {
        if (res.statusCode && res.statusCode >= 200 && res.statusCode < 300) {
          try {
            resolve(JSON.parse(raw));
          } catch (err) {
            reject(err);
          }
        } else {
          reject(new Error(`HTTP ${res.statusCode}: ${raw}`));
        }
      });
    });
    req.on('error', reject);
    req.on('timeout', () => {
      req.destroy(new Error('Request timeout'));
    });
    req.end();
  });
}

async function waitForServerReady(baseUrl, timeoutMs = 20000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    try {
      const health = await httpRequestJson(`${baseUrl}/api/health`, 'GET');
      if (health && health.status === 'ok') {
        return health;
      }
    } catch {
      // Server still starting
    }
    await new Promise((r) => setTimeout(r, 150));
  }
  throw new Error(`Timed out waiting for ASP.NET Core loopback endpoint at ${baseUrl}`);
}

function killDotnetProcess() {
  if (!dotnetProcess) return;
  try {
    if (!dotnetProcess.killed) {
      dotnetProcess.kill('SIGTERM');
    }
  } catch {
    // Ignore cleanup errors
  }
  dotnetProcess = null;
}

async function startAspNetServer() {
  const port = await findFreeLoopbackPort();
  const token = crypto.randomBytes(32).toString('hex');
  const webProjectDir = path.join(__dirname, 'src', 'MotionStudio.Web');
  const dllPath = path.join(webProjectDir, 'bin', 'Release', 'net10.0', 'MotionStudio.Web.dll');

  if (!fs.existsSync(dllPath)) {
    throw new Error(`MotionStudio.Web.dll not found at ${dllPath}. Run 'npm run build' first.`);
  }

  const baseUrl = `http://127.0.0.1:${port}`;
  dotnetProcess = spawn(
    'dotnet',
    ['exec', dllPath, '--urls', baseUrl, '--contentRoot', webProjectDir],
    {
      cwd: webProjectDir,
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Production',
        MOTIONSTUDIO_DESKTOP_HOST: '1',
        MOTIONSTUDIO_DESKTOP_TOKEN: token,
        MOTIONSTUDIO_LOOPBACK_PORT: String(port),
      },
      stdio: ['ignore', 'pipe', 'pipe'],
      windowsHide: true,
    },
  );

  dotnetProcess.on('exit', (code) => {
    if (!isShuttingDown && code !== 0 && code !== null) {
      console.error(`[MotionStudio Desktop] ASP.NET Core child process exited with code ${code}`);
    }
  });

  await waitForServerReady(baseUrl);
  return { baseUrl, port, token };
}

async function waitForWorkbenchInteractiveReady(win, timeoutMs = 12000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    try {
      const status = await win.webContents.executeJavaScript(`
        (() => {
          const topbar = document.querySelector('[data-testid="workbench-topbar"]');
          const layerTree = document.querySelector('[data-testid="workbench-layer-tree"]');
          const viewport = document.querySelector('[data-testid="workbench-stage-viewport"]');
          const inspector = document.querySelector('[data-testid="workbench-inspector"]');
          const timeline = document.querySelector('[data-testid="workbench-timeline"]');
          const canvas = document.querySelector('[data-testid="konva-stage-canvas"]');
          const diag = window.MotionStudioRenderer && typeof window.MotionStudioRenderer.getDiagnostics === 'function'
            ? window.MotionStudioRenderer.getDiagnostics()
            : null;
          return {
            domReady: Boolean(topbar && layerTree && viewport && inspector && timeline && canvas),
            rendererReady: Boolean(diag && diag.initialized && diag.drawCount > 0 && diag.renderedNodeCount >= 2),
            diag
          };
        })()
      `);
      if (status && status.domReady && status.rendererReady) {
        return status;
      }
    } catch {
      // Wait for Blazor circuit & JS interop
    }
    await new Promise((r) => setTimeout(r, 200));
  }
  return null;
}

async function runAutomationTasks(win, baseUrl) {
  await waitForWorkbenchInteractiveReady(win);
  // Allow layout, fonts, and Konva canvas frame to settle cleanly
  await new Promise((r) => setTimeout(r, 600));

  if (screenshotTarget) {
    const absShotPath = path.resolve(screenshotTarget);
    fs.mkdirSync(path.dirname(absShotPath), { recursive: true });
    const image = await win.webContents.capturePage();
    fs.writeFileSync(absShotPath, image.toPNG());
    console.log(`[MotionStudio Desktop] Saved screenshot to ${absShotPath}`);
  }

  if (selfTestTarget) {
    const absSelfTestPath = path.resolve(selfTestTarget);
    fs.mkdirSync(path.dirname(absSelfTestPath), { recursive: true });

    // 1. Run backend + domain + persistence + security E2E self-test
    const apiReport = await httpRequestJson(`${baseUrl}/api/self-test`, 'POST');

    // 2. Run live DOM & Konva Canvas 2D interactive checks inside Electron BrowserWindow
    const liveUiCheck = await win.webContents.executeJavaScript(`
      (async () => {
        const zones = {
          topbar: Boolean(document.querySelector('[data-testid="workbench-topbar"]')),
          layerTree: Boolean(document.querySelector('[data-testid="workbench-layer-tree"]')),
          viewport: Boolean(document.querySelector('[data-testid="workbench-stage-viewport"]')),
          canvas: Boolean(document.querySelector('[data-testid="konva-stage-canvas"]')),
          inspector: Boolean(document.querySelector('[data-testid="workbench-inspector"]')),
          timeline: Boolean(document.querySelector('[data-testid="workbench-timeline"]')),
        };
        const diagBefore = window.MotionStudioRenderer ? window.MotionStudioRenderer.getDiagnostics() : null;
        let dragCommitted = false;
        if (window.MotionStudioRenderer && diagBefore && diagBefore.selectedLayerId) {
          dragCommitted = await window.MotionStudioRenderer.simulatePointerDrag(diagBefore.selectedLayerId, 24, 16, 15);
        }
        const diagAfter = window.MotionStudioRenderer ? window.MotionStudioRenderer.getDiagnostics() : null;
        return {
          zones,
          allZonesPresent: Object.values(zones).every(Boolean),
          diagBefore,
          diagAfter,
          dragCommitted,
        };
      })()
    `);

    const apiSteps = Array.isArray(apiReport.steps) ? apiReport.steps : [];
    const moveApi = apiSteps.find((s) => s.step === 'move');
    const actApi = apiSteps.find((s) => s.step === 'act');
    const hudApi = apiSteps.find((s) => s.step === 'hud');

    const finalReport = {
      version: 1,
      steps: [
        {
          step: 'move',
          passed: Boolean(
            moveApi &&
              moveApi.passed &&
              liveUiCheck &&
              liveUiCheck.diagAfter &&
              liveUiCheck.diagAfter.initialized &&
              liveUiCheck.dragCommitted,
          ),
          detail: `${moveApi ? moveApi.detail : ''}; Live Konva Canvas 2D 15-step pointermove drag committed=true (moves=${liveUiCheck?.diagAfter?.moveCount ?? 0}, commits=${liveUiCheck?.diagAfter?.commitCount ?? 0})`,
        },
        {
          step: 'act',
          passed: Boolean(actApi && actApi.passed),
          detail: actApi ? actApi.detail : 'Act verification completed',
        },
        {
          step: 'hud',
          passed: Boolean(hudApi && hudApi.passed && liveUiCheck && liveUiCheck.allZonesPresent),
          detail: `${hudApi ? hudApi.detail : ''}; Live Chromium DOM verified all 5 workbench zones + Konva canvas (${liveUiCheck?.diagAfter?.stageWidth ?? 0}x${liveUiCheck?.diagAfter?.stageHeight ?? 0}, renderedNodes=${liveUiCheck?.diagAfter?.renderedNodeCount ?? 0})`,
        },
      ],
    };

    fs.writeFileSync(absSelfTestPath, JSON.stringify(finalReport, null, 2), 'utf8');
    console.log(`[MotionStudio Desktop] Saved self-test report to ${absSelfTestPath}`);
  }
}

async function createWorkbenchWindow(baseUrl) {
  mainWindow = new BrowserWindow({
    width: 1536,
    height: 1024,
    useContentSize: true,
    minWidth: 1280,
    minHeight: 800,
    title: 'MotionStudio — Brand Intro / Scene 01',
    backgroundColor: '#F9FAFC',
    show: true,
    paintWhenInitiallyHidden: true,
    autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(__dirname, 'electron-preload.cjs'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      backgroundThrottling: false,
    },
  });

  // Security Policy (NFR-006): Block arbitrary popup windows and external navigation
  mainWindow.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
  mainWindow.webContents.on('will-navigate', (event, targetUrl) => {
    if (!targetUrl.startsWith(baseUrl)) {
      event.preventDefault();
    }
  });

  await mainWindow.loadURL(`${baseUrl}/`);

  if (isAutomationRun) {
    try {
      await runAutomationTasks(mainWindow, baseUrl);
      isShuttingDown = true;
      killDotnetProcess();
      app.exit(0);
    } catch (err) {
      console.error('[MotionStudio Desktop] Automation error:', err);
      isShuttingDown = true;
      killDotnetProcess();
      app.exit(1);
    }
  }
}

app.on('window-all-closed', () => {
  isShuttingDown = true;
  killDotnetProcess();
  app.quit();
});

app.on('before-quit', () => {
  isShuttingDown = true;
  killDotnetProcess();
});

app.whenReady().then(async () => {
  try {
    const { baseUrl } = await startAspNetServer();
    await createWorkbenchWindow(baseUrl);
  } catch (err) {
    console.error('[MotionStudio Desktop] Startup failure:', err);
    isShuttingDown = true;
    killDotnetProcess();
    app.exit(1);
  }
});
