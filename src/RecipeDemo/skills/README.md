# RecipeDemo skills

本目录用于存放 RecipeDemo 的技能（SKILL.md），与 Floaticon 的 `src/FloatAgent/skills/` 结构约定一致：

```
skills/
└─ <skill-name>/
   ├─ SKILL.md        # YAML frontmatter（name / description）+ 正文指令
   └─ <resources>     # 可选：.md / .json / .yaml / .yml / .txt 资源文件
```

- `SKILL.md` 的 frontmatter 中 `description` 决定模型何时加载该技能（渐进式披露）。
- 技能内的脚本**不会被执行**，资源文件只读。
- 构建时本目录整体复制到 exe 旁的 `skills/`（见 RecipeDemo.csproj 的 Content Include）。

当前已放置两个技能：

| 技能 | 作用 | 对应 AI 工具（注册位置） |
| --- | --- | --- |
| `recipe-helper/` | 示例技能，演示 SKILL.md 结构 | — |
| `alignment/` | 配方拉平标准作业流程（打开配方 → 圆心定位 → 粗定位 → 精定位 → Updating LLC → 保存），含人工确认卡点 | `select_recipe`、`open_alignment`（MainViewModel）；`switch_to_edge_alignment`、`calculate_wafer_center`、`switch_to_coarse_alignment`、`create_recipe_template`、`run_coarse_align`、`switch_to_fine_alignment`、`run_fine_align`、`define_llc`、`save_recipe`（AlignmentViewModel） |

工具的注册方式：在对应 ViewModel 中注入 `IAIToolService`，构造时调用
`_toolService.AddTool((Func<string>)XXX, "工具名", "说明")`；参数化工具用 `Func<string, string>` 并给方法参数加 `[Description]`。
