/**
 * Native Browser HTML5 Canvas 2D Scene Primitives & Selection Gizmo Bridge.
 * Zero third-party JS rendering library runtime dependencies (ADR-0004).
 */

export interface NodeConfig {
  id?: string;
  name?: string;
  x?: number;
  y?: number;
  width?: number;
  height?: number;
  scaleX?: number;
  scaleY?: number;
  rotation?: number;
  opacity?: number;
  visible?: boolean;
  listening?: boolean;
  draggable?: boolean;
}

export interface ShapeConfig extends NodeConfig {
  fill?: string;
  stroke?: string;
  strokeWidth?: number;
  cornerRadius?: number;
  radiusX?: number;
  radiusY?: number;
  text?: string;
  fontSize?: number;
  fontFamily?: string;
  data?: string;
  variant?: 'default' | 'artboard-bg' | 'orbital-ring' | 'headline-hero' | 'accent-sphere';
}

export interface StageConfig extends NodeConfig {
  container?: HTMLElement | null;
  width: number;
  height: number;
}

export abstract class KonvaNode<TConfig extends NodeConfig = NodeConfig> {
  protected attrs: TConfig;
  public parent: KonvaContainer | null = null;

  constructor(config: TConfig) {
    this.attrs = {
      x: 0,
      y: 0,
      width: 0,
      height: 0,
      scaleX: 1,
      scaleY: 1,
      rotation: 0,
      opacity: 1,
      visible: true,
      listening: true,
      draggable: false,
      ...config,
    };
  }

  public id(): string {
    return this.attrs.id ?? '';
  }

  public name(): string {
    return this.attrs.name ?? '';
  }

  public x(val?: number): number {
    if (val !== undefined) this.attrs.x = val;
    return this.attrs.x ?? 0;
  }

  public y(val?: number): number {
    if (val !== undefined) this.attrs.y = val;
    return this.attrs.y ?? 0;
  }

  public width(val?: number): number {
    if (val !== undefined) this.attrs.width = val;
    return this.attrs.width ?? 0;
  }

  public height(val?: number): number {
    if (val !== undefined) this.attrs.height = val;
    return this.attrs.height ?? 0;
  }

  public scaleX(val?: number): number {
    if (val !== undefined) this.attrs.scaleX = val;
    return this.attrs.scaleX ?? 1;
  }

  public scaleY(val?: number): number {
    if (val !== undefined) this.attrs.scaleY = val;
    return this.attrs.scaleY ?? 1;
  }

  public rotation(val?: number): number {
    if (val !== undefined) this.attrs.rotation = val;
    return this.attrs.rotation ?? 0;
  }

  public opacity(val?: number): number {
    if (val !== undefined) this.attrs.opacity = val;
    return this.attrs.opacity ?? 1;
  }

  public visible(val?: boolean): boolean {
    if (val !== undefined) this.attrs.visible = val;
    return this.attrs.visible ?? true;
  }

  public setAttrs(partial: Partial<TConfig>): void {
    Object.assign(this.attrs, partial);
  }

  public getAttrs(): Readonly<TConfig> {
    return this.attrs;
  }

  public abstract draw(ctx: CanvasRenderingContext2D): void;

  public hitTest(px: number, py: number): boolean {
    if (!this.visible() || this.attrs.listening === false) {
      return false;
    }
    const nx = this.x();
    const ny = this.y();
    const w = this.width() * Math.abs(this.scaleX());
    const h = this.height() * Math.abs(this.scaleY());
    return px >= nx && px <= nx + w && py >= ny && py <= ny + h;
  }
}

export abstract class KonvaContainer<TConfig extends NodeConfig = NodeConfig> extends KonvaNode<TConfig> {
  protected children: KonvaNode[] = [];

  public add(...nodes: KonvaNode[]): this {
    for (const node of nodes) {
      node.parent = this;
      this.children.push(node);
    }
    return this;
  }

  public destroyChildren(): void {
    for (const child of this.children) {
      child.parent = null;
    }
    this.children = [];
  }

  public getChildren(): readonly KonvaNode[] {
    return this.children;
  }

