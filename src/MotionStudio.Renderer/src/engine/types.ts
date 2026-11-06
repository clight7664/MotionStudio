export type LayerType = 'group' | 'rect' | 'ellipse' | 'path' | 'text';
export type AnimatableProperty =
  | 'x'
  | 'y'
  | 'scaleX'
  | 'scaleY'
  | 'rotation'
  | 'opacity'
  | 'fill'
  | 'stroke'
  | 'cornerRadius'
  | 'transform.x'
  | 'transform.y'
  | 'transform.rotation'
  | 'transform.scaleX'
  | 'transform.scaleY';
export type EasingType = 'linear' | 'step' | 'ease-in' | 'ease-out' | 'ease-in-out' | 'cubic-bezier';

export interface KeyframeValue {
  kind: 'number' | 'color';
  number?: number | null;
  color?: string | null;
}

export interface KeyframeDto {
  id?: string;
  frameIndex: number;
  value: KeyframeValue | number;
  easing: EasingType;
  colorValue?: string | null;
  bezier?: [number, number, number, number] | { x1: number; y1: number; x2: number; y2: number } | null;
}

export interface KeyframeTrackDto {
  id: string;
  layerId: string;
  property: AnimatableProperty;
  keyframes: KeyframeDto[];
}

export interface TransformStateDto {
  x: number;
  y: number;
  anchorX?: number;
  anchorY?: number;
  scaleX: number;
  scaleY: number;
  rotation: number;
  opacity?: number;
}

export interface ShapeStyleDto {
  fill: string;
  stroke: string;
  strokeWidth: number;
  width: number;
  height: number;
  cornerRadius: number;
  pathData?: string | null;
  textContent?: string | null;
  fontSize: number;
}

export interface LayerItemDto {
  id: string;
  name: string;
  type?: LayerType;
  kind?: string;
  parentId?: string | null;
  zOrder?: number;
  zIndex?: number;
  visible: boolean;
  locked: boolean;
  expanded?: boolean;
  transform: TransformStateDto;
  style?: ShapeStyleDto;
  geometry?: { width: number; height: number };
  opacity?: number;
  fill?: string;
  textContent?: string | null;
  fontSize?: number | null;
}

export interface CompositionDto {
  id?: string;
  name?: string;
  width: number;
  height: number;
  fps: number;
  durationFrames: number;
  backgroundColor?: string;
  layers?: LayerItemDto[];
  tracks?: KeyframeTrackDto[];
}

export interface EvaluatedLayerDto {
  id?: string;
  layerId?: string;
  name: string;
  type?: string;
  kind?: string | number;
  parentId?: string | null;
  zOrder?: number;
  zIndex?: number;
  visible: boolean;
  locked: boolean;
  x: number;
  y: number;
  anchorX?: number;
  anchorY?: number;
  scaleX: number;
  scaleY: number;
  rotation: number;
  opacity: number;
  width: number;
  height: number;
  cornerRadius?: number;
  fill: string;
  stroke?: string;
  strokeWidth?: number;
  pathData?: string | null;
  textContent?: string | null;
  fontSize?: number | null;
}

export interface WorkbenchSnapshotDto {
  schemaVersion?: number;
  projectId: string;
  projectName?: string;
  name?: string;
  revision: number;
  isDirty?: boolean;
  isPlaying?: boolean;
  isLooping?: boolean;
  canvasZoom?: number;
  canvasPanX?: number;
  canvasPanY?: number;
  showGrid?: boolean;
  showCheckerboard?: boolean;
  compositionId?: string;
  compositionName?: string;
  width?: number;
  height?: number;
  fps?: number;
  durationFrames?: number;
  backgroundColor?: string;
  currentFrameIndex: number;
  selectedLayerId?: string | null;
  selectedLayerIds?: string[];
  activeTool?: string;
  composition: CompositionDto;
  layers?: LayerItemDto[];
  tracks?: KeyframeTrackDto[];
  evaluatedLayers: EvaluatedLayerDto[];
}

export interface PointerCommitPayload {
  commandId: string;
  documentId: string;
  baseRevision: number;
  expectedRevision: number;
  layerId: string;
  frameIndex: number;
  x: number;
  y: number;
  scaleX: number;
  scaleY: number;
  rotation: number;
  autoKeyframe: boolean;
}

export interface DotNetObjectReference {
  invokeMethodAsync<T = unknown>(methodName: string, ...args: unknown[]): Promise<T>;
}
