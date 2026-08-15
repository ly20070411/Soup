# 汤灵纪行 / Soups & Sprites

Unity 2022.3.12f1 制作的 2D 回合制烹饪经营 roguelite(开发代号「赣什么 / Soup」)。
带领一群小精灵经营魔法厨房:采集食材、加工处理、烹饪成汤,在回合限制内达到目标分数,一路通关章节关卡。

## 玩法概览

- **三段生产流水线**:采集岗产出原料(柔软 / 强韧 / 坚固)→ 处理岗加工为处理食材 → 烹饪岗(小火 / 中火 / 大火,可同时组合)消耗处理食材换取分数。
- **风味系统**:热辣 / 寒冷 / 鲜味即时生效并乘算得分;酸涩不在每回合结算,保留到关底一次性换分。
- **员工**:小精灵可分配到任意岗位;蘑菇人锁定蘑菇采集岗;幽灵不占岗位容量;吱吱只能处理且会偷吃产出;异世界勇者效率极高。
- **关卡与 roguelite 要素**:每关有剧情简报(锅长对话 + 操作规则卡)、通关目标与秘味目标;关间三选一奖励、随机事件、遗物构筑。
- **回合制 + 撤回**:每次「下一回合」按固定管线结算,可撤回上一回合。

## 界面与操作

| 操作 | 说明 |
| --- | --- |
| 鼠标 | 全部 UI 为 IMGUI,鼠标点选 |
| 底部员工栏 | 点选「分配单位」,再用岗位节点的 +/− 分配该类型员工 |
| F1 | 操控面板(资源 / 数值调整 / 遗物 / 岗位进阶 / 分配调试) |
| Esc | 暂停菜单(保存 / 读取 / 返回主菜单 / 退出) |
| Space / Enter | 推进剧情对话 |

界面布局:左侧为资源 / 回合 / 关卡事件窄面板;底部为生产条(员工栏 + 采集 ➜ 处理 ➜ 烹饪 ➜ 通关目标 四段节点,各阶段下方展示产物,目标节点带进度条);关卡开场与通关有 NPC 立绘对话演出。

## 环境与运行

- Unity **2022.3.12f1**,文本编辑器推荐 Rider。
- 打开工程后进入唯一场景 `Assets/Scenes/SampleScene.unity` 直接 Play——几乎所有管理器由 `RuntimeInitializeOnLoadMethod` 在运行时自建,场景只承载少量对象。
- 无测试程序集;无 CLI 构建。
- 注意:`Packages/manifest.json` 引用 `com.coplaydev.unity-mcp`(`file:MCPForUnity`,嵌入式包,不入库)。新克隆机器若缺该目录,删除该依赖行或恢复文件夹即可。

## 打包发布

菜单 `Soup → Build → 打包 Windows EXE`(实现在 `Assets/Scripts/Game/Editor/GameBuildScript.cs`),
自动固定构建场景列表并以 LZ4 压缩输出 x86_64 包;分发时压缩整个输出文件夹。产品名 / 图标等在 `Project Settings → Player` 调整(应用图标由素材链接器从 `Assets/Art/Generated/UI/icon_app.png` 设置)。

## 内容管线(编辑器菜单)

游戏内容(食材 / 岗位 / 员工 / 事件 / 遗物 / 关卡)在 C# 种子器中编写,通过菜单生成或更新 ScriptableObject 资产:

- `Soup/Ingredient Manager/Seed Sample Ingredients`
- `Soup/Job Manager/Seed Sample Jobs`、`Link Gather Jobs By Ingredient Name`
- `Soup/Employee Manager/Seed Employees`
- `Soup/Event Manager/Seed Sample Events`
- `Soup/Relic Manager/Seed All Relics` / `Seed Sample Relics`
- `Soup/Level Manager/Seed Design Levels`
- `Soup/Art Assets/Link Completed Icons` —— 部署生成美术到 `Resources/UI`,绑定食材 / 岗位 / 员工图标,裁剪 9-Slice 素材透明留边,设置应用图标(域重载后自动执行,也可手动跑)

## 目录速览

```
Assets/
  Art/Generated/     生成美术(员工/食材/场景/道具/UI)+ 清单 Documentation/asset_manifest.md
  Data/              ScriptableObject 条目(食材/岗位/遗物/关卡)
  Docs/              设计文档 xlsx 与手绘美术源文件
  Resources/         运行时数据库 + UI 素材(MainMenu 背景、Generated 部署副本)
  Scenes/            SampleScene(唯一场景)
  Scripts/           全部玩法代码,Soup.* 命名空间
```

详细架构说明见 [CLAUDE.md](CLAUDE.md);美术资产清单与接入说明见 [Assets/Art/Generated/Documentation/asset_manifest.md](Assets/Art/Generated/Documentation/asset_manifest.md)。
