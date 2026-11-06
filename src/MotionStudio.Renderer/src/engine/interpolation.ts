import type {
  AnimatableProperty,
  CompositionDto,
  EasingType,
  EvaluatedLayerDto,
  KeyframeDto,
  KeyframeTrackDto,
  LayerItemDto,
} from './types.js';

export function solveCubicBezier(
  x1: number,
  y1: number,
  x2: number,
  y2: number,
  x: number,
): number {
  const cx = 3.0 * x1;
  const bx = 3.0 * (x2 - x1) - cx;
  const ax = 1.0 - cx - bx;

  const cy = 3.0 * y1;
  const by = 3.0 * (y2 - y1) - cy;
  const ay = 1.0 - cy - by;

  const sampleCurveX = (t: number): number => ((ax * t + bx) * t + cx) * t;
  const sampleCurveY = (t: number): number => ((ay * t + by) * t + cy) * t;
  const sampleCurveDerivativeX = (t: number): number => (3.0 * ax * t + 2.0 * bx) * t + cx;

  let t2 = x;
  for (let i = 0; i < 8; i++) {
    const xEst = sampleCurveX(t2) - x;
    if (Math.abs(xEst) < 1e-6) {
      return sampleCurveY(t2);
    }
    const d2 = sampleCurveDerivativeX(t2);
    if (Math.abs(d2) < 1e-6) {
      break;
    }
    t2 -= xEst / d2;
  }

  let t0 = 0.0;
  let t1 = 1.0;
  t2 = x;
  while (t0 < t1) {
    const xEst = sampleCurveX(t2);
    if (Math.abs(xEst - x) < 1e-6) {
      return sampleCurveY(t2);
    }
    if (x > xEst) {
      t0 = t2;
    } else {
      t1 = t2;
    }
    t2 = (t1 - t0) * 0.5 + t0;
    if (t1 - t0 < 1e-6) {
      break;
    }
  }

  return sampleCurveY(t2);
}

export function applyEasing(
  t: number,
  easing: EasingType,
  bezier?: KeyframeDto['bezier'],
): number {
  const clamped = Math.max(0.0, Math.min(1.0, t));
  switch (easing) {
    case 'linear':
      return clamped;
    case 'step':
      return clamped >= 1.0 ? 1.0 : 0.0;
    case 'ease-in':
      return solveCubicBezier(0.42, 0.0, 1.0, 1.0, clamped);
    case 'ease-out':
      return solveCubicBezier(0.0, 0.0, 0.58, 1.0, clamped);
    case 'ease-in-out':
      return solveCubicBezier(0.42, 0.0, 0.58, 1.0, clamped);
    case 'cubic-bezier':
      if (Array.isArray(bezier) && bezier.length === 4) {
        return solveCubicBezier(bezier[0], bezier[1], bezier[2], bezier[3], clamped);
      }
      if (bezier && !Array.isArray(bezier)) {
        return solveCubicBezier(bezier.x1, bezier.y1, bezier.x2, bezier.y2, clamped);
      }
      return clamped;
    default:
      return clamped;
  }
}

export function interpolateNumber(start: number, end: number, easedT: number): number {
  return start + (end - start) * easedT;
}

function parseHexChannel(hex: string, offset: number): number {
  const parsed = Number.parseInt(hex.slice(offset, offset + 2), 16);
  return Number.isNaN(parsed) ? 0 : parsed;
}

function toHexByte(v: number): string {
  const clamped = Math.max(0, Math.min(255, Math.round(v)));
  return clamped.toString(16).toUpperCase().padStart(2, '0');
}

export function interpolateColor(startHex: string, endHex: string, easedT: number): string {
  const s = startHex && startHex.length === 7 && startHex.startsWith('#') ? startHex : '#000000';
  const e = endHex && endHex.length === 7 && endHex.startsWith('#') ? endHex : '#000000';

  const r = interpolateNumber(parseHexChannel(s, 1), parseHexChannel(e, 1), easedT);
  const g = interpolateNumber(parseHexChannel(s, 3), parseHexChannel(e, 3), easedT);
  const b = interpolateNumber(parseHexChannel(s, 5), parseHexChannel(e, 5), easedT);

  return `#${toHexByte(r)}${toHexByte(g)}${toHexByte(b)}`;
}

function extractNumber(kf: KeyframeDto, fallback: number): number {
  if (typeof kf.value === 'number') {
    return kf.value;
  }
  if (kf.value && typeof kf.value.number === 'number') {
    return kf.value.number;
  }
  return fallback;
}

function extractColor(kf: KeyframeDto, fallback: string): string {
  if (kf.colorValue && typeof kf.colorValue === 'string') {
    return kf.colorValue;
  }
  if (typeof kf.value === 'object' && kf.value && typeof kf.value.color === 'string') {
    return kf.value.color;
  }
  return fallback;
}

export function evaluateNumberTrack(
  track: KeyframeTrackDto | undefined,
  frame: number,
  fallback: number,
): number {
  if (!track || track.keyframes.length === 0) {
    return fallback;
  }
  const kfs = track.keyframes;
  if (frame <= kfs[0].frameIndex) {
    return extractNumber(kfs[0], fallback);
  }
  const last = kfs[kfs.length - 1];
  if (frame >= last.frameIndex) {
    return extractNumber(last, fallback);
  }

  for (let i = 0; i < kfs.length - 1; i++) {
    const k0 = kfs[i];
    const k1 = kfs[i + 1];
    if (frame >= k0.frameIndex && frame <= k1.frameIndex) {
      const span = k1.frameIndex - k0.frameIndex;
      if (span <= 0) {
        return extractNumber(k1, fallback);
      }
      const rawT = (frame - k0.frameIndex) / span;
      const easedT = applyEasing(rawT, k0.easing, k0.bezier);
      const v0 = extractNumber(k0, fallback);
      const v1 = extractNumber(k1, fallback);
      return interpolateNumber(v0, v1, easedT);
    }
  }
  return fallback;
}

