import type { HandleKind, KonvaNode, Stage, Transformer } from '../vendor/konva.js';
import type { DotNetObjectReference, PointerCommitPayload } from './types.js';

export interface ActiveGesture {
  layerId: string;
  handle: HandleKind;
  startPointerX: number;
  startPointerY: number;
  initialX: number;
  initialY: number;
  initialScaleX: number;
  initialScaleY: number;
  initialRotation: number;
  initialWidth: number;
  initialHeight: number;
  hasMoved: boolean;
}

export class InteractionController {
  private stage: Stage;
  private transformer: Transformer;
  private dotNetRef: DotNetObjectReference | null;
  private getRevision: () => number;
  private getFrameIndex: () => number;
  private getDocumentId: () => string;
  private isLocked: (layerId: string) => boolean;
  private activeGesture: ActiveGesture | null = null;
  private commitCounter = 0;
  private moveCounter = 0;

  constructor(options: {
    stage: Stage;
    transformer: Transformer;
    dotNetRef: DotNetObjectReference | null;
    getRevision: () => number;
    getFrameIndex: () => number;
    getDocumentId?: () => string;
    isLocked: (layerId: string) => boolean;
  }) {
    this.stage = options.stage;
    this.transformer = options.transformer;
    this.dotNetRef = options.dotNetRef;
    this.getRevision = options.getRevision;
    this.getFrameIndex = options.getFrameIndex;
    this.getDocumentId = options.getDocumentId ?? (() => 'proj_default');
    this.isLocked = options.isLocked;
  }

  public getCommitCount(): number {
    return this.commitCounter;
  }

  public getMoveCount(): number {
    return this.moveCounter;
  }

  public getActiveGesture(): ActiveGesture | null {
    return this.activeGesture;
  }

  public onPointerDown(stageX: number, stageY: number): string | null {
    const attached = this.transformer.getAttachedNode();
    if (attached) {
      const handle = this.transformer.hitTestHandle(stageX, stageY);
      if (handle && !this.isLocked(attached.id())) {
        this.beginGesture(attached, handle, stageX, stageY);
        return attached.id();
      }
    }

    const hitNode = this.stage.getIntersection({ x: stageX, y: stageY });
    if (!hitNode) {
      this.transformer.nodes([]);
      this.stage.batchDraw();
      return null;
    }

    const layerId = hitNode.id();
    this.transformer.nodes([hitNode]);
    this.stage.batchDraw();

    if (!this.isLocked(layerId)) {
      this.beginGesture(hitNode, 'move', stageX, stageY);
    }

    if (this.dotNetRef) {
      void this.dotNetRef.invokeMethodAsync('OnRendererLayerSelectedAsync', layerId, false);
    }
    return layerId;
  }

  private beginGesture(node: KonvaNode, handle: HandleKind, px: number, py: number): void {
    this.activeGesture = {
      layerId: node.id(),
      handle,
      startPointerX: px,
      startPointerY: py,
      initialX: node.x(),
      initialY: node.y(),
      initialScaleX: node.scaleX(),
      initialScaleY: node.scaleY(),
      initialRotation: node.rotation(),
      initialWidth: Math.max(1, node.width()),
      initialHeight: Math.max(1, node.height()),
      hasMoved: false,
    };
  }

  /**
   * High-frequency pointermove handler.
   * Updates only local Konva scene graph and redraws canvas.
   * ZERO SignalR / Blazor calls are permitted here (Hard Constraint #1 & #2).
   */
  public onPointerMove(stageX: number, stageY: number): boolean {
    if (!this.activeGesture) {
      return false;
    }
    const node = this.stage.findOne(`#${this.activeGesture.layerId}`);
    if (!node) {
      return false;
    }

    this.moveCounter++;
    const dx = stageX - this.activeGesture.startPointerX;
    const dy = stageY - this.activeGesture.startPointerY;
    if (Math.abs(dx) > 0.25 || Math.abs(dy) > 0.25) {
      this.activeGesture.hasMoved = true;
    }

    switch (this.activeGesture.handle) {
      case 'move': {
        node.x(Math.round((this.activeGesture.initialX + dx) * 100) / 100);
        node.y(Math.round((this.activeGesture.initialY + dy) * 100) / 100);
        break;
      }
      case 'se':
      case 'ne':
      case 'nw':
      case 'sw': {
        const signX = this.activeGesture.handle.includes('e') ? 1 : -1;
        const signY = this.activeGesture.handle.includes('s') ? 1 : -1;
        const factorX = Math.max(0.1, (this.activeGesture.initialWidth + signX * dx) / this.activeGesture.initialWidth);
        const factorY = Math.max(0.1, (this.activeGesture.initialHeight + signY * dy) / this.activeGesture.initialHeight);
        node.scaleX(Math.round(this.activeGesture.initialScaleX * factorX * 1000) / 1000);
        node.scaleY(Math.round(this.activeGesture.initialScaleY * factorY * 1000) / 1000);
        break;
      }
      case 'rotate': {
        const cx = this.activeGesture.initialX + (this.activeGesture.initialWidth * this.activeGesture.initialScaleX) / 2;
        const cy = this.activeGesture.initialY + (this.activeGesture.initialHeight * this.activeGesture.initialScaleY) / 2;
        const angleRad = Math.atan2(stageY - cy, stageX - cx);
        const angleDeg = (angleRad * 180) / Math.PI + 90;
        node.rotation(Math.round(angleDeg * 100) / 100);
        break;
      }
    }

    this.stage.batchDraw();
    return true;
  }

  /**
   * Commits a single atomic semantic command to C# on pointerup.
   */
  public async onPointerUp(): Promise<PointerCommitPayload | null> {
    const gesture = this.activeGesture;
    this.activeGesture = null;
    if (!gesture || !gesture.hasMoved) {
      return null;
    }

    const node = this.stage.findOne(`#${gesture.layerId}`);
    if (!node) {
      return null;
    }

    const rev = this.getRevision();
    const payload: PointerCommitPayload = {
      commandId: `cmd_ptr_${Date.now()}_${this.commitCounter + 1}`,
      documentId: this.getDocumentId(),
      baseRevision: rev,
      expectedRevision: rev,
      layerId: gesture.layerId,
      frameIndex: this.getFrameIndex(),
      x: node.x(),
      y: node.y(),
      scaleX: node.scaleX(),
      scaleY: node.scaleY(),
      rotation: node.rotation(),
      autoKeyframe: true,
    };

    this.commitCounter++;
    if (this.dotNetRef) {
      await this.dotNetRef.invokeMethodAsync('OnRendererPointerCommitAsync', payload);
    }
    return payload;
  }

  /**
   * Escape cancels an in-flight pointer gesture and restores initial transform without committing.
   */
  public cancelGesture(): boolean {
    if (!this.activeGesture) {
      return false;
    }
    const node = this.stage.findOne(`#${this.activeGesture.layerId}`);
    if (node) {
      node.x(this.activeGesture.initialX);
      node.y(this.activeGesture.initialY);
      node.scaleX(this.activeGesture.initialScaleX);
      node.scaleY(this.activeGesture.initialScaleY);
      node.rotation(this.activeGesture.initialRotation);
      this.stage.batchDraw();
    }
    this.activeGesture = null;
    return true;
  }
}
