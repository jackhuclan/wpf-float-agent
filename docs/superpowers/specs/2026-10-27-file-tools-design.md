# 内置文件工具 read_file / write_file 设计规格

日期：2026-10-27 ｜ 状态：已获用户批准（白名单访问 / 简单覆盖+自动建目录 / 行号分页 / 写前弹窗确认）

## 1. 目标

为 FloatAgent 新增两个内置 AI 工具，使模型能在受控范围内读写本地文本文件：

- `read_file`：按行号分页读取白名单内文本文件；
- `write_file`：写前经聊天窗确认卡人工放行，同意后整文件覆盖写入（自动建父目录）。

两个宿主（Floaticon / RecipeDemo）通过既有 DI 注册自动获得，无需各自实现。

## 2. 非目标（YAGNI）

- 不做多模式写入（append / rename-if-exists）；
- 不做文件枚举 / glob / 删除 / 移动工具；
- 不做写后撤销；
- 不支持 GBK 等非 UTF-8 编码自动识别（按 UTF-8 读写，文档声明限制）。

## 3. 架构与组件

| 组件 | 位置 | 职责 |
| --- | --- | --- |
| `WorkspaceScope` | FloatAgent/Services | 白名单校验：根 = exe 目录（`AppContext.BaseDirectory`，主根）+ `config.json` 的 `WorkspaceRoots` 数组（每次校验动态读取，改配置即生效）；`GetFullPath` 归一后必须落在某根的边界内（防 `..` 穿越）；拒绝名单：文件名 `config.json`（任意大小写）**读写都拒**，防 API Key 泄露 |
| `IConfirmationService` / `ConfirmationService` | FloatAgent/Services | DI 单例；`RequestConfirmationAsync(toolName, title, detail, preview, ct) → Task<bool>`；无订阅者视为拒绝；`TaskCompletionSource` 挂起工具线程；确认超时 120 秒视为拒绝；`ct` 取消（用户点"停止"）视为拒绝 |
| `ConfirmationRequest` | FloatAgent/Services | 确认请求对象：`ToolName / Title / Detail / Preview` + `Complete(bool)`；ChatViewModel 收到事件后生成确认卡，按钮回调 `Complete` |
| `FileTools` | FloatAgent/Tools | 两个工具方法（`[Description]` 齐全），构造注入 `WorkspaceScope` + `IConfirmationService` + `ILogger<FileTools>` |
| `ChatViewModel` / `ChatWindow` | FloatAgent/ViewModels, Views | 订阅 `Requested`：UI 线程插入确认卡消息；聊天窗隐藏则 `Show + Activate`；同意/拒绝 RelayCommand 调 `Complete` 并更新卡片状态 |

### 3.1 工具签名（显式委托注册，[Description] 进 JSON Schema）

```
read_file(path, offset = 1, limit = 2000) → string
write_file(path, content) → string
```

- `read_file`：1-based 行号；返回 `行号→内容` 格式（行号右对齐）；默认显示第 1–2000 行；单行 >2000 字符截断并标注；文件 >10MB 或前 8KB 含 NUL 字节（二进制）→ 拒绝并说明原因；文件不存在 / 是目录 / 路径越界 → 返回错误说明字符串。
- `write_file`：`content` > 200 万字符拒绝；确认通过 → 自动建父目录 → UTF-8（无 BOM）覆盖写 → 返回「已写入：路径（N 行 / M 字节，新建|覆盖）」；用户拒绝 / 超时 / 取消 → 返回「用户拒绝了本次写入…未写入任何内容」，模型据此向用户解释。

### 3.2 错误处理约定

所有失败（路径校验、编码、大小、拒绝、IO 异常）一律**返回可读错误字符串而非抛异常**，让模型能把原因转述给用户。真实异常仍会经 `LoggedAIFunction` 记入 ToolCallLog。

## 4. 数据流（write_file）

```
模型 function_call
  → LoggedAIFunction（留痕）
  → FileTools.WriteFileAsync
  → WorkspaceScope.Validate（白名单 + 拒绝名单）
  → IConfirmationService.RequestConfirmationAsync（挂起）
      → ChatViewModel（UI 线程）：Messages.Add(确认卡)；ChatWindow 隐藏时自动弹出
      → 用户点[同意]/[拒绝]（或超时/点"停止"）
  → 同意：建目录 → File.WriteAllText(UTF-8 no BOM) → 返回写入摘要
     拒绝：返回拒绝说明（未写入）
```

确认卡消息 `Text` 恒为空字符串，天然被 `SyncSession` 跳过，**不入会话 json**。

## 5. 配置

`config.json` 新增可选字段（缺省只有 exe 目录一个根）：

```json
{ "WorkspaceRoots": ["D:\\work\\demo"] }
```

`ModelConfig.WorkspaceRoots` 标注 `JsonIgnoreCondition.WhenWritingNull`：未配置时保存不写 null 字段；配置窗不加编辑项（手改 config.json，改完即生效，无需重启）。

## 6. 测试计划

1. **console 探针**（临时工程 ProjectReference → FloatAgent）：
   - 白名单内相对/绝对路径通过；`..` 穿越、根外绝对路径、`config.json`（读+写）拒绝；
   - `WorkspaceRoots` 附加根生效；
   - read_file：分页（offset/limit 越界）、单行截断、二进制拒绝、超大拒绝、空文件；
   - write_file：自动建目录、覆盖、拒绝分支（确认服务返回 false）返回"用户拒绝"且文件未变。
2. **GUI 验证**（TEMP-DEBUG 钩子 `FLOATICON_TEST_CONFIRM=1`，验完删除）：
   - 启动 Floaticon → 弹聊天窗 → 钩子发起一次确认请求 → 截图确认卡样式 → 钩子延时自动执行同意命令 → 日志记录 `RequestConfirmationAsync` 返回 true，确认 TCS 链路闭环；再跑一次拒绝路径返回 false。
3. 全解决方案编译 0 警告 0 错误。

## 7. 发版

新功能 → 版本 1.8.0；README（中/英）特性表 + 更新记录、PRD 新增 FR-13 与版本历史、`config.example.json` 补示例、`build.cmd -Pack` 产出并只保留 1.8.0 两包、代码+文档+nupkg 同批提交推送，验证 CI 与 publish-nuget 双绿。
