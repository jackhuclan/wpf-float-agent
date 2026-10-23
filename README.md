# Floaticon · 桌面悬浮 AI 助手

一个常驻 Windows 桌面右下角的轻量 AI 入口：一颗可拖拽的悬浮球，点击即弹出流式对话窗，内置 Markdown 渲染、Token 统计、技能（Skills）与工具（Tools）体系，模型供应商可配置。

基于 **.NET 10 WPF** 与 **Microsoft Agent Framework（MAF）** 构建，界面层零第三方 UI 框架依赖（全部自绘）。

```
┌──────────────┐   点击    ┌─────────────────────────────┐
│  悬浮球 60px  │ ───────▶ │  AI 助手            ⚙    ✕  │
│   可拖拽     │ ◀─────── │ ─────────────────────────── │
└──────────────┘   收起    │  消息区（Markdown 流式渲染）  │
                            │ ─────────────────────────── │
                            │  输入框                   ➤  │
                            └─────────────────────────────┘
```

## 功能特性

| 能力 | 说明 |
| --- | --- |
| 悬浮球常驻 | 72×72 无边框透明窗口，不占任务栏；拖拽移动，位移 ≤4 DIP 判定为点击 |
| 流式对话 | 逐 token 推送，首块到达即切换正文；生成中可随时停止 |
| Markdown 渲染 | 标题、加粗、代码块、引用、列表；Compact 紧凑排版（正文 12px） |
| 消息工具栏 | 每条 AI 回复下方显示 复制按钮 / `◇ 共消耗 N tokens` / `⚡ 模型名`（工具调用多次往返时用量累加） |
| 模型配置 | 三字段表单 + 实时必填校验 + **测试连接**（明确区分成功 / 鉴权失败 / 不可达）；保存即生效，无需重启 |
| 技能系统 | `skills/` 目录下的 `SKILL.md` 自动加载，模型按需 `load_skill`（渐进式披露） |
| 工具系统 | C# 函数经 `IAIToolService` 注册为 AIFunction，由 Agent 自动调用（`get_weather` / `run_long_task`） |
| 滚动体验 | 6px 细滚动条、像素滚动、滚轮 170ms ease-out 缓动、流式期间智能跟随底部 |
| 工具进度反馈 | 长耗时工具执行期间实时上报进度（如 `⚡ run_long_task 执行中… 60 / 120 秒`），显示在“正在输入”气泡下方，不再干等 |
| 多屏适配 | PerMonitorV2 DPI 感知，弹窗位置夹取到当前工作区内 |

## 技术架构

三工程拆分，依赖方向单向不可逆：

```
Floaticon（WinExe 启动壳）
  │  只做装配：Host 构建 → DI 注册 → Start → 显示悬浮球
  ▼
FloatAgent（业务类库，WPF）
  │  Models / Services / Tools / ViewModels / Views / skills
  ▼
FloatAgent.Abstractions（接口类库）
     IAIToolService（AddTool / AsTools）
```

**关键设计**

- **启动方式**：`Host.CreateApplicationBuilder` 构建通用主机，退出时 `StopAsync + Dispose` 释放资源。
- **类库不反向依赖启动工程**：类库内视图需要解析瞬时服务时通过 `ServiceLocator.Services`（由 `App` 在 Host 构建后赋值）。
- **配置即生效**：`AgentChatService` 每次请求都按 `IConfigService.Current` 重建 Agent，保存配置后下一条消息即使用新模型。
- **技能与工具的扩展点**：新增技能只需加目录；新增工具只需实现 `AIFunction` 并注册，均不改动聊天核心链路。

### 请求链路

```
ChatViewModel
  └─ IChatService.StreamReplyAsync(history)
       └─ AgentChatService
            ├─ 按当前配置创建 OpenAIClient → ChatClient → AsAIAgent
            ├─ ChatOptions.Instructions  = SystemPrompt
            ├─ ChatOptions.Tools         = IAIToolService.AsTools()
            └─ AIContextProviders        = AgentSkillsProvider（skills 目录）
                 │
                 ▼  MAF RunStreamingAsync
            StreamChunk(Text / Usage) ──▶ 气泡增量渲染（60ms 限流）
```

## 目录结构

```
wpf-float-agent/
├─ Floaticon/                    # 启动壳（WinExe WPF）
│  ├─ App.xaml / App.xaml.cs     # Host 构建、DI 装配、ServiceLocator 赋值
│  ├─ app.manifest               # PerMonitorV2 DPI 感知
│  └─ Properties/AssemblyInfo.cs
├─ FloatAgent/                   # 业务类库
│  ├─ Models/                    # ChatTurn、TokenUsage、ModelConfig
│  ├─ Services/                  # AgentChatService、JsonConfigService、AIToolService、
│  │                             # OpenAIModelTester、SkillProviderFactory、ServiceLocator
│  ├─ Tools/                     # WeatherTool（get_weather，Open-Meteo）
│  │                             # LongRunningTool（run_long_task，2 分钟长耗时）
│  ├─ ViewModels/                # ChatViewModel、ConfigViewModel、ChatMessage
│  ├─ Views/                     # FloatingBallWindow、ChatWindow、ConfigWindow
│  │  └─ Behaviors/              # SmoothScrollBehavior（滚轮缓动）
│  └─ skills/                    # grill-me、skill-creator、get-weather
├─ FloatAgent.Abstractions/      # IAIToolService
├─ docs/                         # PRD（md + docx）
├─ output/                       # PRD 生成链路中间产物
└─ Floaticon.slnx
```

## 环境要求

