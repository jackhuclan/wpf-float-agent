# Floaticon 桌面悬浮 AI 助手 产品需求文档（PRD）

| 项目   | 内容                                                            |
| ---- | ------------------------------------------------------------- |
| 产品名称 | Floaticon（桌面悬浮 AI 助手）                                         |
| 文档版本 | V1.7（文件名沿用 v1.1，保持外部链接稳定）                                        |
| 文档状态 | 已发布（对应已实现版本）                                                  |
| 编写日期 | 2026-10-22                                                    |
| 更新日期 | 2026-10-27                                                    |
| 目标平台 | Windows 10/11 桌面（.NET 10 WPF）                                 |
| 关联工程 | Floaticon（启动壳）、FloatAgent（业务类库）、FloatAgent.Abstractions（接口类库） |

## 执行摘要

Floaticon 是运行在 Windows 桌面右下角的常驻悬浮 AI 助手：以可拖拽悬浮球为唯一入口，点击弹出流式对话窗，内置 Markdown 渲染、token 统计与模型配置能力。

产品在 Microsoft Agent Framework（MAF）之上构建了可扩展的 Agent 底座：技能以 skills 目录下的 SKILL.md 文件承载（首发 grill-me 方案拷问、skill-creator 技能创作、get-weather 天气规约），工具通过 IAIToolService 注册 C# 函数（get_weather 实时天气查询、run_long_task 2 分钟长耗时任务），均由 Agent 自动调用；长耗时工具执行期间界面实时显示执行进度。

本文档汇总产品自 V0.1 骨架至 V1.1 的全部需求，含 10 项功能需求（FR-01～FR-10）、8 项非功能需求及验收标准，对应代码已全部实现并通过端到端验证。

## 1. 背景与目标

### 1.1 背景

日常研发与业务工作中，用户需要频繁向大模型提问（技术方案评估、业务机会分析、天气查询等）。现有交互方式（浏览器、独立客户端）上下文切换成本高，缺少一个"随手可唤起、常驻桌面角落"的轻量入口。

Floaticon 定位为 Windows 桌面右下角的常驻悬浮 AI 助手：一个可拖拽的悬浮球，点击即弹出聊天窗，支持流式对话、Markdown 渲染、可扩展的技能（Skills）与工具（Tools）体系。

### 1.2 产品目标

1. 提供零切换成本的 AI 问答入口：悬浮球常驻，一键唤起/收起聊天窗。
2. 提供接近主流 AI 产品的对话体验：流式逐字输出、Markdown 渲染、token 统计。
3. 建立可扩展的 Agent 能力底座：技能（SKILL.md 文件）与工具（C# AIFunction）均可在不改核心代码的情况下扩展。
4. 模型供应商可配置：支持任意 OpenAI 兼容端点（默认 DeepSeek），配置保存后立即生效。

### 1.3 衡量指标

- 悬浮球点击到聊天窗弹出的响应时间无明显可感知延迟（300ms 量级以内）。
- 对话回复流式呈现，无整段卡顿；长回复滚动流畅。
- 技能/工具扩展只需新增文件或注册代码，无需修改聊天核心链路。

## 2. 用户与使用场景

| 用户角色          | 典型场景                          | 核心诉求                 |
| ------------- | ----------------------------- | -------------------- |
| 研发/产品人员（主要用户） | 写方案前让 AI 拷问方案的漏洞（grill-me 技能） | 快速唤起、多轮结构化追问         |
| 研发/产品人员       | 查询城市天气、决定是否带伞/出行              | 真实数据，不允许模型编造         |
| 技能创作者         | 创建、改进、评测新的技能                  | skill-creator 方法论内置  |
| 通用问答          | 技术问题、翻译、总结                    | 流式输出、可复制 Markdown 原文 |

## 3. 术语表

| 术语          | 定义                                                                           |
| ----------- | ---------------------------------------------------------------------------- |
| 悬浮球         | 常驻桌面右下角的圆形 AI 入口图标，独立无边框窗口                                                 |
| 流式回复        | 模型生成内容逐 token 推送并实时渲染，而非等待完整回复                                               |
| MAF         | Microsoft Agent Framework（NuGet：Microsoft.Agents.AI 1.25.0），承载 Agent、技能与工具调用 |
| SKILL.md    | 技能定义文件，YAML frontmatter（name/description）+ 正文指令                              |
| AIFunction  | Microsoft.Extensions.AI 的函数工具抽象，由 Agent 自动调用                                 |
| 渐进式披露       | 模型先看到技能目录，按需 load_skill 加载完整内容，节省上下文                                         |
| config.json | 运行目录下的模型配置持久化文件（API Key、模型名、接口地址）                                            |

## 4. 功能需求

> 优先级定义：P0 = 本版本必须交付；P1 = 重要增强；P2 = 体验优化。以下各功能均已实现并通过端到端验证。

### FR-01 悬浮球（P0）

**需求描述**

