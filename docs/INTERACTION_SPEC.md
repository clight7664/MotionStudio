# INTERACTION_SPEC — 动画编辑工作台交互规范

- **文档版本**：`0.2.0`
- **维护角色**：UX/UI Designer / QA Engineer

## 1. 核心交互矩阵

| 交互场景 | 触发方式 | 系统精确响应 | 边界条件与异常恢复 |
|---|---|---|---|
| **新建工程 (`FR-001`)** | 顶栏 `New` 或 `Ctrl+N` | 弹出新建配置栏（宽高、FPS、总帧数），确认后初始化新文档并同步 Canvas 与 Timeline | 若当前工程 `IsDirty=true`，需显式确认覆盖；非法数值阻止提交并高亮字段 |
| **创建图元 (`FR-003`)** | 顶栏 `+Rect` / `+Ellipse` / `+Text` | 在画布中心附近创建新图层并自动选中，图层树置顶，Inspector 聚焦该图层属性 | 自动分配递增名称（如 `Rectangle 1`）与唯一 `layerId` |
| **画布拖拽 (`FR-004`, `NFR-005`)** | Canvas `pointerdown` -> `pointermove` -> `pointerup` | `pointerdown` 记录起始矩阵与 `baseRevision`；`pointermove` 仅更新 Konva 本地预览并重绘；`pointerup` 提交单条 `MoveLayerTransform` 命令 | 拖拽途中按 `Escape` 立即取消预览并还原到起始位置；锁定图层禁止拖拽 |
| **图层管理 (`FR-005`)** | 图层树点击 Eye / Lock / Up / Down / Group | 隐藏图层立即从 Canvas 消失且不响应点击；锁定图层在 Canvas 只读不可拖动；Group 将选中图层归入组节点 | 空选择时禁用 Group / Delete 按钮 |
| **属性与多选 (`FR-006`)** | Inspector 修改数值或颜色 | 提交 `SetProperty` 命令，若当前属性在该帧已有关键帧则同步更新关键帧值；多选图层属性值不同时显示 `<Mixed>` | 拒绝 `NaN`/`Infinity`，透明度自动限制在 `[0, 1]`，尺寸限制 `>= 1` |
| **时间轴与播放头 (`FR-007`)** | 点击/拖拽帧标尺或输入帧号 | 更新 `CurrentFrameIndex`，触发 Domain 与 Renderer 在目标帧插值采样，不产生 Undo 记录 | 帧索引自动钳制在 `[0, durationFrames - 1]` |
| **关键帧编辑 (`FR-008`, `FR-020`)** | 点击属性旁 `◆` 或时间轴 `+Keyframe` / 拖动关键帧 / 删除 | 在当前帧插入或更新唯一关键帧；支持修改 Easing (`linear`, `ease-in`, `ease-out`, `ease-in-out`, `cubic-bezier`) | 同图层同属性同帧至多 1 个关键帧，再次插入自动替换原值 |
| **本地播放 (`FR-010`)** | 顶栏/时间轴 `Play` 或 `Space` | 启动 Renderer `requestAnimationFrame` 循环，按项目 FPS 推进本地帧并插值绘制；点击 `Pause` 停在当前帧；点击 `Stop` 回到第 0 帧 | 播放期间禁用破坏性文件覆盖，编辑属性时自动暂停并定位到当前帧 |
| **撤销/重做 (`FR-011`)** | 顶栏 `Undo` / `Redo` 或 `Ctrl+Z` / `Ctrl+Y` | 按单条语义命令逆向回退或正向重做，同步 `DocumentRevision`、Canvas 与 Timeline | 历史栈为空时对应按钮处于 `disabled` 状态 |
| **工程保存/打开 (`FR-013`)** | 顶栏 `Save` / `Save As` / `Open` | 序列化为 `.motion.json` v1 并执行同目录临时文件原子替换；保存成功后清除 `IsDirty` | 打开损坏或 Schema 不兼容文件时保留当前工程不变并弹出内联错误横幅 |
