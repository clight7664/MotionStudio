# SECURITY — 安全设计基线

- 维护角色：Security Engineer
- 关联需求：`NFR-006`, `FR-013`, `FR-016`, `FR-017`

## 1. Electron 桌面宿主安全控制

1. **进程与上下文隔离**：`BrowserWindow` 强制配置 `contextIsolation: true`、`nodeIntegration: false`、`sandbox: true`、`enableRemoteModule: false`。渲染进程仅能通过 `electron-preload.cjs` 暴露的只读受限桥接对象 (`window.motionStudioDesktop`) 获取宿主元数据。
2. **Loopback 最小暴露与会话令牌**：桌面宿主启动 `MotionStudio.Web` 时仅绑定 `127.0.0.1` 的随机空闲端口，并生成一次性随机令牌 `MOTIONSTUDIO_DESKTOP_TOKEN`；导航范围严格限制在 `http://127.0.0.1:<port>`，拦截一切外部 `will-navigate` 与 `setWindowOpenHandler` 请求。
3. **进程回收**：窗口关闭 (`window-all-closed` / `before-quit`) 或异常退出时，主进程立即终止子 `.NET` 服务进程，防止后台孤儿进程残留。

## 2. 工程文件与路径安全 (`FR-013`)

1. **沙箱路径校验**：服务器端工程打开/保存接口仅允许在受控工程目录（默认 `examples/` 与应用工程工作区目录）内操作，拒绝含 `..`、盘符越界、UNC 路径或非 `.motion.json` 扩展名的请求，防止路径穿越（Path Traversal）。
2. **JSON 负载与数值校验**：加载 `.motion.json` 时限制最大文件体积（10 MB）、最大嵌套深度（32 层），并校验 `schemaVersion`、`NaN`/`Infinity`、非法枚举与越界帧号。

## 3. SVG 导入与资源净化 (`FR-017`)

1. `SvgSanitizerService` 对所有导入的 SVG 文本执行白名单与黑名单双重检查：
   - 拒绝 `<script>`、`<iframe>`、`<object>`、`<embed>`、`<foreignObject>` 元素；
   - 拒绝任何 `on*` 事件属性（如 `onload`, `onerror`, `onclick`）；
   - 拒绝 `javascript:`、`data:text/html` 以及外部 `http://` / `https://` 引用；
   - 拒绝包含 `<!DOCTYPE` 或 `<!ENTITY` 的 XML 实体扩展（防止 XXE / Billion Laughs 攻击）。