export function evaluateColorTrack(
  track: KeyframeTrackDto | undefined,
  frame: number,
  fallback: string,
): string {
  if (!track || track.keyframes.length === 0) {
    return fallback;
  }
  const kfs = track.keyframes;
  if (frame <= kfs[0].frameIndex) {
    return extractColor(kfs[0], fallback);
  }
  const last = kfs[kfs.length - 1];
  if (frame >= last.frameIndex) {
    return extractColor(last, fallback);
  }

  for (let i = 0; i < kfs.length - 1; i++) {
    const k0 = kfs[i];
    const k1 = kfs[i + 1];
    if (frame >= k0.frameIndex && frame <= k1.frameIndex) {
      const span = k1.frameIndex - k0.frameIndex;
      if (span <= 0) {
        return extractColor(k1, fallback);
      }
      const rawT = (frame - k0.frameIndex) / span;
      const easedT = applyEasing(rawT, k0.easing, k0.bezier);
      const c0 = extractColor(k0, fallback);
      const c1 = extractColor(k1, fallback);
      return interpolateColor(c0, c1, easedT);
    }
  }
  return fallback;
}

export function evaluateLayerAtFrame(
  composition: CompositionDto,
  layer: LayerItemDto,
  frame: number,
): EvaluatedLayerDto {
  const clampedFrame = Math.max(0.0, Math.min(composition.durationFrames, frame));
  const tracks = composition.tracks ?? [];
  const findTrack = (...props: AnimatableProperty[]): KeyframeTrackDto | undefined =>
    tracks.find((t) => t.layerId === layer.id && props.includes(t.property));

  const x = evaluateNumberTrack(findTrack('x', 'transform.x'), clampedFrame, layer.transform.x);
  const y = evaluateNumberTrack(findTrack('y', 'transform.y'), clampedFrame, layer.transform.y);
  const scaleX = evaluateNumberTrack(
    findTrack('scaleX', 'transform.scaleX'),
    clampedFrame,
    layer.transform.scaleX,
  );
  const scaleY = evaluateNumberTrack(
    findTrack('scaleY', 'transform.scaleY'),
    clampedFrame,
    layer.transform.scaleY,
  );
  const rotation = evaluateNumberTrack(
    findTrack('rotation', 'transform.rotation'),
    clampedFrame,
    layer.transform.rotation,
  );
  const baseOpacity = layer.opacity ?? layer.transform.opacity ?? 1.0;
  const opacity = Math.max(
    0.0,
    Math.min(1.0, evaluateNumberTrack(findTrack('opacity'), clampedFrame, baseOpacity)),
  );
  const baseCornerRadius = layer.style?.cornerRadius ?? 16;
  const cornerRadius = Math.max(
    0.0,
    evaluateNumberTrack(findTrack('cornerRadius'), clampedFrame, baseCornerRadius),
  );
  const baseFill = layer.fill ?? layer.style?.fill ?? '#6B9CFF';
  const baseStroke = layer.style?.stroke ?? '#FFFFFF';
  const fill = evaluateColorTrack(findTrack('fill'), clampedFrame, baseFill);
  const stroke = evaluateColorTrack(findTrack('stroke'), clampedFrame, baseStroke);

  const width = layer.geometry?.width ?? layer.style?.width ?? 240;
  const height = layer.geometry?.height ?? layer.style?.height ?? 140;
  const rawKind = (layer.type ?? layer.kind ?? 'rect').toLowerCase();
  const normType =
    rawKind === 'rectangle'
      ? 'rect'
      : rawKind === 'ellipse'
        ? 'ellipse'
        : rawKind === 'text'
          ? 'text'
          : rawKind === 'group'
            ? 'group'
            : rawKind;

  return {
    id: layer.id,
    layerId: layer.id,
    name: layer.name,
    type: normType,
    parentId: layer.parentId ?? null,
    zOrder: layer.zOrder ?? layer.zIndex ?? 0,
    visible: layer.visible,
    locked: layer.locked,
    x,
    y,
    anchorX: layer.transform.anchorX ?? 0.5,
    anchorY: layer.transform.anchorY ?? 0.5,
    scaleX,
    scaleY,
    rotation,
    opacity,
    width,
    height,
    cornerRadius,
    fill,
    stroke,
    strokeWidth: layer.style?.strokeWidth ?? 2,
    pathData: layer.style?.pathData ?? null,
    textContent: layer.textContent ?? layer.style?.textContent ?? null,
    fontSize: layer.fontSize ?? layer.style?.fontSize ?? 32,
  };
}

export function evaluateCompositionAtFrame(
  composition: CompositionDto,
  frame: number,
): EvaluatedLayerDto[] {
  const layers = composition.layers ?? [];
  const ordered = [...layers].sort((a, b) => (a.zOrder ?? a.zIndex ?? 0) - (b.zOrder ?? b.zIndex ?? 0));
  return ordered.map((layer) => evaluateLayerAtFrame(composition, layer, frame));
}
