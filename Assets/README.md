# Assets 目录说明

Unity 工程资源根目录。各子目录职责与维护规则如下(通用规则:**移动 / 重命名任何资产必须在 Unity Project 窗口内进行**,以保证 `.meta` 与 GUID 同步迁移)。

## Art/ —— 生成美术(源文件)

`Art/Generated/` 存放全部正式生成美术源文件,按类别分目录,统一小写 `snake_case` 命名:

| 子目录 | 前缀 | 内容 |
| --- | --- | --- |
| `Characters/` | `employee_` / `character_` | 员工立绘 5 张;NPC / 剧情角色 9 张(锅长、长老已用于关卡对话演出,其余待接入) |
| `Ingredients/` | `ingredient_` | 食材图标 19 张 |
| `Environments/` | `environment_` | 全幅场景背景(主厨房 / 皇宫 / 巨人山洞已绑定关卡;主视觉用于主菜单;其余待新关卡) |
| `Props/` | `prop_` | 岗位道具(采集地 / 加工台 / 灶台等,用于生产条分区与资源图标) |
| `UI/` | `ui_` / `flavor_` / `logo_` / `icon_` | 9-Slice 面板与按钮、风味图标、LOGO、启动图标 |
| `Documentation/` | — | 资产清单 `asset_manifest.md`(含接入说明与生成提示词) |

**运行时不直接引用本目录**——实际使用的副本由 `Soup/Art Assets/Link Completed Icons`
(实现:`Scripts/Game/Editor/ArtIconLinker.cs`,域重载自动执行)复制到 `Resources/UI/`,
并按用途设置导入尺寸、裁剪 9-Slice 素材透明留边、绑定数据图标。

## Data/ —— 游戏内容条目

各模块的 ScriptableObject 资产(`Ingredients/ Jobs/ Relics/ Levels/ Events/`),
由 `Scripts/<模块>/Editor/` 下的种子器菜单创建或更新;改内容 = 改种子器代码后重跑菜单。

## Docs/ —— 设计文档与手绘源

`"赣什么"游戏.xlsx` 设计文档;`美术素材/完成后上传/` 为手绘完成稿源目录
(ArtIconLinker 会自动把其中 UI 件复制到 Resources 并绑定图标,生成素材优先级更高)。

## Resources/ —— 运行时加载

`Resources.Load` 是运行时唯一资源入口:

- 数据库:`GameConfig` `IngredientDatabase` `JobDatabase` `RelicDatabase` `EventDatabase` `LevelDatabase`(管理器启动时加载并建索引)
- `UI/`:IMGUI 皮肤素材(`ui.png` 按钮底图等,linker 自动裁剪透明留边)
- `UI/MainMenu/`:主菜单背景(`bg` 前缀,当前为 `bg_title_keyart` 主视觉)
- `UI/Generated/`:生成素材运行时副本(LOGO、风味图标、岗位道具、员工与 NPC 立绘、9-Slice 面板 / 按钮),命名与源文件一致

## Scenes/ —— 场景

仅 `SampleScene.unity`:场景内只有少量对象(如 F1 操控面板),
所有管理器由 `[RuntimeInitializeOnLoadMethod]` 自建单例,初始化顺序见根目录 CLAUDE.md。

## Scripts/ —— 代码

全部玩法代码,命名空间 `Soup.*`,与模块目录一一对应:
`Game`(回合管线 / UI)、`Jobs`、`Items`、`Employees`、`Events`、`Relics`、`Levels`;
各模块 `Editor/` 子目录为编辑器工具(种子器、素材链接器、打包脚本)。
UI 层约定(IMGUI 层级表、皮肤、防中文溢出工具)集中在 `Scripts/Game/SoupUITheme.cs`。