1. 应用启动后仅在桌面右下角显示悬浮球，聊天窗按需弹出；悬浮球为独立窗口：无边框、透明背景、不占用任务栏（普通窗口层级，不强制置顶——多窗口互抢置顶位会导致周期性闪烁，V1.1 移除）。
2. 悬浮球直径 60 DIP（窗口 72×72），定位在工作区右下角（距边缘 24 DIP），始终可见。
3. 支持鼠标左键拖拽移动；拖拽结束若位移 ≤4 DIP 判定为"点击"，>4 DIP 判定为"拖拽"。
4. 单击悬浮球切换聊天窗显示/隐藏；弹出时球体播放弹性缩放动画（0.85→1.0，180ms，BackEase）。
5. 右键菜单提供"打开聊天""退出"两个入口；"退出"结束整个应用。
6. 聊天窗打开状态下拖动悬浮球，聊天窗跟随移动到球体正上方。

**验收标准**

- [ ] 启动后仅见悬浮球，任务栏无入口。
- [ ] 拖拽后松开不触发聊天窗；原地单击正常切换聊天窗。
- [ ] 右键菜单两项功能可用；退出后进程结束、资源释放。

### FR-02 聊天窗口（P0）

**需求描述**

1. 无边框、圆角（16px）、带阴影的自绘窗口，尺寸 456×811 DIP。
2. 自绘标题栏：品牌色 #6366F1，左侧"迷你球图标 + AI 助手"标题，右侧"⚙ 配置"与"✕ 收起"按钮；标题栏可拖动窗口。
3. 弹出位置：水平居中于悬浮球正上方（间距 10 DIP），并夹取到屏幕工作区内，避免弹出屏外。
4. 点击窗口外部不关闭窗口；"✕"为隐藏（Hide）而非销毁，再次点击悬浮球可恢复，历史消息保留。
5. 消息区 + 输入区 + 标题栏三段式布局；消息气泡分用户侧（靠右、蓝色 #4F6BF5、圆角 12,12,4,12）与 AI 侧（靠左、白色、圆角 12,12,12,4）。

**验收标准**

- [ ] 窗口在多屏/小屏场景弹出位置均在工作区内。
- [ ] 收起后再唤起，历史消息保留。

### FR-03 输入与发送（P0）

**需求描述**

1. 输入框多行显示（自动换行，最小高 34、最大高 96 DIP），Text 与 ViewModel 双向同步（PropertyChanged 触发）。
2. **Enter 直接发送**；**Shift+Enter 在光标处插入换行**。中文输入法状态下，首次 Enter 用于候选上屏（不触发发送），上屏后的 Enter 才发送。
3. 空输入不可发送（按钮禁用态，透明度 0.35）；输入非空时右下角显示圆形发送按钮（38px，#6366F1，白色纸飞机箭头图标，hover #7C7DF3、按下 #4F52D9，图标在按钮内水平垂直居中）。
4. 生成过程中发送按钮切换为**停止按钮**（同位置、同风格、白色圆角方块图标），点击中止本次生成；中止后保留已生成文本并追加"（已停止生成）"。
5. 发送后输入框立即清空；生成结束后按钮恢复为发送态。

**验收标准**

- [ ] 英文/中文输入法下 Enter 均可发送（IME 上屏行为除外）。
- [ ] Shift+Enter 换行不触发发送。
- [ ] 生成中可随时停止，无残留忙碌态。

### FR-04 流式回复与 Markdown 渲染（P0）

**需求描述**

1. 对话通过 DeepSeek（OpenAI 兼容协议）完成，经 MAF Agent（ChatClientAgent）承载；回复逐 token 流式推送。
2. 发送后先显示"正在输入"占位气泡（三点动画），收到首块内容后切换为正文渲染。
3. AI 消息使用 MdXaml 渲染 Markdown（内置 Compact 紧凑风格：正文 12px、H1 16/H2 14/H3 13），字体统一 Microsoft YaHei UI；用户消息保持纯文本。
4. 每次回复结束回填 token 用量与模型名（见 FR-05）。
5. 系统提示词约束：回答简洁、直接、可执行；技能/工具返回内容为事实来源，禁止编造真实世界数据。

**验收标准**

- [ ] 标题、加粗、行内代码、代码块、引用、列表渲染正常。
- [ ] 流式过程中无整窗卡死；停止后无异常冒泡到用户。

### FR-05 消息复制与 AI 消息工具栏（P1）

**需求描述**

1. 用户气泡与 AI 气泡均支持右键菜单"复制"（复制原始 Markdown 文本，剪贴板异常需捕获提示，不崩溃）。
2. 每条 AI 消息下方展示工具栏：
   - 复制图标按钮（双矩形图标）；
   - token 统计：`◇ 共消耗 N tokens`（N 为本次回复总消耗；当发生工具调用导致模型多次调用时，**累加所有调用**的输入/输出/总 token）；
   - 模型名：`⚡ deepseek-chat`。
3. 问候语等无用量信息的消息自动隐藏统计与模型名区域。
4. 用户气泡鼠标悬浮时，气泡下方靠右露出操作行：时间标签 + 复制图标按钮；鼠标移出隐藏。时间为消息发送时刻，今天显示 `HH:mm`（如 `11:25`），昨天及以前显示 `M月d日 HH:mm`（如 `10月24日 09:50`）。
5. 悬浮判定覆盖**整条消息行**（气泡 + 下方操作行 + 两者之间的间隙），而非仅气泡本身：指针从气泡移向复制按钮的整个过程中操作行保持显示，不会中途消失；复制按钮可点区域 26×18（图标 13×13 居中），点击即复制消息原文。

**验收标准**