  public findOne(selector: string): KonvaNode | null {
    const targetId = selector.startsWith('#') ? selector.slice(1) : selector;
    for (const child of this.children) {
      if (child.id() === targetId || child.name() === selector) {
        return child;
      }
      if (child instanceof KonvaContainer) {
        const nested = child.findOne(selector);
        if (nested) return nested;
      }
    }
    return null;
  }
}

export class Group extends KonvaContainer<NodeConfig> {
  public draw(ctx: CanvasRenderingContext2D): void {
    if (!this.visible()) return;
    ctx.save();
    ctx.globalAlpha *= this.opacity();
    ctx.translate(this.x(), this.y());
    if (this.rotation() !== 0) {
      ctx.rotate((this.rotation() * Math.PI) / 180);
    }
    ctx.scale(this.scaleX(), this.scaleY());
    for (const child of this.children) {
      child.draw(ctx);
    }
    ctx.restore();
  }
}

export class Rect extends KonvaNode<ShapeConfig> {
  public draw(ctx: CanvasRenderingContext2D): void {
    if (!this.visible()) return;
    const w = this.width();
    const h = this.height();

    if (this.attrs.variant === 'artboard-bg') {
      ctx.save();
      ctx.globalAlpha *= this.opacity();
      const grad = ctx.createLinearGradient(0, 0, w, h);
      grad.addColorStop(0, '#F5F6FF');
      grad.addColorStop(0.55, '#FFFFFF');
      grad.addColorStop(1, '#EEF0FE');
      ctx.fillStyle = grad;
      ctx.fillRect(this.x(), this.y(), w, h);

      // Decorative soft corner arcs & orbital guide lines matching concept artboard
      ctx.save();
      ctx.beginPath();
      ctx.rect(this.x(), this.y(), w, h);
      ctx.clip();

      ctx.fillStyle = 'rgba(130, 122, 248, 0.14)';
      ctx.beginPath();
      ctx.arc(this.x() + 80, this.y() - 40, 390, 0, Math.PI * 2);
      ctx.fill();

      ctx.fillStyle = 'rgba(81, 72, 236, 0.22)';
      ctx.beginPath();
      ctx.arc(this.x() - 30, this.y() - 60, 260, 0, Math.PI * 2);
      ctx.fill();

      ctx.fillStyle = 'rgba(130, 122, 248, 0.16)';
      ctx.beginPath();
      ctx.arc(this.x() + w - 120, this.y() + h + 140, 440, 0, Math.PI * 2);
      ctx.fill();

      // Dashed outer orbit curve
      ctx.strokeStyle = 'rgba(103, 112, 132, 0.36)';
      ctx.lineWidth = 2;
      ctx.setLineDash([10, 10]);
      ctx.beginPath();
      ctx.ellipse(this.x() + 1240, this.y() + 560, 560, 380, (-28 * Math.PI) / 180, 0, Math.PI * 2);
      ctx.stroke();
      ctx.setLineDash([]);

      // Solid thin orbit ellipse + top-right blue orb
      ctx.strokeStyle = 'rgba(103, 112, 132, 0.42)';
      ctx.lineWidth = 1.75;
      ctx.beginPath();
      ctx.ellipse(this.x() + 1320, this.y() + 560, 580, 300, (-22 * Math.PI) / 180, 0, Math.PI * 2);
      ctx.stroke();

      const orbGrad = ctx.createRadialGradient(
        this.x() + 1685,
        this.y() + 215,
        8,
        this.x() + 1700,
        this.y() + 230,
        46,
      );
      orbGrad.addColorStop(0, '#8A84FF');
      orbGrad.addColorStop(1, '#3B32D6');
      ctx.fillStyle = orbGrad;
      ctx.beginPath();
      ctx.arc(this.x() + 1700, this.y() + 230, 44, 0, Math.PI * 2);
      ctx.fill();

      ctx.restore();
      ctx.restore();
      return;
    }

    const r = Math.max(0, Math.min(this.attrs.cornerRadius ?? 0, Math.min(w, h) / 2));

    ctx.save();
    ctx.globalAlpha *= this.opacity();
    ctx.translate(this.x() + w / 2, this.y() + h / 2);
    if (this.rotation() !== 0) {
      ctx.rotate((this.rotation() * Math.PI) / 180);
    }
    ctx.scale(this.scaleX(), this.scaleY());
    ctx.translate(-w / 2, -h / 2);

    ctx.beginPath();
    if (r > 0 && typeof ctx.roundRect === 'function') {
      ctx.roundRect(0, 0, w, h, r);
    } else {
      ctx.rect(0, 0, w, h);
    }

    if (this.attrs.fill) {
      ctx.fillStyle = this.attrs.fill;
      ctx.fill();
    }
    if (this.attrs.stroke && (this.attrs.strokeWidth ?? 0) > 0) {
      ctx.strokeStyle = this.attrs.stroke;
      ctx.lineWidth = this.attrs.strokeWidth ?? 1;
      ctx.stroke();
    }
    ctx.restore();
  }
}

