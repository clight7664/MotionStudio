import {
  Ellipse,
  Group,
  type KonvaNode,
  Layer,
  Path,
  Rect,
  type ShapeConfig,
  Stage,
  Text,
  Transformer,
} from './vendor/konva.js';
import { InteractionController } from './engine/interaction.js';
import { evaluateCompositionAtFrame } from './engine/interpolation.js';
import { PlaybackController } from './engine/playback.js';
import type {
  CompositionDto,
  DotNetObjectReference,
  EvaluatedLayerDto,
  LayerItemDto,
  WorkbenchSnapshotDto,
} from './engine/types.js';

export class WorkbenchRenderer {
  private container: HTMLElement | null = null;
  private stage: Stage | null = null;
  private bgLayer: Layer | null = null;
  private contentLayer: Layer | null = null;
  private overlayLayer: Layer | null = null;
  private transformer: Transformer | null = null;
  private interaction: InteractionController | null = null;
  private playback: PlaybackController | null = null;
  private dotNetRef: DotNetObjectReference | null = null;
  private snapshot: WorkbenchSnapshotDto | null = null;
  private currentFrameIndex = 0;

  private boundPointerDown: ((ev: PointerEvent) => void) | null = null;
  private boundPointerMove: ((ev: PointerEvent) => void) | null = null;
  private boundPointerUp: ((ev: PointerEvent) => void) | null = null;
  private boundKeyDown: ((ev: KeyboardEvent) => void) | null = null;

  private getStageWidth(snap: WorkbenchSnapshotDto): number {
    return snap.composition?.width ?? snap.width ?? 1920;
  }

  private getStageHeight(snap: WorkbenchSnapshotDto): number {
    return snap.composition?.height ?? snap.height ?? 1080;
  }

  private getLayers(snap: WorkbenchSnapshotDto): LayerItemDto[] {
    return snap.layers ?? snap.composition?.layers ?? [];
  }

  private getSelectedLayerId(snap: WorkbenchSnapshotDto): string | null {
    if (snap.selectedLayerId) {
      return snap.selectedLayerId;
    }
    if (snap.selectedLayerIds && snap.selectedLayerIds.length > 0) {
      return snap.selectedLayerIds[0] ?? null;
    }
    return null;
  }

  private buildNormalizedComposition(snap: WorkbenchSnapshotDto): CompositionDto {
    return {
      id: snap.composition?.id ?? snap.compositionId ?? snap.projectId,
      name: snap.composition?.name ?? snap.compositionName ?? snap.name ?? 'Composition',
      width: this.getStageWidth(snap),
      height: this.getStageHeight(snap),
      fps: snap.composition?.fps ?? snap.fps ?? 60,
      durationFrames: snap.composition?.durationFrames ?? snap.durationFrames ?? 240,
      backgroundColor: snap.composition?.backgroundColor ?? snap.backgroundColor ?? '#F7F8FE',
      layers: this.getLayers(snap),
      tracks: snap.tracks ?? snap.composition?.tracks ?? [],
    };
  }

  public init(
    containerId: string,
    dotNetRef: DotNetObjectReference | null,
    initialSnapshot: WorkbenchSnapshotDto,
  ): boolean {
    this.dispose();
    this.dotNetRef = dotNetRef;
    this.snapshot = initialSnapshot;
    this.currentFrameIndex = initialSnapshot.currentFrameIndex ?? 0;

    if (typeof document !== 'undefined') {
      this.container = document.getElementById(containerId);
    }

    this.stage = new Stage({
      container: this.container,
      width: this.getStageWidth(initialSnapshot),
      height: this.getStageHeight(initialSnapshot),
    });

    this.bgLayer = new Layer({ id: 'bg-layer' });
    this.contentLayer = new Layer({ id: 'content-layer' });
    this.overlayLayer = new Layer({ id: 'overlay-layer' });
    this.transformer = new Transformer({ id: 'selection-transformer' });
    this.overlayLayer.add(this.transformer);

    this.stage.add(this.bgLayer, this.contentLayer, this.overlayLayer);

    this.interaction = new InteractionController({
      stage: this.stage,
      transformer: this.transformer,
      dotNetRef: this.dotNetRef,
      getRevision: () => this.snapshot?.revision ?? 1,
      getFrameIndex: () => this.currentFrameIndex,
      getDocumentId: () => this.snapshot?.projectId ?? 'proj_default',
      isLocked: (layerId: string) => {
        if (!this.snapshot) return false;
        const layer = this.getLayers(this.snapshot).find((l) => l.id === layerId);
        return layer?.locked ?? false;
      },
    });

    this.playback = new PlaybackController({
      dotNetRef: this.dotNetRef,
      onRenderFrame: (frameIndex: number) => {
        this.currentFrameIndex = frameIndex;
        this.renderEvaluatedFrame(frameIndex);
      },
    });

    this.attachDomListeners();
    this.loadSnapshot(initialSnapshot);
    return true;
  }