- [ ] 带工具调用的回复统计值等于各次调用之和（已实测验证）。
- [ ] 复制内容与气泡原文一致。
- [ ] 指针从气泡移到复制按钮（含两者之间的 4px 间隙）过程中操作行不隐藏，按钮可正常点击并复制（已实测验证）。

### FR-06 模型配置窗（P0）

**需求描述**

1. 标题栏 ⚙ 打开模态配置窗（440×500，普通窗口层级，圆角风格与主窗一致）。
2. 三个必填字段：API Key（PasswordBox 掩码输入，输入字符不可见）、模型（下拉框，预置 `qwen3:1.7b`、`qwen3:8b`、`deepseek-flash`、`deepseek-v4-pro`；已有配置值不在列表中时自动补入首项）、接口地址（OpenAI 兼容端点，如 `https://api.deepseek.com/v1`）；下拉框与其它输入框**水平拉伸等宽**（HorizontalAlignment=Stretch，下拉弹出列表同宽），不出现固定窄宽。
3. 实时校验：任一必填字段清空立即标红（边框 #EF4444，1.5px），输入后立即恢复；接口地址保存前校验必须为合法 http/https URL。
4. **测试连接**按钮：用当前表单值（允许未保存）发一次真实小请求，明确区分三种结果——成功、鉴权失败（401 类）、不可达，并以醒目文案展示。
5. **测试连接进行中，保存按钮进入禁用态并有可辨识的禁用外观（淡靛蓝 #C7CDF4），结果返回后立即恢复可用**；配置窗底部不展示配置文件路径。
6. 保存写入运行目录 `config.json`，**立即生效**（每次请求按当前配置重建 Agent，无需重启）；保存成功后自动关闭窗口。
7. 配置文件缺失或损坏时回落默认配置，不阻塞启动；代码内不保留任何 API Key 默认值，密钥仅从 `config.json` 读取。

**验收标准**

- [ ] 三字段清空全部标红；填回后红色即时消失。
- [ ] API Key 输入框以掩码圆点显示，任何字符均不可见。
- [ ] 模型下拉框与 API Key、接口地址输入框左右边界对齐（等宽），下拉弹出列表同宽。
- [ ] 测试连接对错误 Key/错误地址给出明确区分的提示。
- [ ] 测试连接进行中保存按钮呈可辨识的禁用外观，返回后恢复可用。
- [ ] 修改模型后立即发消息使用新模型（AI 工具栏模型名可验证）。
- [ ] 仓库源码中不含任何 API Key；config.json 已被版本库忽略，仓库仅提供无密钥模板 config.example.json。

### FR-07 技能系统 Skills（P1）

**需求描述**

1. 技能以文件夹形式存放于 `skills/` 目录（随程序输出到 exe 旁），每个技能一个目录，含 `SKILL.md`（YAML frontmatter：name/description + 正文指令）与可选资源文件。
2. 基于 MAF 1.25 内置 `AgentSkillsProvider` 加载：运行时模型先获得技能目录摘要，按需通过 `load_skill` / `read_skill_resource` 渐进加载完整内容。
3. 首发内置三个技能：

| 技能                  | 定位与核心规则                                                                         |
| ------------------- | ------------------------------------------------------------------------------- |
| grill-me（方案拷问）      | 把用户方案视为决策树，按"前沿（frontier）"逐轮编号提问并给出推荐答案；事实自查（工具查证）、决策交用户；前沿清空后输出共识摘要，未经确认不执行方案。 |
| skill-creator（技能创作） | 创建、改进、评测技能的完整方法论（含 references 参考资料）。                                            |
| get-weather（天气查询规约） | 强制通过 get_weather 工具取数、禁止凭记忆编造、多城市多次调用、未指明城市先反问、失败时明确降级话术。                       |

1. 桌面端无审批 UI：加载技能、读取资源等操作自动放行（Disable 全部 Approval）。
2. 只暴露文档类资源（.md/.json/.yaml/.yml/.txt），不执行技能脚本。

**验收标准**

- [ ] 提示"用 grill-me 拷问我的方案"时，Agent 先加载技能，再按 ❓Q1/➡️推荐答案 的轮次格式多轮追问。
- [ ] 询问天气时自动调用工具并遵循 get-weather 规约的输出格式。
- [ ] 在 skills 目录新增技能文件夹（含合法 SKILL.md）后重启即可被识别。

### FR-08 工具系统 Tools（P0）

**需求描述**

1. 提供工具注册表抽象 `IAIToolService`（独立接口工程 FloatAgent.Abstractions）：`AddTool(AIFunction)` 按函数名注册（重名覆盖）、`AsTools()` 输出全部工具；实现类 `AIToolService` 置于 FloatAgent。
2. `AgentChatService` 注入 `IAIToolService`，将全部工具交给 MAF Agent 自动函数调用。
3. 首发内置工具 **get_weather**（C# 实现，Open-Meteo 数据源，无需 API Key）：
   - 输入：城市名（中英文均可）、可选温度单位（摄氏/华氏）；
   - 流程：地理编码 → 实时天气 + 未来三天预报；
   - 输出：中文结构化文本（实时温度/体感/湿度/风速/降水 + 逐日天气、区间温度、降水概率），WMO 天气代码映射为中文描述，末尾标注数据来源；
   - 失败处理：城市未找到、网络异常、解析异常均返回可读中文错误信息，不向模型抛异常。
