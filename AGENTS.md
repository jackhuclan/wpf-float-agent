# AGENTS.md — Floaticon 项目通用开发规范

面向 AI 编码助手与本仓库开发者的项目约定。**改动代码前请先读完本文件**；
详细风格规则以 `.editorconfig` 为准，本文件只提炼必须遵守的高频约定。

---

## 1. 项目概览

| 项 | 内容 |
|---|---|
| 产品 | **Floaticon**：常驻 Windows 桌面右下角的轻量 AI 助手悬浮球 |
| 技术栈 | .NET 10 WPF + Microsoft Agent Framework（MAF），流式对话、Markdown 渲染、Token 统计 |
| 扩展体系 | 技能（`skills/`）+ 工具（AIFunction），模型供应商可配置；Linux 容器内执行 C# / Python / JavaScript 代码 |
| 许可证 | MIT（`LICENSE`，Copyright © 2026 jackhuclan；包内 `PackageLicenseExpression` 保持一致） |
| 需求来源 | `docs/Floaticon-PRD-v1.1.md`（md 为准，docx 为存档）——改功能前先对照 PRD，功能变更后同步更新 PRD 与版本号 |
| 仓库 | `github.com/jackhuclan/wpf-float-agent`，主分支 `main` |

---

## 2. 工程结构与依赖方向

```
Floaticon.slnx
├─ src/Floaticon/             启动壳（WinExe）：App.xaml.cs 只做 Host 构建 + DI 装配，禁止写业务逻辑
├─ src/FloatAgent/            业务类库：Models / Services / Tools / ViewModels / Views / skills
├─ src/FloatAgent.Abstractions/ 接口类库（net10.0）：IAIToolService、ToolProgressService 等
├─ src/RecipeDemo/            演示外壳（WinExe）：配方树 + 五视图，引用 FloatAgent 显示悬浮球
├─ Directory.Build.props      全解决方案共享工程元数据（版本/作者/许可证，见 §3）
├─ Directory.Packages.props   NuGet 包版本集中管理（见 §4）
├─ docs/                      PRD 与设计文档
├─ build.cmd / build.ps1      构建、发布、打包入口（见 §5）
└─ artifacts/                 仅保留当前版本的 nupkg
```

**依赖方向（强制）**：`Floaticon → FloatAgent → FloatAgent.Abstractions`，禁止反向依赖。
类库内视图需要解析瞬时服务时走 `ServiceLocator.Services`（由启动壳赋值），**不得直接引用启动壳**。

---

## 3. 版本与工程元数据（Directory.Build.props）

- `<Version>` 是**唯一版本源**：同时作用于 AssemblyVersion / FileVersion / NuGet 包版本，发布升级只改这一处；
- 语义化版本：修 bug → 补丁位 +1；新功能 → 次版本 +1；破坏性变更 → 主版本 +1；
- `Deterministic=true`、`IncludeSourceRevisionInInformationalVersion=true`（文件属性可见 git 提交号），不要关闭；
- 作者、产品描述、PackageTags、License 表达式集中在此维护，**csproj 内不得重复定义**这些全局属性。

## 4. NuGet 包管理（Directory.Packages.props，CPM）

- `ManagePackageVersionsCentrally=true`：**所有包版本统一在 `Directory.Packages.props` 维护**；
- csproj 中 `PackageReference` 只写 `Include`，**禁止写 `Version`**（`CentralPackageVersionOverrideEnabled=false` 强制）；
- 新增依赖：先在 `Directory.Packages.props` 对应分组（MVVM 与渲染 / Agent 框架 / 通用主机与抽象）登记版本，再在 csproj 引用；
- 引入 preview 包需在登记处注明原因，升级正式版后移除注释。

---

## 5. 构建与运行

```bash
dotnet build Floaticon.slnx                    # 0 警告 0 错误是提交基线
dotnet run --project src/Floaticon             # 本地调试运行
build.cmd                                      # 单文件发布 -> release\
build.cmd -Pack                                # 发布 + NuGet 打包 -> artifacts\
build.cmd -Clean -Pack                         # 清理后重建
```

- 运行时工作目录锚定到 exe 目录（`Directory.SetCurrentDirectory(AppContext.BaseDirectory)`）；
  `config.json` / `skills/` / `sessions/` 均以运行目录为准；