  public loadSnapshot(snapshot: WorkbenchSnapshotDto): void {
    this.snapshot = snapshot;
    if (!this.playback?.getIsPlaying()) {
      this.currentFrameIndex = snapshot.currentFrameIndex ?? 0;
    }

    if (!this.stage || !this.bgLayer || !this.contentLayer || !this.transformer) {
      return;
    }

    const stageW = this.getStageWidth(snapshot);
    const stageH = this.getStageHeight(snapshot);
    if (this.stage.width() !== stageW || this.stage.height() !== stageH) {
      this.stage.setSize(stageW, stageH);
    }

    this.bgLayer.destroyChildren();
    const bgRect = new Rect({
      id: 'composition-bg',
      x: 0,
      y: 0,
      width: stageW,
      height: stageH,
      fill: snapshot.backgroundColor ?? snapshot.composition?.backgroundColor ?? '#F7F8FE',
      listening: false,
    });
    this.bgLayer.add(bgRect);

    const normComp = this.buildNormalizedComposition(snapshot);
    const layersToRender =
      snapshot.evaluatedLayers && snapshot.evaluatedLayers.length > 0
        ? snapshot.evaluatedLayers
        : evaluateCompositionAtFrame(normComp, this.currentFrameIndex);

    this.renderEvaluatedLayers(layersToRender, this.getSelectedLayerId(snapshot));
  }

  public renderEvaluatedFrame(frameIndex: number): void {
    if (!this.snapshot) return;
    const normComp = this.buildNormalizedComposition(this.snapshot);
    const evaluated = evaluateCompositionAtFrame(normComp, frameIndex);
    this.renderEvaluatedLayers(evaluated, this.getSelectedLayerId(this.snapshot));
  }

  private resolveItemId(item: EvaluatedLayerDto): string {
    return item.id ?? item.layerId ?? '';
  }

  private resolveItemKind(item: EvaluatedLayerDto): string {
    if (typeof item.kind === 'number') {
      switch (item.kind) {
        case 0:
          return 'rect';
        case 1:
          return 'ellipse';
        case 2:
          return 'text';
        case 3:
          return 'group';
        default:
          return 'rect';
      }
    }
    const raw = (item.type ?? item.kind ?? 'rect').toString().toLowerCase();
    if (raw === 'rectangle') return 'rect';
    return raw;
  }

  private renderEvaluatedLayers(
    layers: EvaluatedLayerDto[],
    selectedLayerId: string | null,
  ): void {
    if (!this.stage || !this.contentLayer || !this.transformer) return;

    this.contentLayer.destroyChildren();
    let selectedNode: KonvaNode | null = null;

    const ordered = [...layers].sort(
      (a, b) => (a.zOrder ?? a.zIndex ?? 0) - (b.zOrder ?? b.zIndex ?? 0),
    );
    for (const item of ordered) {
      if (!item.visible) continue;
      const node = this.createNodeForLayer(item);
      this.contentLayer.add(node);
      const id = this.resolveItemId(item);
      if (selectedLayerId && id === selectedLayerId) {
        selectedNode = node;
      }
    }

    this.transformer.nodes(selectedNode ? [selectedNode] : []);
    this.stage.batchDraw();
  }

  private createNodeForLayer(item: EvaluatedLayerDto): KonvaNode {
    const id = this.resolveItemId(item);
    const kind = this.resolveItemKind(item);
    const isBackgroundHelper = id === 'layer-bg' || id === 'layer-grid' || item.locked;

    let variant: ShapeConfig['variant'] = 'default';
    if (id === 'layer-bg') {
      variant = 'artboard-bg';
    } else if (id === 'layer-orbital-ring') {
      variant = 'orbital-ring';
    } else if (id === 'layer-headline') {
      variant = 'headline-hero';
    } else if (id === 'layer-accent-shapes') {
      variant = 'accent-sphere';
    }

    const common: ShapeConfig = {
      id,
      name: item.name,
      x: item.x,
      y: item.y,
      width: item.width,
      height: item.height,
      scaleX: item.scaleX,
      scaleY: item.scaleY,
      rotation: item.rotation,
      opacity: item.opacity,
      visible: item.visible,
      listening: !isBackgroundHelper,
      fill: item.fill,
      stroke: item.stroke,
      strokeWidth: item.strokeWidth ?? (id === 'layer-grid' ? 1 : 0),
      cornerRadius: item.cornerRadius ?? (id === 'layer-bg' || id === 'layer-grid' ? 0 : 16),
      variant,
    };

    if (id === 'layer-grid') {
      common.fill = 'rgba(238, 240, 250, 0.08)';
      common.stroke = 'rgba(210, 215, 232, 0.45)';
      common.strokeWidth = 1;
    }

    switch (kind) {
      case 'ellipse':
        return new Ellipse({
          ...common,
          radiusX: item.width / 2,
          radiusY: item.height / 2,
        });
      case 'text':
        return new Text({
          ...common,
          text: item.textContent ?? item.name,
          fontSize: item.fontSize ?? 32,
        });
      case 'path':
        return new Path({
          ...common,
          data: item.pathData ?? 'M 0 0 L 120 0',
        });
      case 'group':
        return new Group(common);
      case 'rect':
      default:
        return new Rect(common);
    }
  }