4. 第二个内置工具 **run_long_task**（长耗时任务，验证工具长时间占用场景）：
   - 输入：任务名称 `task`（模型可见参数说明）；
   - 行为：固定执行 **120 秒**，按 1 秒切片等待（点"停止"可在 1 秒内响应取消）；
   - 输出：任务名、状态、起止时间、实际耗时、任务编号的结构化 Markdown；
   - 执行期间通过 ToolProgressService 实时上报进度（见 FR-10）。
5. Agent 具备多轮工具调用能力：一次用户提问可触发"调用工具 → 拿结果 → 继续生成"的完整链路；工具执行数分钟后仍能正常续答。
6. 第三个内置工具 **thirdparty_long_run_task**（模拟第三方长任务，验证 PeriodicProgressService）：模拟一个内部无法上报进度的第三方任务（约 15 秒），由包装器在等待侧周期上报进度（见 FR-10 需求 6），结束后返回结构化执行结果；注册代码位于启动壳 `RegisterTools`，作为接入真实第三方长任务的参考模板。
7. 第四个内置工具 **run_container_code**（容器内代码执行，基于 NuGet 包 `Microsoft.Agents.AI.Tools.Shell` 的 `DockerShellExecutor`）：
   - 由 `ContainerCodeTool` 承载，在一次性 Linux 容器（Docker）中执行 C# / Python / JavaScript 代码，宿主机无需安装任何运行时；
   - 脚本经构造函数传入，**支持两种形式**：脚本文件路径（按扩展名识别语言）或内联代码文本（按内容特征识别，也可显式指定 `language` 参数）；
   - 语言与镜像映射：C# → `dotnet/sdk:10.0`（容器内生成离线可 restore 的迷你 console 工程执行）、Python → `python:3.12-slim`、JavaScript → `node:22-slim`；
   - 隔离与安全：每次执行创建一次性容器（执行完即销毁），默认网络隔离（`none`）、1 GiB 内存上限、180 秒超时；
   - 代码以 base64 写入容器文件系统后执行，规避引号/换行等 shell 转义问题；C# 经 `dotnet build` + `dotnet exec` 加载 dll（绕开非 root 用户 apphost 的 exec 权限问题），全程不访问网络；
   - 执行期间通过 ToolProgressService 周期上报进度（开始 1 次 + 每 5 秒 1 次 + 完成 1 次），首次运行含镜像拉取时界面不零反馈；
   - 输出：状态（成功/失败 + 退出码）、语言、来源、起止时间与耗时、标准输出、标准错误的结构化 Markdown；
   - Docker 不可用 / 安全策略拒绝时返回可读中文错误，不向模型抛异常。

**验收标准**

- [ ] 提问"上海今天要带伞吗"返回真实天气数据（与 Open-Meteo 一致）。
- [ ] 调用 run_long_task 后界面实时显示执行进度，120 秒后模型基于工具返回结果正常续答。
- [ ] 长任务执行中点击"停止"，1 秒内中止并提示"（已停止生成）"。
- [ ] 工具调用失败时用户收到可读的失败说明而非报错堆栈。
- [ ] 请求验证容器执行时自动调用 run_container_code，在 Linux 容器中执行演示 C# 代码并返回标准输出/退出码；首次运行拉取镜像期间界面有周期进度反馈。

### FR-10 工具执行进度反馈（P1）

**需求描述**

1. 工具在 Agent 流式管道内同步执行，执行期间 `StreamChunk` 零输出；长耗时工具不得让界面长时间停留在"只有三个跳动圆点"的零反馈状态。
2. 提供 `ToolProgressService`（DI 单例）作为进度广播通道：工具执行期间调用 `Report(文本)` 上报，与主流式链路互不阻塞。
3. 进度文本经 ChatViewModel 调度回 UI 线程后，显示在"正在输入"占位气泡下方（小字号灰色文本）；无进度时不占位，首块正文到达后立即清除。
4. 上报节奏由工具自定：run_long_task 为开始 1 次、执行中每 5 秒 1 次（`⚡ run_long_task 执行中… N / 120 秒`）、结束 1 次。
5. 进度更新不得抢焦点、不得影响窗口位置与流式渲染。
6. **第三方长耗时方法的通用包装**：对内部无法注入进度上报的第三方方法，提供 `PeriodicProgressService` 包装器（第三方代码零改动）——调用后上报「{方法名} 开始执行...」，等待期间每 5 秒上报「{方法名} 正在执行... 已运行 N 秒」，结束后上报「{方法名} 执行完成」；构造函数私有，统一通过 `Create` 工厂方法创建，支持 `Func<CancellationToken, Task>` / `Func<Task>` / `Action` / `Func<CancellationToken, Task<string>>`（返回值附入结果）四种形态；第三方异常转为可读的失败结果文本返回模型，取消语义保持向上传播（点"停止"在周期间隔内生效）。

**验收标准**

- [ ] 调用 run_long_task 期间，气泡下方每 5 秒更新一次执行进度（含已耗时与总时长）。
- [ ] 工具返回后正文正常流出，进度文本消失，不残留占位样式。
- [ ] 长任务执行中可随时点"停止"，取消在 1 秒内生效。