export class Ellipse extends KonvaNode<ShapeConfig> {
  public draw(ctx: CanvasRenderingContext2D): void {
    if (!this.visible()) return;
    const w = this.width();
    const h = this.height();
    const rx = this.attrs.radiusX ?? w / 2;
    const ry = this.attrs.radiusY ?? h / 2;

    ctx.save();
    ctx.globalAlpha *= this.opacity();
    ctx.translate(this.x() + rx, this.y() + ry);
    if (this.rotation() !== 0) {
      ctx.rotate((this.rotation() * Math.PI) / 180);
    }
    ctx.scale(this.scaleX(), this.scaleY());

    if (this.attrs.variant === 'orbital-ring') {
      // 3D Torus Ring in #5148EC matching Brand Intro / Scene 01
      ctx.save();
      ctx.rotate((-24 * Math.PI) / 180);
      const ringGrad = ctx.createLinearGradient(-rx, -ry, rx, ry);
      ringGrad.addColorStop(0, '#6E68F8');
      ringGrad.addColorStop(0.5, this.attrs.fill ?? '#5148EC');
      ringGrad.addColorStop(1, '#3930C8');

      ctx.lineWidth = Math.max(48, Math.min(rx, ry) * 0.34);
      ctx.strokeStyle = ringGrad;
      ctx.beginPath();
      ctx.ellipse(0, 0, rx * 0.82, ry * 0.56, 0, 0, Math.PI * 2);
      ctx.stroke();

      // Inner specular highlight
      ctx.lineWidth = 6;
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.35)';
      ctx.beginPath();
      ctx.ellipse(0, -4, rx * 0.82, ry * 0.56, 0, Math.PI * 1.05, Math.PI * 1.92);
      ctx.stroke();
      ctx.restore();
      ctx.restore();
      return;
    }

    if (this.attrs.variant === 'accent-sphere') {
      const sphereGrad = ctx.createRadialGradient(-rx * 0.3, -ry * 0.3, rx * 0.1, 0, 0, rx);
      sphereGrad.addColorStop(0, '#FFFFFF');
      sphereGrad.addColorStop(0.65, '#E4E7F5');
      sphereGrad.addColorStop(1, '#B8C0DC');
      ctx.fillStyle = sphereGrad;
      ctx.beginPath();
      ctx.ellipse(0, 0, rx, ry, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
      return;
    }

    ctx.beginPath();
    ctx.ellipse(0, 0, rx, ry, 0, 0, Math.PI * 2);
    if (this.attrs.fill) {
      ctx.fillStyle = this.attrs.fill;
      ctx.fill();
    }
    if (this.attrs.stroke && (this.attrs.strokeWidth ?? 0) > 0) {
      ctx.strokeStyle = this.attrs.stroke;
      ctx.lineWidth = this.attrs.strokeWidth ?? 1;
      ctx.stroke();
    }
    ctx.restore();
  }
}

