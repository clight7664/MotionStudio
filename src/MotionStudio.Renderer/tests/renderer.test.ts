import assert from 'node:assert/strict';
import test from 'node:test';
import {
  applyEasing,
  evaluateCompositionAtFrame,
  InteractionController,
  interpolateColor,
  interpolateNumber,
  Konva,
  type CompositionDto,
  type DotNetObjectReference,
  type PointerCommitPayload,
} from '../src/index.js';

function createSampleComposition(): CompositionDto {
  return {
    id: 'comp_main',
    name: 'Main Comp',
    width: 1280,
    height: 720,
    fps: 30,
    durationFrames: 90,
    backgroundColor: '#0F1115',
    layers: [
      {
        id: 'layer_rect_1',
        name: 'Hero Card',
        type: 'rect',
        parentId: null,
        zOrder: 0,
        visible: true,
        locked: false,
        expanded: true,
        transform: {
          x: 0,
          y: 220,
          anchorX: 0.5,
          anchorY: 0.5,
          scaleX: 1,
          scaleY: 1,
          rotation: 0,
          opacity: 1,
        },
        style: {
          fill: '#4F8CFF',
          stroke: '#FFFFFF',
          strokeWidth: 2,
          width: 360,
          height: 220,
          cornerRadius: 20,
          pathData: null,
          textContent: null,
          fontSize: 32,
        },
      },
    ],
    tracks: [
      {
        id: 'track_x_1',
        layerId: 'layer_rect_1',
        property: 'x',
        keyframes: [
          {
            id: 'kf_1',
            frameIndex: 0,
            value: { kind: 'number', number: 0 },
            easing: 'linear',
          },
          {
            id: 'kf_2',
            frameIndex: 30,
            value: { kind: 'number', number: 300 },
            easing: 'linear',
          },
        ],
      },
    ],
  };
}

void test('TC-002: Linear interpolation at frame 15 between 0 and 300 equals 150', () => {
  const comp = createSampleComposition();
  const eval0 = evaluateCompositionAtFrame(comp, 0)[0];
  const eval15 = evaluateCompositionAtFrame(comp, 15)[0];
  const eval30 = evaluateCompositionAtFrame(comp, 30)[0];

  assert.equal(eval0.x, 0);
  assert.equal(eval15.x, 150);
  assert.equal(eval30.x, 300);
});

void test('Golden vectors: Easing curves and hex color interpolation match C# evaluator', () => {
  const easeInMid = applyEasing(0.5, 'ease-in');
  const easeOutMid = applyEasing(0.5, 'ease-out');
  const easeInOutMid = applyEasing(0.5, 'ease-in-out');
  const stepHold = applyEasing(0.95, 'step');
  const stepEnd = applyEasing(1.0, 'step');

  assert.ok(easeInMid < 0.5, 'ease-in at t=0.5 should be < 0.5');
  assert.ok(easeOutMid > 0.5, 'ease-out at t=0.5 should be > 0.5');
  assert.ok(Math.abs(easeInOutMid - 0.5) < 1e-4, 'ease-in-out at t=0.5 should equal 0.5');
  assert.equal(stepHold, 0.0, 'step hold before end should equal 0');
  assert.equal(stepEnd, 1.0, 'step at t=1.0 should equal 1');
  assert.equal(interpolateNumber(100, 200, 0.25), 125);
  assert.equal(interpolateColor('#000000', '#FFFFFF', 0.5), '#808080');
});

void test('TC-006: 100 pointermove events trigger 0 SignalR calls and 1 pointerup triggers 1 semantic commit', async () => {
  const stage = new Konva.Stage({ width: 1280, height: 720 });
  const layer = new Konva.Layer({ id: 'content' });
  const rect = new Konva.Rect({
    id: 'layer_rect_1',
    name: 'Hero Card',
    x: 100,
    y: 150,
    width: 240,
    height: 160,
    fill: '#4F8CFF',
  });
  const transformer = new Konva.Transformer({ id: 'tr' });
  layer.add(rect, transformer);
  stage.add(layer);

  const calls: Array<{ method: string; args: unknown[] }> = [];
  const mockDotNetRef: DotNetObjectReference = {
    invokeMethodAsync: async <T = unknown>(methodName: string, ...args: unknown[]): Promise<T> => {
      calls.push({ method: methodName, args });
      return undefined as T;
    },
  };

  const interaction = new InteractionController({
    stage,
    transformer,
    dotNetRef: mockDotNetRef,
    getRevision: () => 7,
    getFrameIndex: () => 15,
    isLocked: () => false,
  });

  const selectedId = interaction.onPointerDown(150, 200);
  assert.equal(selectedId, 'layer_rect_1');

  const callsBeforeMove = calls.filter((c) => c.method === 'OnRendererPointerCommitAsync').length;
  assert.equal(callsBeforeMove, 0);

  for (let i = 1; i <= 100; i++) {
    interaction.onPointerMove(150 + i * 2, 200 + i);
  }

  // Zero commit calls during 100 high-frequency pointermove events
  const callsDuringMove = calls.filter((c) => c.method === 'OnRendererPointerCommitAsync').length;
  assert.equal(callsDuringMove, 0);
  assert.equal(interaction.getMoveCount(), 100);
  assert.equal(rect.x(), 300);
  assert.equal(rect.y(), 250);

  const payload = await interaction.onPointerUp();
  assert.ok(payload !== null);

  const commitCalls = calls.filter((c) => c.method === 'OnRendererPointerCommitAsync');
  assert.equal(commitCalls.length, 1);

  const committed = commitCalls[0].args[0] as PointerCommitPayload;
  assert.equal(committed.expectedRevision, 7);
  assert.equal(committed.layerId, 'layer_rect_1');
  assert.equal(committed.frameIndex, 15);
  assert.equal(committed.x, 300);
  assert.equal(committed.y, 250);
});

void test('Escape cancels active drag gesture and restores initial transform without committing', async () => {
  const stage = new Konva.Stage({ width: 1280, height: 720 });
  const layer = new Konva.Layer({ id: 'content' });
  const rect = new Konva.Rect({
    id: 'layer_rect_1',
    x: 120,
    y: 180,
    width: 200,
    height: 120,
  });
  const transformer = new Konva.Transformer({ id: 'tr' });
  layer.add(rect, transformer);
  stage.add(layer);

  let commitCount = 0;
  const interaction = new InteractionController({
    stage,
    transformer,
    dotNetRef: {
      invokeMethodAsync: async <T = unknown>(methodName: string): Promise<T> => {
        if (methodName === 'OnRendererPointerCommitAsync') commitCount++;
        return undefined as T;
      },
    },
    getRevision: () => 3,
    getFrameIndex: () => 0,
    isLocked: () => false,
  });

  interaction.onPointerDown(160, 220);
  interaction.onPointerMove(260, 320);
  assert.equal(rect.x(), 220);
  assert.equal(rect.y(), 280);

  const cancelled = interaction.cancelGesture();
  assert.equal(cancelled, true);
  assert.equal(rect.x(), 120);
  assert.equal(rect.y(), 180);

  const result = await interaction.onPointerUp();
  assert.equal(result, null);
  assert.equal(commitCount, 0);
});