### FR-09 滚动与流畅性体验（P1）

**需求描述**

1. 消息区滚动条为自定义细窄样式（宽 6px、圆角滑块，hover/拖拽变色），通过自定义 ScrollViewer 模板显式引用（隐式样式无法穿透模板边界）。
2. 消息区采用像素滚动（CanContentScroll=False），保证滑块尺寸计算正确与平滑位移。
3. 滚轮滚动带缓动动画：每档滚动 96px，170ms ease-out cubic 帧插值（15ms/帧），替代原生"一次跳 3 行"的生硬体验；支持连续滚动打断衔接。
4. 流式回复期间消息区自动跟随底部（距底 96px 内视为跟随态）；用户主动上翻查看历史时不被打断；新消息到达时强制回底。
5. 流式文本按 60ms 限流批量刷新，避免 Markdown 每分片整篇重排导致的卡顿；流结束后做最终全文刷新。

**验收标准**

- [ ] 长回复场景滚轮滚动平滑无跳变，可随时反向滚动。
- [ ] 生成过程中视图持续跟随最新内容；上翻后不再自动回底。

### FR-11 会话自动保存（P1）

**需求描述**

1. 每次开始新对话（应用启动或用户点「新建会话」）即生成会话 guid，会话整体持久化为运行目录 `sessions/<guid>.json`，一个会话一个文件。
2. 提供 `ISessionStore`（DI 单例）与 `JsonSessionStore` 实现：`StartNewSession()` 生成新 guid，`Save()` 整会话覆盖写（先写 `.tmp` 再原子替换，不留半截文件）；目录不存在时自动创建。
3. 会话内容：sessionId、标题（取首条用户消息前 24 字，换行压成空格）、创建/更新时间，以及逐条消息的 role / text / timestamp / token 用量 / 模型名；"正在输入"占位等界面态不入盘。
4. 落盘时机：用户消息发送后立即写一次（防中途崩溃丢提问），每轮问答结束（含停止 / 出错）再写一次；保存失败只记调试日志，不打断对话。
5. 聊天窗标题栏新增「＋」新建会话按钮：清空消息、换新 guid 并立刻写出新的会话文件；生成期间禁用。
6. 会话目录 `sessions/` 属本地数据，加入 `.gitignore` 不入库。

**验收标准**

- [ ] 启动应用后 `sessions/` 下出现以当前会话 guid 命名的 json。
- [ ] 发送一条消息并收到回复后，文件含 assistant / user / assistant 三条且用量、模型名正确。
- [ ] 点「＋」后产生新的 guid 与新文件，旧文件内容不变。

### FR-12 工具执行日志与调用历史查证（P1）

**需求描述**

1. 所有经 `IAIToolService` 注册的 AI 工具由 `LoggedAIFunction` 包装：每次真实调用自动写入 `ToolCallLog`（DI 单例，会话内最多 200 条，超出淘汰最旧），记录时间 / 工具名 / 参数 / 结果 / 耗时 / 是否异常；被取消与抛异常的调用同样入日志。
2. 内置工具 `query_tool_history(last = 10)`：返回最近 N 条调用的可读文本，供模型核实"某一步是否真的执行过、结果是什么"，替代凭记忆断言调用历史。
3. 新建会话（启动或点「＋」）时 `ToolCallLog.Clear()`，日志以会话为边界。
4. Floaticon 与 RecipeDemo 均注册 `ToolCallLog`；技能正文（alignment）配套新增核心原则 8–10：事实只认工具返回原文或用户明确告知、被质疑先定事实不迎合、返回值必须如实引用。

**验收标准**

- [ ] 工具注册清单含 `query_tool_history`，且与各 ViewModel 注册的工具并列可见。
- [ ] 连续调用多个工具后，`query_tool_history` 按时间返回各次调用的参数与结果原文。
- [ ] 清空（新会话）后查询返回"没有任何工具调用记录"。

### FR-13 内置文件工具 read_file / write_file（P1）

**需求描述**

1. FloatAgent 内置两个文件工具（`Tools/FileTools`），两个宿主（Floaticon / RecipeDemo）经 DI 注册自动获得：
   - `read_file(path, offset = 1, limit = 2000)`：按行号分页读取文本文件（1-based 行号右对齐前缀），默认前 2000 行、单次上限 5000 行；单行超 2000 字符截断并标注原始长度；空文件 / 文件不存在 / 目录 / 超 10MB / 二进制（前 8KB 含 NUL）均返回可读错误说明；
   - `write_file(path, content)`：整文件覆盖写入，父目录自动创建，UTF-8 无 BOM；内容上限 200 万字符。
2. **工作区白名单**（`WorkspaceScope`）：允许的根 = 运行目录（exe 所在目录，相对路径基准）+ `config.json` 可选字段 `WorkspaceRoots`（数组，每次校验动态读取，改配置即生效）；绝对路径必须落在某根内（目录分隔符边界检查，防 `..` 穿越与同前缀目录名误判）；拒绝名单：任意层级的 `config.json`（含 API Key）**读写都拒**。
3. **写前人工确认**（`IConfirmationService`）：`write_file` 校验通过后挂起等待人工决定，聊天窗插入确认卡（标题 / 目标路径 / 内容预览前 15 行 / 同意·拒绝按钮），聊天窗隐藏时自动弹出；用户同意 → 执行写入并返回写入摘要；拒绝 / 确认超时（120 秒）/ 取消（点"停止"）/ 无 UI 订阅 → 不写入任何内容并返回"用户拒绝"说明。确认卡消息不入会话 json。
4. 所有失败一律返回可读字符串而非抛异常，便于模型向用户解释原因。

