import type { DotNetObjectReference } from './types.js';

export class PlaybackController {
  private isPlaying = false;
  private isLooping = true;
  private startFrame = 0;
  private currentFrame = 0;
  private fps = 30;
  private durationFrames = 90;
  private startTimestampMs = 0;
  private rafId: number | null = null;
  private onRenderFrame: (frameIndex: number) => void;
  private dotNetRef: DotNetObjectReference | null;

  constructor(options: {
    onRenderFrame: (frameIndex: number) => void;
    dotNetRef: DotNetObjectReference | null;
  }) {
    this.onRenderFrame = options.onRenderFrame;
    this.dotNetRef = options.dotNetRef;
  }

  public getIsPlaying(): boolean {
    return this.isPlaying;
  }

  public getCurrentFrame(): number {
    return this.currentFrame;
  }

  public start(options: {
    startFrame: number;
    fps: number;
    durationFrames: number;
    loop: boolean;
  }): void {
    this.cancelRaf();
    this.isPlaying = true;
    this.startFrame = Math.max(0, options.startFrame);
    this.currentFrame = this.startFrame;
    this.fps = Math.max(1, options.fps);
    this.durationFrames = Math.max(1, options.durationFrames);
    this.isLooping = options.loop;
    this.startTimestampMs = typeof performance !== 'undefined' ? performance.now() : Date.now();

    const tick = (nowMs: number): void => {
      if (!this.isPlaying) {
        return;
      }
      const elapsedSec = Math.max(0, (nowMs - this.startTimestampMs) / 1000);
      const rawFrame = this.startFrame + elapsedSec * this.fps;

      if (rawFrame >= this.durationFrames) {
        if (this.isLooping) {
          this.currentFrame = Math.floor(rawFrame % this.durationFrames);
          this.onRenderFrame(this.currentFrame);
        } else {
          this.currentFrame = this.durationFrames;
          this.onRenderFrame(this.currentFrame);
          this.isPlaying = false;
          this.rafId = null;
          if (this.dotNetRef) {
            void this.dotNetRef.invokeMethodAsync(
              'OnRendererPlaybackCompletedAsync',
              Math.round(this.currentFrame),
            );
          }
          return;
        }
      } else {
        this.currentFrame = Math.floor(rawFrame);
        this.onRenderFrame(this.currentFrame);
      }

      if (typeof requestAnimationFrame === 'function') {
        this.rafId = requestAnimationFrame(tick);
      }
    };

    if (typeof requestAnimationFrame === 'function') {
      this.rafId = requestAnimationFrame(tick);
    } else {
      this.onRenderFrame(this.currentFrame);
    }
  }

  public pause(): number {
    this.isPlaying = false;
    this.cancelRaf();
    return Math.round(this.currentFrame);
  }

  public stop(): number {
    this.isPlaying = false;
    this.cancelRaf();
    this.currentFrame = 0;
    this.onRenderFrame(0);
    return 0;
  }

  public dispose(): void {
    this.isPlaying = false;
    this.cancelRaf();
  }

  private cancelRaf(): void {
    if (this.rafId !== null && typeof cancelAnimationFrame === 'function') {
      cancelAnimationFrame(this.rafId);
      this.rafId = null;
    }
  }
}
