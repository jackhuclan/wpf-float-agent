# Alignment（配方拉平）流程图

本图依据 `src/RecipeDemo/skills/alignment/SKILL.md`（技能正文，4 阶段 12 动作）整理，用于快速把握执行顺序、分支与人工卡点。技能正文为唯一事实来源，两者冲突时以 SKILL.md 为准。

## 流程图

渲染版（另有 Mermaid 源码见下节，GitHub 可直接渲染）：

![拉平技能流程图](alignment-flow.png)

### Mermaid 源码

```mermaid
flowchart TD
    A([用户请求：拉平某配方]) --> B{配方已在界面打开？<br/>用户已明确指定且可见}
    B -- 是：快速通道 --> P1
    B -- 否 --> C[select_recipe 选择配方]
    C --> D{工具返回值}
    D -->|找到了您需要的配方X| P1
    D -->|找到多个配方| D1[把候选整理成选项<br/>让用户选定，不替用户选]
    D1 --> D2[用用户选定的唯一名称<br/>重新调用 select_recipe]
    D2 --> D
    D -->|没有找到配方| D3[请用户给出准确配方名称]
    D3 --> X1([终止：禁止猜测配方名重试])

    subgraph S1["阶段 1 · 进入拉平界面"]
        P1[提醒后调用 open_alignment] --> P2[汇报：已进入拉平界面]
    end
    P2 --> G1{用户明确确认继续？}
    G1 -- 含糊/提问/纠正 --> G1B[停在原地答疑<br/>不推进]
    G1B --> G1
    G1 -- 明确回复继续 --> H1

    subgraph S2["阶段 2 · 圆心定位 · Edge 视图（全自动，无人工卡点）"]
        H1[switch_to_edge_alignment] --> H2{切换成功？}
        H2 -- 否 --> X2([失败即停：原样转述])
        H2 -- 是 --> H3[calculate_wafer_center]
        H3 --> H4{工具返回值}
        H4 -- 成功找到圆心<br/>载台已自动移到圆心 --> H5[汇报：已自动找到圆心并移动载台]
        H4 -- 自动寻找圆心没有成功 --> H6[原样转述<br/>请用户确认晶圆是否完整在相机视野内]
        H6 --> H7{用户调整后重试<br/>仅允许一次}
        H7 -- 成功 --> H5
        H7 -- 仍失败 --> X3([失败即停])
    end

    H5 --> G2{用户明确确认继续？}
    G2 -- 含糊/提问 --> G2B[停在原地答疑]
    G2B --> G2
    G2 -- 明确回复继续 --> I1

    subgraph S3["阶段 3 · 粗定位"]
        I1[switch_to_coarse_alignment] --> I2[提示：请在画面中框选 ROI<br/>等用户回复]
        I2 --> G3{用户明确回复框选完成？}
        G3 -- 未回复/含糊 --> G3B[不得假定 ROI 已选好<br/>继续等待]
        G3B --> G3
        G3 -- 是 --> I4[create_recipe_template]
        I4 --> I5{模版创建成功？}
        I5 -- 否 --> X4([失败即停])
        I5 -- 是 --> I6[run_coarse_align]
        I6 --> I7{工具返回值}
        I7 -- 成功执行粗定位 --> I8[汇报粗定位成功]
        I7 -- 取消/中断/执行失败 --> X5([失败即停：原样转述])
        I7 -- 工具成功但界面有报错弹窗 --> X5
    end

    I8 --> G4{用户明确确认继续？}
    G4 -- 含糊/提问 --> G4B[停在原地答疑]
    G4B --> G4
    G4 -- 明确回复继续 --> J1

    subgraph S4["阶段 4 · 精定位"]
        J1[switch_to_fine_alignment] --> J2[提示：请在画面中框选 ROI<br/>等用户回复]
        J2 --> G5{用户明确回复框选完成？}
        G5 -- 未回复/含糊 --> G5B[不得假定 ROI 已选好<br/>继续等待]
        G5B --> G5
        G5 -- 是 --> J4[create_recipe_template]
        J4 --> J5{模版创建成功？}
        J5 -- 否 --> X6([失败即停])
        J5 -- 是 --> J6[run_fine_align]
        J6 --> J7{精定位成功？}
        J7 -- 否 --> X7([失败即停：原样转述])
        J7 -- 是 --> L1{{人工确认卡点 · LLC<br/>请人工到精定位界面核对 LLC}}
        L1 -- 含糊/提问/要求调整 --> L1B[停在原地答疑<br/>不得假定已确认]
        L1B --> L1
        L1 -- 明确回复 LLC 已确认 --> L2[提醒后调用 define_llc<br/>提示用户点 OK 确认框]
        L2 --> L3[重绘 WaferMap<br/>载台移动到 Field0 LLC]
        L3 --> L4[save_recipe 保存配方]
        L4 --> L5{工具返回值}
        L5 -- 保存配方成功 --> L6[请用户确认界面无报错弹窗]
        L5 -- 保存配方失败 --> L7[原样转述<br/>检查弹窗如 Please select 2-4 fields]
        L7 --> L8{处理后重试一次}
        L8 -- 成功 --> L6
        L8 -- 仍失败 --> X8([失败即停])
    end

    L6 --> Z[输出完成汇报<br/>配方 / 圆心 / 粗定位 / 精定位 / LLC / 结论]
    Z --> E([结束])

    classDef halt fill:#FEE2E2,stroke:#DC2626,color:#991B1B;
    classDef gate fill:#FEF3C7,stroke:#D97706,color:#92400E;
    classDef ok fill:#DCFCE7,stroke:#16A34A,color:#166534;
    class X1,X2,X3,X4,X5,X6,X7,X8 halt;
    class G1,G2,G3,G4,G5,L1 gate;
    class A,E,Z ok;
```