- `config.json` 含 API Key，**绝不入库**（.gitignore 已忽略）；仓库只提供 `config.example.json` 模板；
- `bin/`、`obj/`、`release/`、`artifacts/` 为构建产物，不入库；`output/`、`sessions/` 为本地运行数据，不入库。

---

## 6. 代码风格（提炼自 .editorconfig）

### 6.1 格式化

- UTF-8、LF/CRLF 按 `.editorconfig` 文件类型约定（`.sh` 用 LF，`.cmd/.bat` 用 CRLF），文件末尾保留换行，去除行尾空白；
- 缩进：C#/Markdown 4 空格；csproj/props/xml/yaml 2 空格；
- 大括号独立成行（Allman：`csharp_new_line_before_open_brace = all`）；
- 命名空间用**文件作用域**（`csharp_style_namespace_declarations = file_scoped`），
  且命名空间与文件夹路径一致（`dotnet_style_namespace_match_folder`）；
- `using` 置于命名空间之外，`System.*` 排前；隐式全局 using 覆盖的 `System.*` 不要重复写；
  提交前清理多余 using（IDE0005）。

### 6.2 命名约定（本仓库强制）

| 对象 | 规则 |
|---|---|
| 私有/内部实例字段 | `_camelCase` |
| 私有/内部静态字段 | 同样 `_camelCase`（**不用 `s_` 前缀**，本仓库特有约定） |
| 常量字段 | `PascalCase` |
| 接口 | `I` 前缀 |
| 类型/方法/属性 | `PascalCase` |
| 局部变量/参数 | `camelCase` |

### 6.3 语言偏好

- 内置类型用关键字（`string`/`int`），不用 `String`/`Int32`；
- 不用 `this.` 限定成员；`var` 仅在类型显而易见时可用；
- 优先：模式匹配、`is null` 判空、`??`、`?.`、switch 表达式、对象/集合初始化器、显式元组名；
- 表达式主体成员、主构造函数可用（suggestion 级）；
- 修饰符顺序：`public private protected internal static extern new virtual abstract sealed override readonly unsafe volatile async`；
- 分析器级别已调优（IDE0005/IDE0130 = suggestion，CS0219/CS0168 = warning，
  CA1050/CA1812 关闭）——不要为消警告而改 `.editorconfig`，有异议先讨论。

### 6.4 提交前自检清单

- [ ] 编译 **0 警告 0 错误**；
- [ ] 多余 `using` 已清理；
- [ ] 无调试遗留（`TEMP-DEBUG` 钩子、假端点配置必须还原/删除）；
- [ ] 新依赖已登记 CPM；
- [ ] 行为改动已真实运行验证（见 §9）。

---

## 7. MVVM 与 DI 约定

- ViewModel 统一用 CommunityToolkit.Mvvm：`[ObservableProperty]` / `[RelayCommand]`；
  **命令的 `CanExecute` 依赖属性必须挂 `[NotifyCanExecuteChangedFor]`**，否则按钮状态不刷新（高频坑）；
- DI 注册集中在启动壳 `App.xaml.cs`：

| 服务 | 生命周期 | 原因 |
|---|---|---|
| ChatViewModel / ChatWindow / FloatingBallWindow | Singleton | 聊天窗是 Hide 而非销毁，重建会丢历史 |
| ConfigViewModel / ConfigWindow | Transient | WPF 窗口 Close 后不可复用 |
| IConfigService / IAIToolService / ToolProgressService | Singleton | 全局共享状态 |

---

## 8. 工具（Tools）扩展规则

新工具 = 一个 C# 方法 + `RegisterTools` 里注册。两个高频坑：

1. **必须用显式委托类型接方法组**（如 `Func<string, CancellationToken, Task<string>> run = tool.XxxAsync;`），
   方法/参数上的 `[Description]` 和参数名才会进入 AIFunction 的 JSON Schema；
   用 lambda 注册会退化成 `arg1` 且丢失取消接线。
2. **CancellationToken 参数自动排除**在模型可见签名之外，但要保留"停止"能力就必须在方法签名里带上它。

长耗时任务：