**验收标准**

- [ ] 白名单内相对 / 绝对路径可读写；`..` 穿越、根外路径、同前缀目录名、`config.json`（含子目录）读写均被拒绝并给出原因。
- [ ] `WorkspaceRoots` 配置的附加根即配即生效，移除后立即失效。
- [ ] 大文件 / 二进制 / 单行超长按规格截断或拒绝；`offset/limit` 分页与越界提示正确。
- [ ] `write_file` 未确认前不落盘；同意后自动建父目录、内容与编码正确、二次写报"覆盖原文件"；拒绝后文件内容不变。

## 5. 非功能需求

| 编号     | 类别   | 需求                                                                                                                                                                                                             |
| ------ | ---- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| NFR-01 | 技术栈  | .NET 10（net10.0-windows）WPF；CommunityToolkit.Mvvm 8.4.2（MVVM）；Microsoft.Extensions.DependencyInjection 10.0.12；Microsoft.Extensions.Hosting 10.0.12；Microsoft.Agents(.AI) 1.25.0；MdXaml 2.0.0-pre              |
| NFR-02 | 启动方式 | 通过 `Host.CreateApplicationBuilder` 构建通用主机：注册服务 → Build → Start → 显示悬浮球；退出时 StopAsync + Dispose 释放资源                                                                                                            |
| NFR-03 | 工程结构 | 三工程拆分：Floaticon（WPF 启动壳：App/Host/DI 装配）、FloatAgent（业务类库：Models/Services/Tools/ViewModels/Views/skills）、FloatAgent.Abstractions（接口：IAIToolService）；依赖方向 Floaticon → FloatAgent → FloatAgent.Abstractions，禁止反向依赖 |
| NFR-04 | 可扩展性 | 新增工具：实现 AIFunction 并注册到 IAIToolService；新增技能：添加 skills 子目录；均不需改动聊天核心链路                                                                                                                                          |
| NFR-05 | 可靠性  | 配置缺失/损坏回落默认值；所有用户可见失败以气泡内可读文案呈现；剪贴板、网络异常均有兜底，不允许未处理异常崩溃                                                                                                                                                        |
| NFR-06 | 性能   | 流式渲染限流 60ms；滚轮动画 15ms/帧；技能按需加载（渐进披露）控制上下文开销                                                                                                                                                                    |
| NFR-07 | 安全   | API Key 明文保存于本地 config.json（需提示用户注意保管/轮换）；**代码内不保留任何密钥默认值，密钥仅从运行目录 config.json 读取**；config.json 已加入 .gitignore，仓库提供无密钥模板 config.example.json；配置窗 API Key 以掩码显示；不执行技能脚本，资源读取限定白名单扩展名                                                              |
| NFR-08 | 兼容   | 支持任意 OpenAI 兼容端点（DeepSeek、本地 Ollama qwen 系列等），不锁定供应商                                                                                                                                                           |

## 6. 系统架构与工程结构

> 物理布局：三个工程目录统一放在仓库根目录的 `src/` 下（`src/Floaticon`、`src/FloatAgent`、`src/FloatAgent.Abstractions`），解决方案文件 `Floaticon.slnx` 位于根目录。

### 6.1 架构分层

```
Floaticon（启动壳，WinExe）
  ├─ App.xaml.cs：Host.CreateApplicationBuilder → DI 装配 → ServiceLocator 赋值 → 显示悬浮球
  └─ 依赖：Microsoft.Extensions.Hosting / DependencyInjection
        │
        ▼
FloatAgent（业务类库，net10.0-windows + UseWPF）
  ├─ Services/：AgentChatService（MAF 流式对话）、SkillProviderFactory（技能加载）、
  │             AIToolService（工具注册表）、ToolProgressService（工具进度广播）、
  │             JsonConfigService、OpenAIModelTester、ServiceLocator
  ├─ Tools/：WeatherTool（get_weather，Open-Meteo）、LongRunningTool（run_long_task，2 分钟长耗时）
  ├─ ViewModels/：ChatViewModel、ConfigViewModel、ChatMessage
  ├─ Views/：FloatingBallWindow、ChatWindow、ConfigWindow、Behaviors/SmoothScrollBehavior
  ├─ Models/：ChatTurn、TokenUsage、ModelConfig
  └─ skills/：grill-me、skill-creator、get-weather（随输出复制到主程序目录）
        │
        ▼
FloatAgent.Abstractions（接口类库，net10.0）
  └─ IAIToolService（AddTool / AsTools）
```

### 6.2 关键数据流

