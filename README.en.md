# Floaticon · Floating AI Assistant for Windows Desktop

English | [简体中文](README.md)

[![NuGet](https://img.shields.io/badge/dynamic/xml?url=https%3A%2F%2Fraw.githubusercontent.com%2Fjackhuclan%2Fwpf-float-agent%2Fmain%2FDirectory.Build.props&query=%2FProject%2FPropertyGroup%2FVersion&prefix=v&label=nuget&logo=nuget)](https://github.com/jackhuclan/wpf-float-agent/pkgs)
[![CI](https://github.com/jackhuclan/wpf-float-agent/actions/workflows/ci.yml/badge.svg)](https://github.com/jackhuclan/wpf-float-agent/actions/workflows/ci.yml)
[![publish-nuget](https://github.com/jackhuclan/wpf-float-agent/actions/workflows/publish-nuget.yml/badge.svg)](https://github.com/jackhuclan/wpf-float-agent/actions/workflows/publish-nuget.yml)
[![License: MIT](https://img.shields.io/github/license/jackhuclan/wpf-float-agent)](https://github.com/jackhuclan/wpf-float-agent/blob/main/LICENSE)

A lightweight AI entry point that lives in the bottom-right corner of your Windows desktop: a draggable floating ball — click it and a streaming chat window pops up, with built-in Markdown rendering, token usage stats, a Skills and Tools system, and fully configurable model providers.

Built on **.NET 10 WPF** and the **Microsoft Agent Framework (MAF)**, with zero third-party UI framework dependencies in the presentation layer (everything is custom-drawn).

```
┌──────────────┐   click   ┌─────────────────────────────┐
│ FloatingBall │ ────────▶ │  AI Assistant       ⚙   ✕  │
│  60px drag   │ ◀──────── │ ─────────────────────────── │
└──────────────┘  collapse │  Messages (Markdown, stream)│
                           │ ─────────────────────────── │
                           │  Input box               ➤  │
                           └─────────────────────────────┘
```

## Features

| Capability | Description |
| --- | --- |
| Persistent floating ball | 72×72 borderless transparent window, no taskbar entry; drag to move — movement ≤4 DIP counts as a click |
| Streaming chat | Token-by-token streaming; the body switches to live output as soon as the first chunk arrives; stop anytime while generating |
| Markdown rendering | Headings, bold, code blocks, quotes, lists; compact layout (12px body text) |
| Message toolbar | Each AI reply shows a copy button / `◇ N tokens used` / `⚡ model name` (usage accumulates across multi-turn tool calls) |
| User message hover bar | Hovering a user bubble reveals `11:25` (or `10月24日 09:50` for older days) plus a copy button below it, right-aligned; the hover area spans the whole message row, so the button no longer disappears while you move toward it — click to copy the original text |
| Model configuration | Three-field form + live required-field validation + **Test Connection** (clearly distinguishes success / auth failure / unreachable); saving takes effect immediately, no restart needed |
| Skill system | `SKILL.md` files under the `skills/` directory are auto-discovered; the model loads them on demand via `load_skill` (progressive disclosure) |
| Tool system | C# functions registered as AIFunctions through `IAIToolService`, invoked automatically by the Agent (`get_weather` / `run_long_task` / `run_container_code`) |
| Scrolling experience | 6px slim scrollbar, pixel-based scrolling, 170ms ease-out wheel animation, smart follow-to-bottom during streaming |
| Tool progress feedback | Long-running tools report progress in real time (e.g. `⚡ run_long_task running… 60 / 120 s`), shown under the "typing" bubble — no more silent waiting |
| Session auto-save | Every new conversation is saved as `sessions/<guid>.json` next to the exe (atomic overwrite; role / text / time / token usage / model name per message, title from the first user message) |
| Session management drawer | A「☰」button on the left of the title bar slides out an overlay drawer (220ms animation + translucent mask, click the mask to close): new conversation / open a history session (messages fully restored with roles & timestamps) / inline rename (Enter to confirm, Esc to cancel, blur commits; renamed titles are no longer overwritten by the auto title) / delete with in-place confirmation (deleting the current session auto-switches to the latest one, or creates a new one when the list is empty); list items show "title + N messages · relative time" (today / yesterday / M月d日 / cross-year, full time on hover), current session highlighted |
| Built-in file tools | `read_file`: line-numbered paged reads inside the workspace whitelist (first 2000 lines by default, resumable; overlong lines truncated; binary/oversized files rejected); `write_file`: shows a confirmation card in the chat window before writing (path + content preview + approve/deny buttons); on approval overwrites the file (parent dirs auto-created, UTF-8 no BOM). Scope = the app directory + `WorkspaceRoots` from `config.json` (effective immediately); `config.json` itself (contains the API key) is denied for both read and write; `..` traversal blocked |
| Multi-monitor support | PerMonitorV2 DPI awareness; popup positions are clamped to the current working area |

## Architecture

Three projects with a one-way, non-invertible dependency direction:

![Visual Studio solution structure](docs/solution-projects.png)

```
Floaticon (WinExe startup shell)
  │  Assembly only: build Host → register DI → Start → show floating ball
  ▼
FloatAgent (business class library, WPF)
  │  Models / Services / Tools / ViewModels / Views / skills
  ▼
FloatAgent.Abstractions (interface library)
     IAIToolService (AddTool / AsTools)
```

**Key design decisions**

- **Startup**: `Host.CreateApplicationBuilder` builds the generic host; `StopAsync + Dispose` releases resources on exit.
- **Class libraries never reference the startup project**: views inside the library resolve transient services through `ServiceLocator.Services` (assigned by `App` after the host is built).
- **Configuration takes effect immediately**: `AgentChatService` rebuilds the Agent from `IConfigService.Current` on every request, so the next message after saving uses the new model.
- **Extension points for skills and tools**: adding a skill only requires a new directory; adding a tool only requires implementing an `AIFunction` and registering it — the chat core pipeline stays untouched.

### Request pipeline

```
ChatViewModel
  └─ IChatService.StreamReplyAsync(history)
       └─ AgentChatService
            ├─ Create OpenAIClient → ChatClient → AsAIAgent from current config
            ├─ ChatOptions.Instructions  = SystemPrompt
            ├─ ChatOptions.Tools         = IAIToolService.AsTools()
            └─ AIContextProviders        = AgentSkillsProvider (skills directory)
                 │
                 ▼  MAF RunStreamingAsync
            StreamChunk(Text / Usage) ──▶ incremental bubble rendering (60ms throttle)
```

## Repository layout

```
wpf-float-agent/
├─ src/Floaticon/                    # Startup shell (WinExe WPF)
│  ├─ App.xaml / App.xaml.cs     # Host build, DI assembly, ServiceLocator assignment
│  ├─ app.manifest               # PerMonitorV2 DPI awareness
│  └─ Properties/AssemblyInfo.cs
├─ src/FloatAgent/                   # Business class library
│  ├─ Models/                    # ChatTurn, TokenUsage, ModelConfig
│  ├─ Services/                  # AgentChatService, JsonConfigService, AIToolService,
│  │                             # OpenAIModelTester, SkillProviderFactory, ServiceLocator
│  ├─ Tools/                     # WeatherTool (get_weather, Open-Meteo)
│  │                             # LongRunningTool (run_long_task, 2-minute long task)
│  │                             # ContainerCodeTool (run_container_code, C#/Python/JS in Linux containers)
│  ├─ ViewModels/                # ChatViewModel, ConfigViewModel, ChatMessage
│  ├─ Views/                     # FloatingBallWindow, ChatWindow, ConfigWindow
│  │  └─ Behaviors/              # SmoothScrollBehavior (wheel easing)
│  └─ skills/                    # grill-me, skill-creator, get-weather
├─ src/FloatAgent.Abstractions/      # IAIToolService, ToolProgressService
├─ src/RecipeDemo/                   # RecipeDemo demo shell (recipe tree + 5 views + floating ball)
├─ docs/                         # PRD (md + docx)
├─ output/                       # Intermediate artifacts of the PRD generation pipeline
├─ Directory.Build.props         # Solution-wide metadata (version/author/URL/description)
├─ Directory.Packages.props      # Central NuGet package version management
├─ build.cmd / build.ps1         # One-click Release build + NuGet packing (output → release\ / artifacts\)
├─ AGENTS.md / .editorconfig     # Project conventions and code style
└─ Floaticon.slnx
```

## Requirements

- Windows 10 / 11
- [.NET SDK 10.0](https://dotnet.microsoft.com/download) (developed with 10.0.402)
- An OpenAI-compatible model endpoint (defaults tuned for DeepSeek; local Ollama qwen models also work)

## Quick start

```bash
git clone https://github.com/jackhuclan/wpf-float-agent.git
cd wpf-float-agent

dotnet build Floaticon.slnx

# Run (or simply double-click src/Floaticon/bin/Debug/net10.0-windows/Floaticon.exe)
dotnet run --project src/Floaticon
```

After startup, a floating ball appears in the bottom-right corner of the desktop. **Single-click** it to open the chat window; **right-click** to open the chat or exit.

### Build and package

One-click build scripts live at the repository root (`build.cmd` is the command-line entry to `build.ps1`; arguments are passed through):

```bat
:: Self-contained single-file publish for win-x64 (.NET runtime included; no .NET install needed on target machines)
:: Output: release\Floaticon-<version>.exe (version read from Directory.Build.props)
build.cmd

:: Pack NuGet packages (FloatAgent, FloatAgent.Abstractions) into artifacts\
build.cmd -Pack

:: Pack with a version suffix (produces FloatAgent.1.3.1-preview.nupkg)
build.cmd -Pack -VersionSuffix preview

:: Clean release\ / artifacts\ before publishing
build.cmd -Clean
```

> Under the hood the single-file publish runs: `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`. The first launch of the exe extracts native libraries to a temp directory, so it is slightly slower — this is normal. The skills directory and config.json still follow the "working directory" convention and live next to the exe.

Version, author, project URL, description, and other project metadata are maintained centrally in the root `Directory.Build.props` (auto-imported by all .csproj files) — a single place to bump a release.

> Note: `MdXaml` (2.0.0-pre) and `Microsoft.Agents.AI.Tools.Shell` (1.25.0-preview), which FloatAgent depends on, are pre-release packages, so packing stable NuGet packages raises NU5104 warnings. This does not affect package generation or usage.

### CI / NuGet auto-publish (GitHub Actions)

Two workflows ship with the repo:

- [`.github/workflows/ci.yml`](.github/workflows/ci.yml) — **CI**: on every push to `main` or PR (documentation-only commits are skipped automatically) it runs Restore → Release build → pack smoke test (verifying both library packages are produced at the current version). It does not publish — verification only.
- [`.github/workflows/publish-nuget.yml`](.github/workflows/publish-nuget.yml) — **Publish**: on every push to `main` (or a manual dispatch, or a published Release) it packs `FloatAgent` / `FloatAgent.Abstractions` automatically and publishes them to **GitHub Packages** (`https://nuget.pkg.github.com/jackhuclan/index.json`). Publishing the same version again is skipped; a **version gate** also fails the pipeline when the latest pushed commit touches code (`src/`, `*.csproj`, props, `Floaticon.slnx`) without bumping `<Version>` in `Directory.Build.props`, guaranteeing every code update ships a new versioned package.

Installing from GitHub Packages (a GitHub PAT is required, even for public packages):

```bash
dotnet nuget add source \
  --username <GitHub username> --password <PAT> --store-password-in-clear-text \
  --name github "https://nuget.pkg.github.com/jackhuclan/index.json"

dotnet add package FloatAgent --source github
```

### Configure the model

The API key is read only from `config.json` — **no secrets are kept in code**. On first launch the config file does not exist, so all three fields show red-border required prompts once you open **⚙**:

- **Recommended**: fill in the three fields in the config window → click "Test Connection" to verify → save. The app writes the configuration to `config.json` next to the exe.
- **Manual**: copy `config.example.json` to `config.json` in the exe directory, fill in the key, and restart the app.

| Field | Description | Example |
| --- | --- | --- |
| API Key | Endpoint key (required, no default, masked while typing) | `sk-...` |
| Model | Dropdown selection; a configured value missing from the list is added automatically | `deepseek-chat` |
| Endpoint | OpenAI-compatible endpoint | `https://api.deepseek.com/v1` |

The configuration is saved as **`config.json` in the working directory** (the working directory is anchored to the exe folder at startup, so both double-click and command-line launches resolve to the same location). See `config.example.json`:

```json
{
  "ApiKey": "sk-...",
  "Model": "deepseek-chat",
  "Endpoint": "https://api.deepseek.com/v1"
}
```

> ⚠️ `config.json` is listed in `.gitignore` and the API key is stored in plain text — never commit or share it. The repository only contains `config.example.json` without real secrets. If the config file is missing or corrupted, the app falls back to defaults (empty API key) without blocking startup; just re-enter the settings.

## Extending

### Add a skill

Create a directory under `src/FloatAgent/skills/` with a `SKILL.md` (YAML frontmatter + instructions):

```markdown
---
name: my-skill
description: One sentence on when this skill applies (the model uses this to decide whether to load it)
---

# My Skill
Concrete steps…
```

After rebuilding and running, the model first sees the skill directory summary and loads the full text via `load_skill` when needed. Optional resource files (`.md`/`.json`/`.yaml`/`.yml`/`.txt`) are read through `read_skill_resource`; for security reasons scripts inside skills are **never executed**.

### Add a tool

```csharp
public sealed class MyTool
{
    [Description("What the tool does — the model uses this to decide when to call it")]
    public async Task<string> DoSomethingAsync(
        [Description("Parameter description")] string input,
        CancellationToken cancellationToken = default) { /* ... */ }
}

// Register in RegisterTools (src/Floaticon/App.xaml.cs)
Func<string, CancellationToken, Task<string>> fn = tool.DoSomethingAsync;
registry.AddTool(AIFunctionFactory.Create(fn, "do_something"));
```

> Receive the method group with an explicit delegate type so the `[Description]` attributes on the method and parameters survive — that's how the model gets parameter descriptions.

> For long-running tools, inject the `ToolProgressService` (DI singleton) and call `Report("…")` periodically; progress shows live under the "typing" bubble instead of a long silence. See `LongRunningTool` for a reference implementation.

**Wrapping third-party long-running methods you cannot modify**: when the third-party code cannot report progress internally, use `PeriodicProgressService` to report from the waiting side (1× on start → 1× every 5 s → 1× on completion) with zero changes to the third-party code:

```csharp
// In RegisterTools (src/Floaticon/App.xaml.cs); progressService resolves ToolProgressService from the container
var periodic = PeriodicProgressService.Create(
    progressService, "thirdparty_long_run_task", ThirdPartyLib.LongRunAsync);

// RunAsync takes only a CancellationToken; register with a method group, not a lambda (lambdas lose the cancellation wiring)
Func<CancellationToken, Task<string>> run = periodic.RunAsync;
registry.AddTool(AIFunctionFactory.Create(run, "thirdparty_long_run_task", "Long-running third-party task"));
```

The constructors are private — create instances through the `Create` factory methods. Four third-party shapes are supported: `Func<CancellationToken, Task>`, `Func<Task>`, `Action` (runs on the thread pool), and `Func<CancellationToken, Task<string>>` (the return value is appended to the completion report). Exceptions thrown by the third party are converted into readable failure text returned to the model; clicking "Stop" still cancels within the reporting interval.

## Built-in skills and tools

| Name | Type | Purpose |
| --- | --- | --- |
| `grill-me` | Skill | Plan interrogation: treats a vague plan as a decision tree and asks numbered questions front by front; outputs a consensus summary when the frontier is cleared |
| `skill-creator` | Skill | Skill-authoring methodology: create, improve, and evaluate skills |
| `get-weather` | Skill | Weather-query protocol: forces tool-based data, forbids fabrication, supports multiple cities and repeated calls, asks back when the city is unspecified |
| `get_weather` | Tool | Open-Meteo live weather + 3-day forecast (no API key needed), WMO codes mapped to descriptions |
| `run_long_task` | Tool | Long-running task (fixed 2 minutes): 1-second sliced waits, mid-run cancellation, progress reported every 5 seconds; returns task name / start-end times / actual duration. Used to verify the Agent stays responsive while a tool occupies it |
| `thirdparty_long_run_task` | Tool | Simulated third-party long task (~15 s, cannot report progress internally): `PeriodicProgressService` reports progress periodically from the waiting side and returns the result; validates the third-party progress-wrapper pipeline |
| `run_container_code` | Tool | Based on `DockerShellExecutor` from `Microsoft.Agents.AI.Tools.Shell`, runs C# / Python / JavaScript in a disposable Linux container (no runtime needed on the host): scripts via path or inline code, written in as base64 before execution, network isolation + 1 GiB memory cap + 180 s timeout, progress every 5 seconds; for C# a mini offline-restoreable project is generated inside the container (no network access); first run needs to pull images |

## Tech stack

| Component | Version | Purpose |
| --- | --- | --- |
| .NET / WPF | net10.0-windows | Desktop framework |
| Microsoft.Agents.AI(.OpenAI) | 1.25.0 | Agent, skill orchestration, streaming chat |
| Microsoft.Extensions.AI.Abstractions | 10.10.1 | AIFunction / ChatMessage abstractions |
| CommunityToolkit.Mvvm | 8.4.2 | ObservableObject / RelayCommand |
| MdXaml | 2.0.0-pre | Markdown → FlowDocument rendering |
| Microsoft.Extensions.Hosting | 10.0.12 | Generic host and dependency injection |

## Known limitations

- User messages render as plain text (Markdown looks poor on the blue bubble; approach TBD).
- Full-document Markdown re-layout during long replies has minor overhead, mitigated by a 60ms throttle.
- With a Chinese IME, the first Enter commits the candidate text — this is expected; only the next Enter sends.
- No single-instance enforcement; launching twice shows two floating balls.

## RecipeDemo demo shell

`src/RecipeDemo` is a standalone demo project (window title: RecipeWindow) previewing a production-line recipe management UI:

- **Left recipe tree**: search box filters by name; the Collapse button beside the "Recipes" root collapses everything; a small blue tab on the right edge hides/shows the panel.
- **Five views on the right** (each an independent UserControl + ViewModel): Summary / Wafer Map / Alignment / Tests / Recipe-Run Parameters, switched via the top tab bar.
- Clicking a recipe in the tree makes **Summary show "当前选择的配方是：***"** (the currently selected recipe); Wafer Map initially only shows a "Create Wafer Map" button that renders the wafer map on click; Alignment hosts arrow-shaped sub-tabs Edge / Coarse / Fine (Caculate, Create Template + Run) with a Save button at the bottom-right.
- **Skills wired to AI tools**: `skills/alignment/` ships the standard recipe alignment workflow (select recipe → wafer center → coarse → fine → Updating LLC → save, with human-confirmation gates). The 11 matching AI tools are registered in the ViewModels — `select_recipe` / `open_alignment` in `MainViewModel`, the other 9 (view switching, wafer center, template creation, coarse/fine align, define_llc, save_recipe) in `AlignmentViewModel` — via constructor-injected `IAIToolService.AddTool`. Tools are invoked on background threads and marshal UI state changes through `AppHelper.DispatcherInvoke`; failure branches (wrong view / missing template) are enforced like real rules.
- **Tool execution log**: every tool call is recorded automatically (time / name / arguments / result / duration); the model can verify "was this step actually executed" via the built-in `query_tool_history` tool instead of asserting call history from memory.
- When the main window opens, the FloatingBall from FloatAgent is shown as well; clicking the ball opens the chat window. Skills live in `src/RecipeDemo/skills/` and are copied next to the exe at build time.

```bash
dotnet run --project src/RecipeDemo
```

## Changelog

| Version | Date | Changes |
| --- | --- | --- |
| 1.8.0 | 2026-10-27 | Built-in file tools: `read_file` (line-numbered paged reads, overlong-line truncation, binary/>10MB rejection) and `write_file` (confirmation card in the chat window before writing; on approval overwrites with parent-dir auto-creation, UTF-8 no BOM); `WorkspaceScope` whitelist (app directory + optional `WorkspaceRoots` in `config.json`, effective immediately), `config.json` denied for read & write to protect the API key; confirmation cards excluded from session json; verified by a 26-case console probe plus a GUI hook |
| 1.9.3 | 2026-10-28 | Session drawer simplification: ① list item subtitle no longer shows "N messages" — only the relative date remains; ② deleting a session no longer creates a new one — deleting the current session directly loads the latest remaining one (no disk write), and when the list becomes empty the chat area resets to an unsaved blank session (written to disk only on the first message), eliminating the spurious "新会话" file created during deletes; verified by a two-phase TEMP-DEBUG probe (switched=True / finalFiles=0 / blank=True) |
| 1.9.2 | 2026-10-28 | Session drawer polish: ① the「…」dropdown / right-click menu stayed open after the drawer closed — `SessionHistoryPanel` now closes all open Popups/ContextMenus and resets the toggle when it becomes invisible; ② the drawer list scrollbar differed from the ChatWindow message area — the thin scrollbar styles were extracted into a shared `ThinScrollBarStyles.xaml` dictionary referenced by both |
| 1.9.1 | 2026-10-28 | Fixed drawer width: `DrawerOverlay` had no column definitions, so the panel stretched across the full chat area (100% width); added a two-column structure matching the parent so the panel is fixed at 50% width with a 30% black mask over the whole chat area |
| 1.8.1 | 2026-10-27 | Refactor: the write-confirmation card was extracted from ChatWindow into a standalone `WriteConfirmationCard` UserControl with its own `ConfirmationCardViewModel` (approve/deny commands and card state now live in the card VM; ChatViewModel only enqueues the request); behavior and UI unchanged |
| 1.9.0 | 2026-10-27 | Session management drawer (requirements extracted from the MegaAgent PRD FR-3/FR-4/B15): a「☰」on the left of the title bar (replacing the decorative dot) slides out an overlay drawer — new conversation / open history sessions (`ISessionStore` gains `List/Load/Delete/Rename`; messages are restored with roles & timestamps on switch) / inline rename (select-all by default, Enter commits, Esc cancels, blur commits; renamed titles are no longer auto-overwritten) / in-place delete confirmation (deleting the current session auto-switches to the latest, or starts a new one when empty); list items show "title + N messages · relative time" with the current session highlighted; the title-bar「＋」was removed — new conversations live in the drawer; corrupted session files are skipped without blocking the list |
| 1.7.2 | 2026-10-27 | Standard logging for session save failures: ChatViewModel now injects `ILogger<ChatViewModel>` (Microsoft.Extensions.Logging); the Persist failure path was upgraded from Debug.WriteLine to `LogError` (with the session file name and the full exception), picked up automatically by the host's default logging pipeline (Debug/Console/EventSource) |
| 1.7.1 | 2026-10-27 | Polished the chat window title bar button hover states: ＋/⚙ now use a translucent white overlay on hover (darker when pressed), and the close button uses the conventional Windows red highlight with white foreground; the config window close button got the same fix, removing the default gray system highlight that clashed with the indigo title bar |
| 1.7.0 | 2026-10-27 | Alignment skill refined from a real run: three new core principles ("facts come only from tool returns or user statements / settle disputes by verifying, never capitulate / quote tool returns verbatim") with matching anti-patterns (no fabricated completions, no sycophantic reversals, no improvised result paraphrasing); new tool execution log — every call recorded (time / name / args / result / duration) with a built-in `query_tool_history` tool so the model can verify call history; log clears on a new session |
| 1.6.0 | 2026-10-27 | Session auto-save: each new conversation writes `sessions/<guid>.json` keyed by the session guid (ISessionStore / JsonSessionStore, atomic overwrite, role / text / time / token usage / model name, title from the first user message); new 「＋」 button in the chat title bar starts a new session under a new guid; the file is written after the user message lands and after each completed round |
| 1.5.0 | 2026-10-27 | RecipeDemo skills wired to AI tools: the alignment workflow skill (4 stages / 12 actions with human-confirmation gates) plus 11 tools (select_recipe / open_alignment / switch_to_edge_alignment / calculate_wafer_center / switch_to_coarse_alignment / create_recipe_template / run_coarse_align / switch_to_fine_alignment / run_fine_align / define_llc / save_recipe) registered by MainViewModel / AlignmentViewModel via IAIToolService; background-thread tool calls marshal UI updates through the Dispatcher |
| 1.4.0 | 2026-10-27 | Added the RecipeDemo demo shell: recipe tree (search/collapse/hide) + Summary / Wafer Map / Alignment / Tests / Recipe-Run Parameters views; clicking a recipe updates Summary; Wafer Map reduced to a Create Wafer Map button; Alignment hosts Edge/Coarse/Fine arrow sub-tabs with Save; the FloatAgent floating ball shows alongside the main window; ships with a skills folder |
| 1.3.2 | 2026-10-26 | publish-nuget adds a version gate: pushes that change code without bumping the version now fail, so every code update ships a new versioned NuGet package |
| 1.3.1 | 2026-10-26 | Fixed the user bubble hover bar: the hover area now spans the whole message row, so the copy button no longer vanishes while moving from the bubble toward it (including the 4px gap); click target enlarged to 26×18 |
| 1.3.0 | 2026-10-24 | Version metadata centralized in `Directory.Build.props`; added the `run_container_code` container execution tool and the `PeriodicProgressService` factory API |
| 1.2.0 | 2026-10-24 | Three projects moved under `src/`, English README, MIT LICENSE, GitHub Actions CI and automatic NuGet publishing |
| 1.1.0 | 2026-10-23 | Long-running tool with progress reporting, skill system, tool injection, smooth wheel scrolling and streaming auto-follow |

Release rule: **every code change updates both READMEs, the PRD and the version in `Directory.Build.props`, regenerates versioned NuGet packages into `artifacts\` with `build.cmd -Pack`, and commits/pushes all of them together** (publish-nuget skips duplicate versions, so the version must be bumped).

## Documentation

- [Product Requirements (PRD) V1.3, file name kept at v1.1](docs/Floaticon-PRD-v1.1.md) (Chinese) · [docx archive (v1.0)](docs/Floaticon-PRD-v1.0.docx)
- [Alignment skill flowchart (Mermaid: 4 stages, 12 actions, manual gates)](docs/alignment-flow.md) (Chinese)

## License

This project is open-sourced under the [MIT](LICENSE) license.