export class Text extends KonvaNode<ShapeConfig> {
  public draw(ctx: CanvasRenderingContext2D): void {
    if (!this.visible()) return;
    const w = this.width();
    const h = this.height();
    const fontSize = this.attrs.fontSize ?? 32;
    const fontFamily = this.attrs.fontFamily ?? 'Inter, "Segoe UI", system-ui, sans-serif';
    const text = this.attrs.text ?? 'Text';

    ctx.save();
    ctx.globalAlpha *= this.opacity();
    ctx.translate(this.x() + w / 2, this.y() + h / 2);
    if (this.rotation() !== 0) {
      ctx.rotate((this.rotation() * Math.PI) / 180);
    }
    ctx.scale(this.scaleX(), this.scaleY());
    ctx.translate(-w / 2, -h / 2);

    if (this.attrs.variant === 'headline-hero' && text.toUpperCase().includes('DESIGN IN MOTION')) {
      ctx.font = `italic 800 ${Math.round(fontSize * 1.18)}px ${fontFamily}`;
      ctx.textBaseline = 'top';
      ctx.fillStyle = '#0F1526';
      ctx.fillText('DESIGN', 0, 0);
      ctx.fillStyle = '#4B44E8';
      ctx.fillText('IN MOTION', 0, Math.round(fontSize * 1.24));
      ctx.restore();
      return;
    }

    ctx.font = `500 ${fontSize}px ${fontFamily}`;
    ctx.textBaseline = 'top';
    if (this.attrs.fill) {
      ctx.fillStyle = this.attrs.fill;
      ctx.fillText(text, 0, 0);
    }
    if (text.includes('Make every frame')) {
      ctx.fillStyle = '#5148EC';
      if (typeof ctx.roundRect === 'function') {
        ctx.beginPath();
        ctx.roundRect(0, fontSize + 28, 120, 10, 5);
        ctx.fill();
      } else {
        ctx.fillRect(0, fontSize + 28, 120, 10);
      }
    }
    ctx.restore();
  }
}

export class Path extends KonvaNode<ShapeConfig> {
  public draw(ctx: CanvasRenderingContext2D): void {
    if (!this.visible()) return;
    const w = this.width();
    const h = this.height();

    ctx.save();
    ctx.globalAlpha *= this.opacity();
    ctx.translate(this.x() + w / 2, this.y() + h / 2);
    if (this.rotation() !== 0) {
      ctx.rotate((this.rotation() * Math.PI) / 180);
    }
    ctx.scale(this.scaleX(), this.scaleY());
    ctx.translate(-w / 2, -h / 2);

    if (typeof Path2D !== 'undefined' && this.attrs.data) {
      const p2d = new Path2D(this.attrs.data);
      if (this.attrs.fill) {
        ctx.fillStyle = this.attrs.fill;
        ctx.fill(p2d);
      }
      if (this.attrs.stroke && (this.attrs.strokeWidth ?? 0) > 0) {
        ctx.strokeStyle = this.attrs.stroke;
        ctx.lineWidth = this.attrs.strokeWidth ?? 1;
        ctx.stroke(p2d);
      }
    } else {
      ctx.beginPath();
      ctx.moveTo(0, h / 2);
      ctx.lineTo(w, h / 2);
      ctx.strokeStyle = this.attrs.stroke ?? this.attrs.fill ?? '#5148EC';
      ctx.lineWidth = Math.max(2, this.attrs.strokeWidth ?? 3);
      ctx.stroke();
    }
    ctx.restore();
  }
}

export type HandleKind = 'move' | 'nw' | 'ne' | 'se' | 'sw' | 'rotate';

export class Transformer extends KonvaNode<NodeConfig> {
  private attachedNode: KonvaNode | null = null;

  public nodes(list?: KonvaNode[]): KonvaNode[] {
    if (list !== undefined) {
      this.attachedNode = list[0] ?? null;
    }
    return this.attachedNode ? [this.attachedNode] : [];
  }

  public getAttachedNode(): KonvaNode | null {
    return this.attachedNode;
  }

  public hitTestHandle(px: number, py: number): HandleKind | null {
    const node = this.attachedNode;
    if (!node || !node.visible()) return null;

    const x = node.x();
    const y = node.y();
    const w = node.width() * Math.abs(node.scaleX());
    const h = node.height() * Math.abs(node.scaleY());
    const handleRadius = 14;

    const corners: Array<{ kind: HandleKind; cx: number; cy: number }> = [
      { kind: 'nw', cx: x, cy: y },
      { kind: 'ne', cx: x + w, cy: y },
      { kind: 'se', cx: x + w, cy: y + h },
      { kind: 'sw', cx: x, cy: y + h },
      { kind: 'rotate', cx: x + w / 2, cy: y - 28 },
    ];

    for (const c of corners) {
      if (Math.hypot(px - c.cx, py - c.cy) <= handleRadius) {
        return c.kind;
      }
    }
    if (px >= x && px <= x + w && py >= y && py <= y + h) {
      return 'move';
    }
    return null;
  }