  public startPlayback(
    startFrame: number,
    fps: number,
    durationFrames: number,
    loop: boolean,
  ): void {
    this.playback?.start({ startFrame, fps, durationFrames, loop });
  }

  public pausePlayback(): number {
    return this.playback?.pause() ?? this.currentFrameIndex;
  }

  public stopPlayback(): number {
    return this.playback?.stop() ?? 0;
  }

  public getStage(): Stage | null {
    return this.stage;
  }

  public getInteraction(): InteractionController | null {
    return this.interaction;
  }

  public getDiagnostics(): {
    initialized: boolean;
    stageWidth: number;
    stageHeight: number;
    drawCount: number;
    renderedNodeCount: number;
    selectedLayerId: string | null;
    currentFrameIndex: number;
    commitCount: number;
    moveCount: number;
  } {
    return {
      initialized: this.stage !== null,
      stageWidth: this.stage?.width() ?? 0,
      stageHeight: this.stage?.height() ?? 0,
      drawCount: this.stage?.getDrawCount() ?? 0,
      renderedNodeCount: this.contentLayer?.getChildren().length ?? 0,
      selectedLayerId: this.transformer?.getAttachedNode()?.id() ?? null,
      currentFrameIndex: this.currentFrameIndex,
      commitCount: this.interaction?.getCommitCount() ?? 0,
      moveCount: this.interaction?.getMoveCount() ?? 0,
    };
  }

  public dispose(): void {
    this.detachDomListeners();
    this.playback?.dispose();
    this.stage?.destroy();
    this.stage = null;
    this.bgLayer = null;
    this.contentLayer = null;
    this.overlayLayer = null;
    this.transformer = null;
    this.interaction = null;
    this.playback = null;
  }

  private toStageCoords(ev: PointerEvent): { x: number; y: number } {
    const canvas = this.stage?.getCanvasElement();
    if (!canvas || !this.stage) {
      return { x: ev.offsetX ?? 0, y: ev.offsetY ?? 0 };
    }
    const rect = canvas.getBoundingClientRect();
    if (rect.width <= 0 || rect.height <= 0) {
      return { x: ev.offsetX ?? 0, y: ev.offsetY ?? 0 };
    }
    const scaleX = this.stage.width() / rect.width;
    const scaleY = this.stage.height() / rect.height;
    return {
      x: (ev.clientX - rect.left) * scaleX,
      y: (ev.clientY - rect.top) * scaleY,
    };
  }

  private attachDomListeners(): void {
    const canvas = this.stage?.getCanvasElement();
    if (!canvas || typeof window === 'undefined') return;

    this.boundPointerDown = (ev: PointerEvent): void => {
      const pos = this.toStageCoords(ev);
      this.interaction?.onPointerDown(pos.x, pos.y);
    };
    this.boundPointerMove = (ev: PointerEvent): void => {
      if (!this.interaction?.getActiveGesture()) return;
      const pos = this.toStageCoords(ev);
      this.interaction.onPointerMove(pos.x, pos.y);
    };
    this.boundPointerUp = (): void => {
      void this.interaction?.onPointerUp();
    };
    this.boundKeyDown = (ev: KeyboardEvent): void => {
      if (ev.key === 'Escape' && this.interaction?.getActiveGesture()) {
        this.interaction.cancelGesture();
      }
    };

    canvas.addEventListener('pointerdown', this.boundPointerDown);
    window.addEventListener('pointermove', this.boundPointerMove);
    window.addEventListener('pointerup', this.boundPointerUp);
    window.addEventListener('keydown', this.boundKeyDown);
  }

  private detachDomListeners(): void {
    const canvas = this.stage?.getCanvasElement();
    if (canvas && this.boundPointerDown) {
      canvas.removeEventListener('pointerdown', this.boundPointerDown);
    }
    if (typeof window !== 'undefined') {
      if (this.boundPointerMove) window.removeEventListener('pointermove', this.boundPointerMove);
      if (this.boundPointerUp) window.removeEventListener('pointerup', this.boundPointerUp);
      if (this.boundKeyDown) window.removeEventListener('keydown', this.boundKeyDown);
    }
    this.boundPointerDown = null;
    this.boundPointerMove = null;
    this.boundPointerUp = null;
    this.boundKeyDown = null;
  }
}
