import { WorkbenchRenderer } from './workbench.js';
import type { DotNetObjectReference, WorkbenchSnapshotDto } from './engine/types.js';

export * from './engine/types.js';
export * from './engine/interpolation.js';
export * from './engine/interaction.js';
export * from './engine/playback.js';
export * from './vendor/konva.js';
export * from './workbench.js';

const singleton = new WorkbenchRenderer();
let hasSignalRCircuit = false;
let latestSnapshot: WorkbenchSnapshotDto | null = null;
let localIsPlaying = false;

function formatTimecode(frameIndex: number, fps: number): string {
  const safeFps = Math.max(1, fps);
  const totalSec = Math.floor(frameIndex / safeFps);
  const remFrames = Math.floor(frameIndex % safeFps);
  const mins = Math.floor(totalSec / 60);
  const secs = totalSec % 60;
  return `${String(mins).padStart(2, '0')}:${String(secs).padStart(2, '0')}:${String(remFrames).padStart(2, '0')}`;
}

function syncDomFromSnapshot(snap: WorkbenchSnapshotDto): void {
  latestSnapshot = snap;
  if (typeof document === 'undefined') return;

  const selectedId =
    snap.selectedLayerId ??
    (snap.selectedLayerIds && snap.selectedLayerIds.length > 0 ? snap.selectedLayerIds[0] : null);

  // Highlight selected layer row
  const layerRows = document.querySelectorAll<HTMLElement>('[data-testid^="layer-row-"]');
  layerRows.forEach((row) => {
    const rowId = row.getAttribute('data-testid')?.replace('layer-row-', '') ?? '';
    row.classList.toggle('is-selected', rowId === selectedId);
  });

  // Update playhead & timecode
  const duration = Math.max(1, snap.composition?.durationFrames ?? snap.durationFrames ?? 240);
  const fps = Math.max(1, snap.composition?.fps ?? snap.fps ?? 60);
  const pct = Math.max(0, Math.min(100, (snap.currentFrameIndex / duration) * 100));

  document.querySelectorAll<HTMLElement>('.ms-playhead-marker').forEach((el) => {
    el.style.left = `${pct.toFixed(2)}%`;
  });

  const readout = document.querySelector<HTMLElement>('[data-testid="timeline-readout"]');
  if (readout) {
    readout.textContent = formatTimecode(snap.currentFrameIndex, fps);
  }

  const slider = document.querySelector<HTMLInputElement>('[data-testid="timeline-slider"]');
  if (slider) {
    slider.value = String(snap.currentFrameIndex);
  }

  // Update inspector inputs for selected layer
  if (selectedId && snap.evaluatedLayers) {
    const evalLayer = snap.evaluatedLayers.find((e) => (e.id ?? e.layerId) === selectedId);
    if (evalLayer) {
      const setInputVal = (testId: string, val: string): void => {
        const inp = document.querySelector<HTMLInputElement>(`[data-testid="${testId}"]`);
        if (inp && document.activeElement !== inp) {
          inp.value = val;
        }
      };
      setInputVal('input-prop-x', Math.round(evalLayer.x).toString());
      setInputVal('input-prop-y', Math.round(evalLayer.y).toString());
      setInputVal('input-geom-w', Math.round(evalLayer.width).toString());
      setInputVal('input-geom-h', Math.round(evalLayer.height).toString());
      setInputVal('input-prop-rot', evalLayer.rotation.toFixed(1));
      setInputVal('input-prop-scalex', evalLayer.scaleX.toFixed(2));
      setInputVal('input-prop-fill', evalLayer.fill);
    }
  }
}