- Windows 10 / 11
- [.NET SDK 10.0](https://dotnet.microsoft.com/download)（开发时使用 10.0.402）
- 一个 OpenAI 兼容的模型端点（默认适配 DeepSeek，也支持本地 Ollama 的 qwen 系列）

## 快速开始

```bash
git clone https://github.com/jackhuclan/wpf-float-agent.git
cd wpf-float-agent

dotnet build Floaticon.slnx

# 运行（也可直接双击 Floaticon/bin/Debug/net10.0-windows/Floaticon.exe）
dotnet run --project Floaticon
```

启动后桌面右下角出现悬浮球，**单击**弹出聊天窗，**右键**可打开聊天或退出。

### 配置模型

API Key 只从 `config.json` 读取，**代码中不保留任何密钥**。首次启动时配置文件不存在，点开 **⚙** 后三个字段都会红框提示必填：

- **推荐**：在配置窗里填写三项 → 点「测试连接」确认可用 → 保存。应用会把配置写入 exe 所在目录的 `config.json`。
- **手动**：把 `config.example.json` 复制为 exe 同目录下的 `config.json` 并填入密钥，重启应用即可。

| 字段 | 说明 | 示例 |
| --- | --- | --- |
| API Key | 端点密钥（必填，无默认值，输入以掩码显示） | `sk-...` |
| 模型 | 下拉选择，已有配置值不在列表时会自动补入 | `deepseek-chat` |
| 接口地址 | OpenAI 兼容端点 | `https://api.deepseek.com/v1` |

配置保存为**运行目录下的 `config.json`**（启动时已把工作目录锚定到 exe 所在目录，故无论双击还是命令行启动都指向同一位置），结构见 `config.example.json`：

```json
{
  "ApiKey": "sk-...",
  "Model": "deepseek-chat",
  "Endpoint": "https://api.deepseek.com/v1"
}
```

> ⚠️ `config.json` 已列入 `.gitignore`，API Key 以明文存储，请勿提交或分享该文件；仓库里只有不含真实密钥的 `config.example.json`。配置文件缺失或损坏时会回落默认值（API Key 为空），不阻塞启动，只需重新填写。

## 扩展指南

### 新增技能

在 `FloatAgent/skills/` 下新建目录，放入 `SKILL.md`（YAML frontmatter + 正文指令）：

```markdown
---
name: my-skill
description: 一句话说明何时该用这个技能（模型据此决定是否加载）
---

# My Skill
具体执行步骤……
```

重新编译运行后，模型会先看到技能目录摘要，需要时自行 `load_skill` 加载全文。可选附带资源文件（`.md`/`.json`/`.yaml`/`.yml`/`.txt`），由 `read_skill_resource` 读取；出于安全考虑**不执行**技能内脚本。

### 新增工具

```csharp
public sealed class MyTool
{
    [Description("工具用途，模型据此决定何时调用")]
    public async Task<string> DoSomethingAsync(
        [Description("参数说明")] string input,
        CancellationToken cancellationToken = default) { /* ... */ }
}

// 在 Floaticon/App.xaml.cs 的 RegisterTools 中注册
Func<string, CancellationToken, Task<string>> fn = tool.DoSomethingAsync;
registry.AddTool(AIFunctionFactory.Create(fn, "do_something"));
```

> 用显式委托类型接收方法组，才能保留方法与参数上的 `[Description]`，模型才拿得到参数说明。

> 长耗时工具建议注入 `ToolProgressService`（DI 单例）并周期性 `Report("…")`，进度会实时显示在“正在输入”气泡下方，避免长时间零反馈。参考 `LongRunningTool` 的实现。

## 内置技能与工具

| 名称 | 类型 | 用途 |
| --- | --- | --- |
| `grill-me` | 技能 | 方案拷问：把模糊方案视为决策树，按「前沿」逐轮编号提问，前沿清空后输出共识摘要 |
| `skill-creator` | 技能 | 技能创作方法论：创建、改进、评测技能 |
| `get-weather` | 技能 | 天气查询规约：强制走工具取数、禁止编造、多城市多次调用、未指明城市先反问 |
| `get_weather` | 工具 | Open-Meteo 实时天气 + 未来三天预报（无需 API Key），WMO 代码映射中文描述 |
| `run_long_task` | 工具 | 长耗时任务（固定 2 分钟）：逐秒切片等待、支持中途取消、每 5 秒上报执行进度，返回任务名 / 起止时间 / 实际耗时；用于验证工具长时间占用时 Agent 仍能续答 |

## 技术栈

| 组件 | 版本 | 用途 |
| --- | --- | --- |
| .NET / WPF | net10.0-windows | 桌面框架 |
| Microsoft.Agents.AI(.OpenAI) | 1.25.0 | Agent、技能调度、流式对话 |
| Microsoft.Extensions.AI.Abstractions | 10.10.1 | AIFunction / ChatMessage 抽象 |
| CommunityToolkit.Mvvm | 8.4.2 | ObservableObject / RelayCommand |
| MdXaml | 2.0.0-pre | Markdown → FlowDocument 渲染 |
| Microsoft.Extensions.Hosting | 10.0.12 | 通用主机与依赖注入 |

## 已知限制

- 用户消息按纯文本渲染（蓝底气泡上 Markdown 观感不佳，方案待定）。
- 长回复期间 Markdown 整篇重排有轻微开销，已用 60ms 限流缓解。
- 中文输入法下首个 Enter 用于候选上屏属预期行为，上屏后的 Enter 才发送。
- 未做单实例限制，重复启动会出现多个悬浮球。

## 相关文档

- [产品需求文档 PRD v1.0](docs/Floaticon-PRD-v1.0.md) · [docx 版](docs/Floaticon-PRD-v1.0.docx)

## License

本项目暂未指定开源许可证。