1. **发送链路**：ChatViewModel → IChatService.StreamReplyAsync → AgentChatService（按配置建 Agent：ChatClientAgentOptions 挂 Instructions/Tools/AIContextProviders[技能]）→ MAF RunStreamingAsync → StreamChunk(Text/Usage) → 气泡增量渲染。
2. **工具调用链路**：模型输出 function_call → MAF 自动执行 AIFunction → 结果回传模型 → 二次生成；各次用量累加后统一回填。
3. **工具进度旁路**：工具内 ToolProgressService.Report → ChatViewModel（Dispatcher 调度回 UI 线程）→ ChatMessage.Status → "正在输入"气泡下方实时刷新；首块正文到达即清除；与主流式链路互不阻塞。
4. **配置链路**：ConfigViewModel → JsonConfigService.Save(config.json) → 内存 Current 立即更新 → 下次请求生效。

### 6.3 拉平技能流程图

RecipeDemo 的 alignment 技能（4 阶段 12 动作、含人工确认卡点）执行顺序与分支判定见 [alignment-flow.md](alignment-flow.md)（Mermaid，GitHub 直接渲染），该图为技能正文 `src/RecipeDemo/skills/alignment/SKILL.md` 的可视化整理，冲突时以技能正文为准。

## 7. 已知问题与风险

| 编号   | 风险/问题                     | 影响                      | 缓解措施                                   |
| ---- | ------------------------- | ----------------------- | -------------------------------------- |
| R-01 | API Key 明文存储于 config.json | 凭据泄露风险                  | 文档提示用户保管与轮换；后续可加密存储                    |
| R-02 | 长回复 Markdown 全篇重排         | 轻微渲染开销                  | 已做 60ms 限流；必要时可切换"生成中纯文本、完成后 Markdown" |
| R-03 | 用户消息暂不渲染 Markdown         | 含 Markdown 语法的用户输入原样展示  | 蓝底气泡上 Markdown 样式观感差，待定方案              |
| R-04 | IME 组合态 Enter 语义          | 中文输入首条 Enter 用于上屏（标准行为） | 已按主流产品惯例处理，文档明示                        |

## 8. 版本历史（需求迭代记录）