- 自研工具：注入 `ToolProgressService`，执行中 `Report("…")` 上报（等待按 1 秒切片，保证取消 1 秒内生效）；
- 不可修改的第三方方法：用 `PeriodicProgressService` 包装（开始/每 5 秒/完成三段式）；
  构造函数私有，统一 `PeriodicProgressService.Create(...)` 工厂创建，参考 `RegisterTools` 的 `thirdparty_long_run_task`。

---

## 9. 验证习惯（重要）

- **每个行为改动都要真实运行验证**，不要只靠编译通过：
  - UI 改动用截图对比（WPF 窗口用 `PrintWindow(PW_RENDERFULLCONTENT)` 抓取，不受遮挡影响）；
  - 命令行可验证的逻辑用临时 console 工程 `ProjectReference` 到 FloatAgent 直接调服务；
- 端到端验证模型链路：本地起假的 OpenAI 兼容 SSE 服务（ThreadingHTTPServer + chunked 编码），
  exe 目录 `config.json` 临时指向它，**测完必须还原**（OpenAI SDK 对连不上的端点会自动重试约 4 次/8s，假端点要即时应答）；
- 验证用 `TEMP-DEBUG` 钩子（如 `FLOATICON_AUTOSEND` 环境变量）验证完必须删除；
- GUI 自动化点击前先 `SetForegroundWindow` + `WindowFromPoint` 确认落点，否则点击被其它前台窗口吞掉会得出假结论。

---

## 10. Git 与提交规范

### 10.1 提交信息

- **中文 Conventional Commits**：`feat(scope): …` / `fix(scope): …` / `refactor(scope): …` / `docs: …`；
- 涉及代码改动的提交在描述末尾带版本尾注 `（1.14.0）`，与本次 `Directory.Build.props` 的 `<Version>` 一致；
- scope 常用值：`ui` / `tools` / `guess-game` / `demo` / `config` 等，沿用现有历史风格；
- **每个功能/修复单独成提交**；文档（PRD/README）与对应代码改动**同批提交**；
  纯文档改动可用 `docs: …`，不带版本尾注。

### 10.2 分支与推送

- 主分支 `main`，推送前确认工作区干净、构建通过；
- `.gitignore` 必须生效：`config.json`、`bin/`、`obj/`、`.vs/`、`.workbuddy/`、`release/`、`sessions/` 不入库。

### 10.3 发版流程（每次代码改动必做，不给"只改代码"的提交）

1. 升 `Directory.Build.props` 的 `<Version>`（修 bug → 补丁位，新功能 → 次版本）；
2. 同步文档：
   - `README.md` / `README.en.md`：功能特性表（新行为）、更新记录表（版本 + 日期 + 变更）、版本号相关示例命令；
   - `docs/Floaticon-PRD-v1.1.md`：对应 FR 条目与验收标准、「更新日期」、第 8 节版本历史新增一行；
3. 重新打包 `build.cmd -Pack`，`artifacts/` 只保留当前版本的
   `FloatAgent.<版本>.nupkg` 与 `FloatAgent.Abstractions.<版本>.nupkg`；
4. 同一批提交包含：**代码 + 文档 + 新 nupkg**，提交信息带版本尾注，推送 `main`；
5. CI 门禁：push 的最新提交若改了代码（`src/`、`*.csproj`、`Directory.Packages.props`、
   `Directory.Build.props`、`Floaticon.slnx`）而没升 `<Version>`，`publish-nuget` 流水线**直接失败**；
   纯文档提交不受限；同版本重复推送由 `--skip-duplicate` 跳过。

---

## 11. 已知约束（踩坑记录，勿重复踩）

- 沙箱/自动化环境里 GUI 子进程会在工具调用结束后被回收：启动→操作→截图→断言必须在单次工具调用内完成；
- 悬浮球/聊天窗/配置窗是普通窗口层级（强制置顶会互相争抢导致周期性闪烁，V1.1 已移除）；
- 配置窗 API Key 用 `PasswordBox` + `PasswordBoxBinding` 附加属性双向绑定
  （`PasswordBox.Password` 不是依赖属性，不能直接 Binding）；
- 本地 AI 工作区数据放 `.workbuddy/`（已 ignore，见 §10.2）。
