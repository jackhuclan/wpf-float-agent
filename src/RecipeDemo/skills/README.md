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

当前已放置一个示例技能 `recipe-helper/`，后续按需新增目录即可。