| 版本节点         | 需求内容                                                                                                              |
| ------------ | ----------------------------------------------------------------------------------------------------------------- |
| V1.8 内置文件工具与写确认卡 | 新增 FR-13：`read_file`（行号分页 / 单行截断 / 二进制与超大拒绝）与 `write_file`（覆盖写 / 自动建目录 / UTF-8 无 BOM）两个内置工具，`WorkspaceScope` 白名单（运行目录 + config.json 可选 `WorkspaceRoots`，动态生效）+ `config.json` 拒绝名单（防 API Key 泄露）；`write_file` 写前经 `IConfirmationService` 人工确认——聊天窗插确认卡（路径 / 预览 / 同意·拒绝），隐藏时自动弹出，拒绝 / 超时 120s / 停止均不落盘；确认卡不入会话 json；console 探针 26 用例 + GUI 钩子（截图 + 自动同意 approved=True）双验证；版本升至 1.8.0 |
| V0.1 骨架      | 悬浮球 + 点击弹出聊天窗（.NET 10 WPF，无边框置顶可拖拽）                                                                               |
| V0.2 框架      | 引入 CommunityToolkit.Mvvm 8.4.2、依赖注入 10.0.12                                                                       |
| V0.3 对话      | 接入 DeepSeek 实现流式聊天                                                                                                |
| V0.4 Agent 化 | MAF 重构；Enter 发送/Shift+Enter 多行；配置窗（必填校验变红、config.json）                                                            |
| V0.5 交互修复    | 测试连接按钮；字段清空标红；发送→停止按钮（参考图风格、图标居中）                                                                                 |
| V0.6 配置增强    | 模型下拉框（4 个预置项）；发送按钮与标题栏风格统一                                                                                        |
| V0.7 渲染      | MdXaml 渲染；双方消息可复制；AI 消息工具栏（复制/token/模型）                                                                           |
| V0.8 尺寸与细节   | 窗口宽高 ×1.2、高度再 ×1.3（456×811）；AI 字体 Compact；细窄滚动条                                                                   |
| V0.9 平台化     | Host.CreateApplicationBuilder 启动；skills 技能体系（grill-me/skill-creator/get-weather）；IAIToolService 工具注入（get_weather） |
| V0.10 体验优化   | 滚轮缓动 + 流式跟随滚动 + 限流刷新；修复 Enter 不发送（TextBox 类处理器抢占问题）                                                               |
| V1.0 工程化     | 拆分三工程：Floaticon 启动壳 / FloatAgent 类库 / FloatAgent.Abstractions 接口                                                  |
| V1.1 长任务与安全 | 新增 run_long_task 长耗时工具（固定 2 分钟、可取消）与新 FR-10 工具执行进度反馈（气泡内实时显示 N/120 秒）；移除全部窗口强制置顶；API Key 改为掩码输入、仅从 config.json 读取（代码零硬编码，新增 config.example.json 模板并忽略 config.json）；模型下拉框水平拉伸与输入框等宽；测试连接期间保存按钮禁用态可视化；配置窗底部移除路径展示 |
| V1.2 容器与工程化 | 新增 FR-08 第 7 项 run_container_code（DockerShellExecutor 一次性 Linux 容器执行 C#/Python/JS，离线编译、网络隔离、周期进度上报）；PeriodicProgressService 构造函数私有化、改由 Create 工厂创建；工程元数据集中到 Directory.Build.props（版本 1.2.0→1.3.0）；新增 build.cmd/build.ps1（win-x64 自包含单文件发布 + NuGet 打包到 artifacts）；新增 GitHub Actions CI 与 NuGet 自动发布、MIT LICENSE、英文 README；三工程目录迁入 src/ |
| V1.3 交互修复与发版同步 | FR-05 第 5 项：用户气泡悬浮操作行的悬浮判定扩到整条消息行，指针从气泡移向复制按钮途经间隙时操作行不再消失、按钮可正常点击（可点区域 26×18）；版本升至 1.3.1，README（中/英）与 PRD、NuGet 包随代码同批更新发布 |
| V1.4 RecipeDemo 演示外壳（衍生需求） | 新增独立演示工程 `src/RecipeDemo`（产线配方管理界面预演）：左侧配方树（关键字过滤 / Collapse 折叠 / 右缘蓝条收起）+ 右侧五个独立 UserControl+ViewModel 视图 Summary / Wafer Map / Alignment / Tests / Recipe-Run Parameters；点击配方树后 Summary 显示「当前选择的配方是：***」；Wafer Map 初始仅「Create Wafer Map」按钮；Alignment 为 Edge / Coarse / Fine 箭头式子标签（Caculate、Create Template + Run）+ 右下角 Save；RecipeDemo 引用 FloatAgent / FloatAgent.Abstractions，主窗打开时同步显示悬浮球（可弹出聊天窗）；自带 skills 目录（构建时复制到 exe 旁）；版本升至 1.4.0 |
| V1.5 RecipeDemo 技能与 AI 工具打通 | 内置 `skills/alignment/` 配方拉平标准作业流程技能（4 阶段 12 动作：打开配方 → 圆心定位 → 粗定位 → 精定位 → Updating LLC → 保存，含人工确认卡点）；对应 11 个 AI 工具由 ViewModel 注入 `IAIToolService` 并 `AddTool` 注册：`select_recipe` / `open_alignment`（MainViewModel，配方树查找与标签页切换）与其余 9 个（AlignmentViewModel：switch_to_edge/coarse/fine_alignment、calculate_wafer_center、create_recipe_template、run_coarse_align、run_fine_align、define_llc、save_recipe）；工具在后台线程调用，经 `AppHelper.DispatcherInvoke` 同步投递 UI 线程改 ObservableProperty；失败分支（视图不对 / 模版未创建 / 未选配方）按真实规则拦截；已真机验证 11 工具全链路（含失败分支）；版本升至 1.5.0 |
| V1.7 拉平技能实测优化与工具执行日志 | 依据 2026-10-27 实际运行（会话 debe28a5）暴露的问题优化 alignment 技能：核心原则新增 8「事实只认两个来源」（最近一次工具返回原文或用户明确告知）、9「被质疑时先定事实，不迎合」（迎合式认错与编造同罪，用幂等重跑定事实）、10「返回值必须如实引用」（结果汇报不得写在工具返回之前，返回值不确定须重调确认）；反例补 3 条（迎合式改口 / 预期当结果 / 凭印象转述）；代码侧新增 FR-12 工具执行日志：`LoggedAIFunction` 包装所有注册工具，调用自动写 `ToolCallLog`（会话内 200 条上限），内置 `query_tool_history` 供模型查证调用历史，新建会话清空；alignment 的 SOP.md 与技能正文重复，不保留（如需人工操作卡后续放 docs/）；版本升至 1.7.0 |
| V1.7.2 会话保存失败走标准日志 | ChatViewModel 注入 `ILogger<ChatViewModel>`（Microsoft.Extensions.Logging，宿主 Host builder 默认提供）：Persist 捕获异常由 Debug.WriteLine 升级为 `_logger.LogError(ex, ...)`（带会话文件名与完整异常堆栈），磁盘满/权限不足等错误可经 Debug/Console/EventSource 等日志提供程序观测，仍不打断对话；版本升至 1.7.2 |
| V1.7.1 标题栏按钮 hover 美化 | 聊天窗标题栏三个按钮（＋ 新建会话 / ⚙ 配置 / ✕ 关闭）原为默认 Button 模板，悬停出现系统默认浅灰高亮，与靛蓝标题栏（#6366F1）撞色：新增 `TitleBarButtonStyle` 自定义模板，＋/⚙ 悬停改半透明白叠加（#26FFFFFF，按下 #40FFFFFF）圆角 6；关闭按钮用 `TitleBarCloseButtonStyle`，悬停红色高亮（#E81123）并转白字（Windows 惯例）；配置窗关闭按钮同步套用；版本升至 1.7.1 |
| V1.6 会话自动保存 | 新增 FR-11：每次新对话按会话 guid 持久化为运行目录 `sessions/<guid>.json`（ISessionStore / JsonSessionStore 单例，`.tmp` 原子替换覆盖写，目录自动创建）；内容含标题（首条用户消息前 24 字）与逐条 role / text / timestamp / token 用量 / 模型名；落盘时机为用户消息发送后与每轮问答结束（含停止 / 出错），失败只记日志不打断对话；聊天窗标题栏新增「＋」新建会话按钮（生成期间禁用，新 guid 新文件）；`sessions/` 加入 .gitignore；ChatSession.Messages 用 JsonObjectCreationHandling.Populate 保证反序列化可回填；已真机验证（启动即建文件 / 三消息完整 / 新会话换 guid 旧文件不变）；版本升至 1.6.0 |
