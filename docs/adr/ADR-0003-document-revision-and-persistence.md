# ADR-0003 — 单语义命令事务、DocumentRevision 与 `.motion.json` 原子持久化

- **Status**: `Accepted`
- **Date**: `2026-11-06`
- **Related Requirements**: `FR-011`, `FR-013`, `NFR-003`, `NFR-005`, `NFR-008`
- **Related Risks**: `R-003`

## 1. Context

动画编辑器需要支持高频属性调整、多步撤销/重做 (`Undo`/`Redo`)、防止并发或断连恢复时的陈旧状态覆盖，以及保证工程保存时即使发生异常也不会损坏已有工程文件。

## 2. Decision

1. **命令信封与乐观并发 (`DocumentRevision`)**：
   - 所有修改文档的操作均封装为 `EditorCommandEnvelope`（包含 `CommandId`, `ContractVersion`, `DocumentId`, `BaseRevision`, `Timestamp`, `CommandType`, `Payload`）。
   - `EditorWorkspaceService` 校验 `BaseRevision`：若客户端提交的 `BaseRevision` 与服务端当前 `DocumentRevision` 不匹配，返回 `RevisionConflict` 错误，拒绝静默覆盖。
2. **单语义历史栈 (`Undo`/`Redo`)**：
   - 每次有效文档变更生成前向快照与逆向还原记录压入 Undo 栈；
   - 移动播放头 (`SeekFrame`)、切换缩放比例或播放/暂停属于瞬态视图状态，**严禁**压入 Undo 栈。
3. **`.motion.json` Schema v1 与同目录原子保存**：
   - 持久化采用 UTF-8 JSON (`schemaVersion: 1`)，包含 `projectId`, `name`, `composition`, `layers`, `tracks`, `assets`。
   - 保存流程：`Schema & Domain Validation -> 写入同目录 <file>.tmp -> Flush -> 若原文件存在则备份至 <file>.bak -> 原子移动替换目标文件`。
   - 打开流程：先完整读取并校验新文件，校验通过后才原子替换内存中的 `ProjectDocument`；若文件损坏或校验失败，当前已打开工程保持毫发无损。

## 3. Consequences

- 保证任何单步手势或属性修改均可 100% 精确 Undo/Redo；
- 保证磁盘写入中断或非法 JSON 输入不会导致用户数据丢失。