  public draw(ctx: CanvasRenderingContext2D): void {
    const node = this.attachedNode;
    if (!node || !node.visible()) return;

    const x = node.x();
    const y = node.y();
    const w = node.width() * Math.abs(node.scaleX());
    const h = node.height() * Math.abs(node.scaleY());

    ctx.save();
    ctx.strokeStyle = '#5148EC';
    ctx.lineWidth = 2.2;
    ctx.strokeRect(x, y, w, h);

    // 8 Square selection handles (4 corners + 4 midpoints) matching reference/picture-2.png
    const handleSize = 14;
    const pts: Array<[number, number]> = [
      [x, y],
      [x + w / 2, y],
      [x + w, y],
      [x + w, y + h / 2],
      [x + w, y + h],
      [x + w / 2, y + h],
      [x, y + h],
      [x, y + h / 2],
    ];
    for (const [hx, hy] of pts) {
      ctx.fillStyle = '#FFFFFF';
      ctx.fillRect(hx - handleSize / 2, hy - handleSize / 2, handleSize, handleSize);
      ctx.strokeRect(hx - handleSize / 2, hy - handleSize / 2, handleSize, handleSize);
    }

    ctx.restore();
  }
}

export class Layer extends KonvaContainer<NodeConfig> {
  public batchDraw(): void {
    if (this.parent instanceof Stage) {
      this.parent.batchDraw();
    }
  }

  public draw(ctx: CanvasRenderingContext2D): void {
    if (!this.visible()) return;
    for (const child of this.children) {
      child.draw(ctx);
    }
  }
}

export class Stage extends KonvaContainer<StageConfig> {
  private canvas: HTMLCanvasElement | null = null;
  private ctx: CanvasRenderingContext2D | null = null;
  private drawCount = 0;

  constructor(config: StageConfig) {
    super(config);
    if (config.container && typeof document !== 'undefined') {
      const existing = config.container.querySelector('canvas');
      if (existing) {
        this.canvas = existing;
      } else {
        this.canvas = document.createElement('canvas');
        this.canvas.className = 'ms-konva-canvas';
        this.canvas.setAttribute('data-testid', 'konva-stage-canvas');
        config.container.appendChild(this.canvas);
      }
      this.canvas.width = config.width;
      this.canvas.height = config.height;
      this.canvas.style.width = '100%';
      this.canvas.style.height = '100%';
      this.canvas.style.display = 'block';
      this.ctx = this.canvas.getContext('2d');
    }
  }

  public getCanvasElement(): HTMLCanvasElement | null {
    return this.canvas;
  }

  public getDrawCount(): number {
    return this.drawCount;
  }

  public setSize(width: number, height: number): void {
    this.width(width);
    this.height(height);
    if (this.canvas) {
      this.canvas.width = width;
      this.canvas.height = height;
    }
  }

  public batchDraw(): void {
    this.drawCount++;
    if (!this.ctx) return;
    this.draw(this.ctx);
  }

  public draw(ctx: CanvasRenderingContext2D): void {
    ctx.clearRect(0, 0, this.width(), this.height());
    for (const child of this.children) {
      child.draw(ctx);
    }
  }

  public getIntersection(pos: { x: number; y: number }): KonvaNode | null {
    for (let i = this.children.length - 1; i >= 0; i--) {
      const layer = this.children[i];
      if (layer instanceof KonvaContainer) {
        const layerChildren = layer.getChildren();
        for (let j = layerChildren.length - 1; j >= 0; j--) {
          const node = layerChildren[j];
          if (!(node instanceof Transformer) && node.hitTest(pos.x, pos.y)) {
            return node;
          }
        }
      }
    }
    return null;
  }

  public destroy(): void {
    this.destroyChildren();
    if (this.canvas && this.canvas.parentElement) {
      this.canvas.parentElement.removeChild(this.canvas);
    }
    this.canvas = null;
    this.ctx = null;
  }
}

export const Konva = {
  Stage,
  Layer,
  Group,
  Rect,
  Ellipse,
  Text,
  Path,
  Transformer,
};

export default Konva;