## 图例

| 形状 / 颜色 | 含义 |
| --- | --- |
| 绿色圆角（起止、完成汇报） | 流程入口与出口 |
| 黄色菱形/六边形 | 人工卡点：必须收到用户**明确**答复才放行（含糊、提问、纠正一律视为未确认） |
| 红色节点 | 「失败即停」出口：工具报错、返回非成功、或工具报成功但界面弹报错窗（核心原则 5 / 7） |
| 白色矩形 | 单个工具调用或提示动作，一次只做一步 |

## 关键卡点一览

| 卡点 | 前置条件 | 放行条件 | 违反后果 |
| --- | --- | --- | --- |
| 阶段 1 → 2 | `open_alignment` 返回成功 | 用户明确回复继续 | 跳步、抢跑 |
| 阶段 2 → 3 | `calculate_wafer_center` 返回成功 | 用户明确回复继续 | 找圆心是唯一全自动步骤，但仍需汇报 |
| 阶段 3 ROI | 已切到 Coarse 视图 | 用户明确回复框选完成 | 未框选就执行会失败或点到错误区域 |
| 阶段 3 → 4 | `run_coarse_align` 返回成功 | 用户明确回复继续 | 粗定位未成功就精定位，结果不可信 |
| 阶段 4 ROI | 已切到 Fine 视图 | 用户明确回复框选完成 | 同上 |
| **LLC** | `run_fine_align` 返回成功 | 用户明确回复「LLC 已确认」 | 未确认即 `define_llc` 会按错误位置重绘 WaferMap 并移动载台 |
| 保存 | `define_llc` 完成 | — | 保存失败需先处理界面弹窗（如 `Please select 2-4 fields！`） |

## 与技能正文的对应关系

- 核心原则 1–4（一次一步、事前提醒、禁止跳步、确认即放行）→ 图中的 `G1`–`G5` 卡点与"停在原地答疑"回边。
- 核心原则 5（失败即停，同工具最多重试 1 次）→ 所有 `X*` 红色出口。
- 核心原则 6（不猜参数）→ 阶段 0 多配方时让用户选定、工具不可用直接终止。
- 核心原则 7（界面弹窗也是结果）→ 阶段 3 的"工具成功但界面有报错弹窗"分支。
- 核心原则 8–10（事实只认工具返回原文 / 被质疑先定事实不迎合 / 返回值必须如实引用）→ 每个 `{工具返回值}` 判定节点，以及汇报必须写在工具返回之后。

相关文件：

- 技能正文：[`src/RecipeDemo/skills/alignment/SKILL.md`](../src/RecipeDemo/skills/alignment/SKILL.md)
- 工具契约与注册：`MainViewModel`（`select_recipe` / `open_alignment`）、`AlignmentViewModel`（其余 9 个）
