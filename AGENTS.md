# AGENTS.md — Floaticon 项目通用规范

面向 AI 编码助手与本仓库开发者的项目约定。改动前请先读完本文件。

## 项目概览

Floaticon：Windows 桌面右下角悬浮 AI 助手（.NET 10 WPF）。悬浮球常驻，点击弹出流式对话窗；
基于 Microsoft Agent Framework（MAF）的 Agent 底座，支持技能（skills/）与工具（AIFunction）扩展。

需求来源：`docs/Floaticon-PRD-v1.1.md`（改功能前先对照 PRD；功能变更后同步更新 PRD 与版本号）。

## 工程结构

```
src/Floaticon/            启动壳（WinExe）：App.xaml.cs 只做 Host 构建 + DI 装配，禁止写业务逻辑
src/FloatAgent/           业务类库：Models / Services / Tools / ViewModels / Views / skills
src/FloatAgent.Abstractions/ 接口类库（net10.0）：IAIToolService
Directory.Packages.props   NuGet 包版本集中管理（csproj 里只写 Include 不写 Version）
docs/                      PRD（md 为准，docx 为存档）
```

依赖方向：Floaticon → FloatAgent → FloatAgent.Abstractions，禁止反向依赖。
类库内的视图需要解析瞬时服务时走 `ServiceLocator.Services`（由启动壳赋值），不直接引用启动壳。

## 构建与运行

```bash
dotnet build Floaticon.slnx          # 0 警告 0 错误是基线
dotnet run --project src/Floaticon       # 或直接运行 src/Floaticon/bin/Debug/net10.0-windows/Floaticon.exe
```

- 运行时工作目录被锚定到 exe 目录（`Directory.SetCurrentDirectory(AppContext.BaseDirectory)`），
  `config.json` / `skills/` 都以运行目录为准。
- `config.json` 含 API Key，**绝不入库**（.gitignore 已忽略）；仓库提供 `config.example.json` 模板。

## 代码规范

- 详细风格规则见 `.editorconfig`（命名、缩进、表达式主体、文件作用域命名空间等）。
- 提交前确认：多余的 `using` 已清理（隐式全局 using 覆盖的 `System.*` 不要再写）；
  编译 0 警告 0 错误。
- MVVM：ViewModel 用 CommunityToolkit.Mvvm（`[ObservableProperty]` / `[RelayCommand]`）；
  命令的 `CanExecute` 依赖的属性必须挂 `[NotifyCanExecuteChangedFor]`，否则按钮状态不刷新。
- 命名：私有字段 `_camelCase`；静态私有字段同样 `_camelCase`（不用 s_ 前缀）；接口 I 前缀。

## DI 注册约定（App.xaml.cs）

| 服务 | 生命周期 | 原因 |
| --- | --- | --- |
| ChatViewModel / ChatWindow / FloatingBallWindow | Singleton | 聊天窗是 Hide 而非销毁，重建会丢历史 |
| ConfigViewModel / ConfigWindow | Transient | WPF 窗口 Close 后不可复用 |
| IConfigService / IAIToolService / ToolProgressService | Singleton | 全局共享状态 |

## 工具（Tools）扩展规则

新工具 = 一个 C# 方法 + `RegisterTools` 里注册。两个高频坑：

1. **必须用显式委托类型接方法组**（如 `Func<string, CancellationToken, Task<string>> run = tool.XxxAsync;`），
   这样方法/参数上的 `[Description]` 和参数名才会进入 AIFunction 的 JSON Schema；
   用 lambda 注册会退化成 `arg1` 且丢失取消接线。
2. **CancellationToken 参数会自动排除**在模型可见签名之外，但要保留"停止"能力就必须在方法签名里带上它。

长耗时任务：

- 自己写的工具：注入 `ToolProgressService`，执行中 `Report("…")` 上报进度（参考 `LongRunningTool`，
  等待按 1 秒切片，保证取消 1 秒内生效）。
- 不可修改的第三方方法：用 `PeriodicProgressService` 包装（开始/每 5 秒/完成三段式上报）；
  构造函数私有，统一用 `PeriodicProgressService.Create(...)` 工厂创建（四种委托形态各有重载），
  参考 `RegisterTools` 里的 `thirdparty_long_run_task`。

## 验证习惯（重要）

- **每个行为改动都要真实运行验证**，不要只靠编译通过。UI 改动用截图对比（WPF 窗口用
  `PrintWindow(PW_RENDERFULLCONTENT)` 抓取，不受遮挡影响）；命令行可验证的逻辑用临时
  console 工程 `ProjectReference` 到 FloatAgent 直接调服务。
- 端到端验证模型链路：本地起一个假的 OpenAI 兼容 SSE 服务（ThreadingHTTPServer +
  chunked 编码），把 exe 目录 config.json 临时指向它，**测完必须还原**。
- 验证用的 `TEMP-DEBUG` 钩子（如 `FLOATICON_AUTOSEND` 环境变量）验证完必须删除。
- GUI 自动化点击前先 `SetForegroundWindow` + `WindowFromPoint` 确认落点，否则点击会被
  其它前台窗口吞掉，得出假结论。
- OpenAI SDK 对连不上的端点会自动重试约 4 次（约 8s）；假端点要用即时应答的本地服务器。

## 已知约束

- 沙箱/自动化环境里 GUI 子进程会在工具调用结束后被回收：启动→操作→截图→断言必须
  在单次工具调用内完成。
- 悬浮球/聊天窗/配置窗是普通窗口层级（强制置顶会互相争抢导致周期性闪烁，已在 V1.1 移除）。
- 配置窗 API Key 用 `PasswordBox` + `PasswordBoxBinding` 附加属性双向绑定
  （`PasswordBox.Password` 不是依赖属性，不能直接 Binding）。

## Git 约定

- 提交信息用中文 Conventional Commits：`feat(scope): …` / `fix(scope): …` / `docs: …`。
- 每个 功能/修复 单独成提交；文档（PRD/README）与代码改动同批提交。
- 推送前确认 `.gitignore` 生效：`config.json`、`bin/`、`obj/`、`.vs/`、`.workbuddy/` 不入库。
