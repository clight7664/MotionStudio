/**
 * Vite Library Configuration for MotionStudio.Renderer
 * Produces browser bundle for MotionStudio.Web/wwwroot/renderer/motionstudio-renderer.js
 */
export default {
  build: {
    lib: {
      entry: './src/index.ts',
      name: 'MotionStudioRendererBundle',
      formats: ['iife'],
      fileName: () => 'motionstudio-renderer.js',
    },
    outDir: '../MotionStudio.Web/wwwroot/renderer',
    emptyOutDir: true,
    sourcemap: true,
  },
};