async function postWorkspaceAction(body: Record<string, unknown>): Promise<void> {
  if (typeof fetch !== 'function') return;
  try {
    const resp = await fetch('/api/workspace/action', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
    if (!resp.ok) return;
    const data = (await resp.json()) as { snapshot?: WorkbenchSnapshotDto };
    if (data.snapshot) {
      singleton.loadSnapshot(data.snapshot);
      syncDomFromSnapshot(data.snapshot);
    }
  } catch {
    // Ignore transient network errors during window close
  }
}

function createLoopbackDotNetBridge(): DotNetObjectReference {
  return {
    invokeMethodAsync: async <T = unknown>(methodName: string, ...args: unknown[]): Promise<T> => {
      if (methodName === 'OnRendererPlaybackFrameSyncAsync' && typeof args[0] === 'number') {
        if (latestSnapshot) {
          const nextSnap: WorkbenchSnapshotDto = {
            ...latestSnapshot,
            currentFrameIndex: args[0],
          };
          syncDomFromSnapshot(nextSnap);
        }
      }

      if (typeof fetch !== 'function') {
        return undefined as T;
      }

      try {
        const resp = await fetch('/api/workspace/invoke', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ method: methodName, args }),
        });
        if (!resp.ok) {
          return undefined as T;
        }
        const data = (await resp.json()) as { result?: T; snapshot?: WorkbenchSnapshotDto };
        if (data.snapshot) {
          singleton.loadSnapshot(data.snapshot);
          syncDomFromSnapshot(data.snapshot);
        }
        return (data.result ?? data) as T;
      } catch {
        return undefined as T;
      }
    },
  };
}

function bindFallbackInteractiveDom(): void {
  if (typeof document === 'undefined') return;

  document.addEventListener('click', (ev) => {
    if (hasSignalRCircuit) return;
    const target = ev.target as HTMLElement | null;
    if (!target) return;

    const btnVis = target.closest<HTMLElement>('[data-testid^="btn-vis-"]');
    if (btnVis) {
      const layerId = btnVis.getAttribute('data-testid')?.replace('btn-vis-', '') ?? '';
      void postWorkspaceAction({ action: 'toggle-vis', layerId });
      return;
    }

    const btnLock = target.closest<HTMLElement>('[data-testid^="btn-lock-"]');
    if (btnLock) {
      const layerId = btnLock.getAttribute('data-testid')?.replace('btn-lock-', '') ?? '';
      void postWorkspaceAction({ action: 'toggle-lock', layerId });
      return;
    }

    const layerRow = target.closest<HTMLElement>('[data-testid^="layer-row-"]');
    if (layerRow) {
      const layerId = layerRow.getAttribute('data-testid')?.replace('layer-row-', '') ?? '';
      void postWorkspaceAction({ action: 'select-layer', layerId, multi: ev.ctrlKey || ev.shiftKey });
      return;
    }

    if (target.closest('[data-testid="btn-add-rect"]')) {
      void postWorkspaceAction({ action: 'add-shape', kind: 'rectangle' });
      return;
    }
    if (target.closest('[data-testid="btn-add-ellipse"]')) {
      void postWorkspaceAction({ action: 'add-shape', kind: 'ellipse' });
      return;
    }
    if (target.closest('[data-testid="btn-add-text"]')) {
      void postWorkspaceAction({ action: 'add-shape', kind: 'text' });
      return;
    }
    if (target.closest('[data-testid="btn-undo"]')) {
      void postWorkspaceAction({ action: 'undo' });
      return;
    }
    if (target.closest('[data-testid="btn-redo"]')) {
      void postWorkspaceAction({ action: 'redo' });
      return;
    }
    if (target.closest('[data-testid="btn-quick-save"]')) {
      void postWorkspaceAction({ action: 'save' });
      return;
    }

    if (target.closest('[data-testid="btn-play-pause"]') || target.closest('[data-testid="btn-play-top"]')) {
      if (!latestSnapshot) return;
      if (localIsPlaying) {
        localIsPlaying = false;
        singleton.pausePlayback();
      } else {
        localIsPlaying = true;
        singleton.startPlayback(
          latestSnapshot.currentFrameIndex,
          latestSnapshot.composition?.fps ?? latestSnapshot.fps ?? 60,
          latestSnapshot.composition?.durationFrames ?? latestSnapshot.durationFrames ?? 240,
          true,
        );
      }
      return;
    }

    const themeBtn = target.closest<HTMLElement>('[title*="Light / Dark"]');
    if (themeBtn) {
      const root = document.querySelector<HTMLElement>('.ms-workbench');
      if (root) {
        root.setAttribute('data-theme', root.getAttribute('data-theme') === 'dark' ? 'light' : 'dark');
      }
    }
  });

  document.addEventListener('input', (ev) => {
    if (hasSignalRCircuit) return;
    const target = ev.target as HTMLInputElement | null;
    if (!target) return;
    if (target.getAttribute('data-testid') === 'timeline-slider') {
      const frame = Number.parseInt(target.value, 10);
      if (!Number.isNaN(frame)) {
        singleton.renderEvaluatedFrame(frame);
        void postWorkspaceAction({ action: 'seek', frame });
      }
    }
  });

  document.addEventListener('change', (ev) => {
    if (hasSignalRCircuit) return;
    const target = ev.target as HTMLInputElement | null;
    if (!target) return;
    const tid = target.getAttribute('data-testid');
    if (tid === 'input-prop-x') {
      void postWorkspaceAction({ action: 'set-prop', property: 'transform.x', value: Number(target.value) });
    } else if (tid === 'input-prop-y') {
      void postWorkspaceAction({ action: 'set-prop', property: 'transform.y', value: Number(target.value) });
    } else if (tid === 'input-prop-rot') {
      void postWorkspaceAction({ action: 'set-prop', property: 'transform.rotation', value: Number(target.value) });
    } else if (tid === 'input-prop-scalex') {
      void postWorkspaceAction({ action: 'set-prop', property: 'transform.scaleX', value: Number(target.value) });
    } else if (tid === 'input-prop-fill') {
      void postWorkspaceAction({ action: 'set-prop', property: 'fill', value: 0, color: target.value });
    }
  });
}

function autoBootFromDom(): void {
  if (typeof document === 'undefined') return;
  const host = document.getElementById('motion-canvas-host');
  const scriptEl = document.getElementById('ms-initial-snapshot');
  if (!host || !scriptEl || !scriptEl.textContent) return;

  try {
    const parsed = JSON.parse(scriptEl.textContent) as WorkbenchSnapshotDto;
    latestSnapshot = parsed;
    singleton.init('motion-canvas-host', createLoopbackDotNetBridge(), parsed);
    bindFallbackInteractiveDom();
  } catch {
    // Ignore malformed embedded snapshot
  }
}

export interface MotionStudioRendererBridge {
  init: (
    containerId: string,
    dotNetRef: DotNetObjectReference | null,
    initialSnapshot: WorkbenchSnapshotDto,
  ) => boolean;
  loadSnapshot: (snapshot: WorkbenchSnapshotDto) => void;
  startPlayback: (
    startFrame: number,
    fps: number,
    durationFrames: number,
    loop: boolean,
  ) => void;
  pausePlayback: () => number;
  stopPlayback: () => number;
  getDiagnostics: () => ReturnType<WorkbenchRenderer['getDiagnostics']>;
  simulatePointerDrag: (
    layerId: string,
    deltaX: number,
    deltaY: number,
    steps?: number,
  ) => Promise<boolean>;
  dispose: () => void;
}

export const MotionStudioRenderer: MotionStudioRendererBridge = {
  init: (containerId, dotNetRef, initialSnapshot) => {
    if (dotNetRef) {
      hasSignalRCircuit = true;
    }
    latestSnapshot = initialSnapshot;
    return singleton.init(containerId, dotNetRef ?? createLoopbackDotNetBridge(), initialSnapshot);
  },
  loadSnapshot: (snapshot) => {
    latestSnapshot = snapshot;
    singleton.loadSnapshot(snapshot);
  },
  startPlayback: (startFrame, fps, durationFrames, loop) =>
    singleton.startPlayback(startFrame, fps, durationFrames, loop),
  pausePlayback: () => singleton.pausePlayback(),
  stopPlayback: () => singleton.stopPlayback(),
  getDiagnostics: () => singleton.getDiagnostics(),
  simulatePointerDrag: async (layerId, deltaX, deltaY, steps = 20) => {
    const stage = singleton.getStage();
    const interaction = singleton.getInteraction();
    if (!stage || !interaction) return false;
    const node = stage.findOne(`#${layerId}`);
    if (!node) return false;
    const startX = node.x() + node.width() / 2;
    const startY = node.y() + node.height() / 2;
    interaction.onPointerDown(startX, startY);
    for (let i = 1; i <= steps; i++) {
      const ratio = i / steps;
      interaction.onPointerMove(startX + deltaX * ratio, startY + deltaY * ratio);
    }
    const committed = await interaction.onPointerUp();
    return committed !== null;
  },
  dispose: () => singleton.dispose(),
};

if (typeof window !== 'undefined') {
  (window as unknown as { MotionStudioRenderer: MotionStudioRendererBridge }).MotionStudioRenderer =
    MotionStudioRenderer;
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', autoBootFromDom, { once: true });
  } else {
    autoBootFromDom();
  }
}
