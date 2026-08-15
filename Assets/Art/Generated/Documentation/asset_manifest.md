# 《汤灵纪行 / Soups & Sprites》生成美术资产清单与接入说明

> 版本：2026-08-15 主界面星饰文字 LOGO 修订  
> 生成方式：Codex 内置 `imagegen`，以项目现有草图和已完成 UI 为造型参考  
> 视觉基准：粗深棕描边、糖果色、手绘二维、轻黑色童话、缩小后轮廓优先  
> 原始草图：完整保留在 `Assets/Docs/美术素材`，本目录不覆盖原文件

## 1. 交付概况

当前累计新生成并部署 56 张 PNG：

- 员工角色 5 张；
- 食材图标 19 张，覆盖当前 `IngredientDataSeeder` 全部食材；
- 主厨房背景 1 张；
- 核心岗位道具 6 张；
- 风味图标 4 张；
- UI 面板与按钮 2 张。
- NPC / 剧情角色 9 张；
- 章节与探索场景 6 张；
- 主界面品牌视觉 4 张：主视觉、图形双语 LOGO、纯文字双语 LOGO、启动 ICON。

其中首批核心玩法素材 37 张、角色与场景扩展 15 张、主界面品牌视觉 4 张。目录中另有 `ui.png`、`switch_left.png`、`switch_right.png`、`divider.png`、`divider2.png` 等已有 UI 文件，不计入 56 张生成数量，也没有被覆盖。

除全幅场景背景外，新增 Sprite 均已验证为 RGBA PNG，Alpha 范围包含 0 与 255，可直接作为透明图导入。7 张背景均为 1672×941 RGB 全幅图。

## 2. 目录与命名规范

| 目录 | 前缀 | 用途 |
| --- | --- | --- |
| `Characters` | `employee_` / `character_` | 可雇佣员工，以及不直接进入员工数据库的 NPC / 剧情角色 |
| `Ingredients` | `ingredient_` | 食材卡、库存、岗位和结算图标 |
| `Environments` | `environment_` | 全幅场景与关卡背景 |
| `Props` | `prop_` | 可独立摆放的岗位和厨房道具 |
| `UI` | `ui_` / `flavor_` | 面板、按钮与风味状态图标 |
| `Documentation` | 无 | 资产清单、提示词与接入说明 |

统一采用小写英文 `snake_case`，文件名表达“类别 + 唯一语义”。后续变体可追加 `_idle`、`_hover`、`_disabled`、`_01` 等状态或序号，不使用“最终版”“新建画布”一类不可维护命名。

## 3. 员工角色

| 文件名 | 游戏名称 / ID | 造型说明 | 建议用途 |
| --- | --- | --- | --- |
| `employee_elf.png` | 小精灵 / `elf` | 姜饼质感主体、悬浮三指手、巨大白眼和红色蝴蝶结嘴；本项目正式风格锚点 | 默认员工头像、地图单位、教程角色 |
| `employee_mushroom_person.png` | 蘑菇人 / `mushroom_person` | 小精灵体型上生长蓝蘑菇与菌液，保留同族比例 | 锁定岗位员工、蘑菇事件角色 |
| `employee_ghost.png` | 幽灵 / `ghost` | 淡青半透明灵体、悬浮手和旧围巾，轮廓柔和 | 幽灵员工、无实体劳动提示 |
| `employee_otherworld_hero.png` | 异世界勇者 / `otherworld_hero` | 锅盖盾、汤勺剑、青绿色披风的圆润勇者 | 高效率稀有员工、剧情角色 |
| `employee_zhizhi.png` | 吱吱 / `zhizhi` | 炭灰鼠形魔物、芥黄色围裙、手持食物 | 会偷吃产出的特殊员工 |

### 3.1 NPC 与剧情角色

`character_` 前缀表示人物美术资产，不代表可雇佣员工。当前不自动写入 `EmployeeDatabase`，待关卡、对话和招募规则确定后再按需接入，避免仅因导入图片就改变现有玩法数据。

| 文件名 | 名称 / 建议 ID | 造型说明 | 建议剧情与玩法用途 |
| --- | --- | --- | --- |
| `character_pot_chief.png` | 锅长 / `pot_chief` | 铜汤锅礼帽、奶油色厨师服、红领巾与巨型木勺的厨房指挥官 | 新手引导、厨房公会负责人、岗位与锅具升级解锁 |
| `character_elder.png` | 长老 / `elder` | 蒸汽般白胡须、蓝棕长袍、汤滴法杖的年迈汤精灵 | 世界观讲述、大关结算、祖传配方与预言线索 |
| `character_giant.png` | 巨人 / `giant` | 赭色皮肤、补丁围裙、巨碗与树干汤勺，体型庞大但友善 | 巨人山洞委托、超大订单、终章宴席盟友 |
| `character_stone_golem.png` | 石头人 / `stone_golem` | 苔藓岩块身体、青色眼睛与发光汤核的远古守卫 | 城墙守卫、仓储试炼、耐久与容量机制教学 |
| `character_troll.png` | 巨魔 / `troll` | 苔绿色皮肤、青色鬃毛、铁锅护肩与食材桶的滑稽拦路者 | 城门或森林冲突、护送食材桶、条件交易事件 |
| `character_gray_mage.png` | 灰魔法师 / `gray_mage` | 灰袍尖帽、青色眼睛、符文法杖与灰紫魔雾 | 魔法味教学、遗物线索、风险配方与反转剧情 |
| `character_wood_elf_ranger.png` | 树精灵游侠 / `wood_elf_ranger` | 叶片斗篷、树皮护甲、弓箭和蘑菇挂饰 | 游侠营地向导、采集与狩猎任务、密林路线解锁 |
| `character_high_elf_mage.png` | 高等精灵魔法师 / `high_elf_mage` | 银金长发、象牙蓝金法袍、月牙法杖与双色宝珠 | 皇宫使者、高阶魔法配方、外交与终章仪式 |
| `character_goblin_merchant.png` | 地精商人 / `goblin_merchant` | 大耳地精、紫红马甲、满载锅具药瓶的背包和黄铜秤 | 局间商店、以物易物、随机折扣与带条件交易 |

## 4. 食材图标

| 文件名 | 游戏名称 / ID | 识别特征与说明 |
| --- | --- | --- |
| `ingredient_mushroom.png` | 蘑菇 / `mushroom` | 蓝色水滴形菌盖与浅色菌柄，基础软质食材 |
| `ingredient_sweet_berry.png` | 小甜果 / `berry` | 红色圆果、锯齿叶冠和白色甜味标记 |
| `ingredient_ice_fruit.png` | 冰晶果 / `ice_fruit` | 冰蓝圆果、雪花标记和绿色叶冠 |
| `ingredient_hot_fruit.png` | 爆辣果 / `hot_fruit` | 红橙果体、火焰纹和爆裂高光 |
| `ingredient_sour_fruit.png` | 青酸果 / `sour_fruit` | 荧光青柠果体和绿色叶冠 |
| `ingredient_magic_leaf.png` | 魔法叶 / `magic_leaf` | 七彩分段的双弧叶，表达不稳定魔法风味 |
| `ingredient_rush.png` | 灯芯草 / `rush` | 白色半透明花苞、淡蓝茎叶和金色花蕊 |
| `ingredient_daisy.png` | 小白花 / `daisy` | 白色花瓣与金黄花心，轮廓简洁 |
| `ingredient_mutant_mushroom.png` | 变异蘑菇 / `mutant_mushroom` | 非对称蓝紫菌盖、青紫大理石斑纹，表现随机风味 |
| `ingredient_fat_mushroom.png` | 肥大蘑菇 / `fat_mushroom` | 超大厚重菌盖和短粗菌柄，表现高软质产量 |
| `ingredient_strange_mushroom.png` | 奇异蘑菇 / `strange_mushroom` | 三株卷曲菌盖和彩虹虹彩，表现高随机风味 |
| `ingredient_sweet_bun.png` | 甜团团 / `sweet_bun` | 绿、红、奶白三层柔软团体和小眼睛 |
| `ingredient_big_horn_beast.png` | 大角兽 / `big_horn_beast` | 紫色球形身体、单只巨大黑眼与双黑角 |
| `ingredient_sticky_crawler.png` | 黏爬爬 / `nian_papa` | 浅黄面包状软体、短足和伸出的舌头 |
| `ingredient_little_spiky_ball.png` | 小刺球 / `little_spiky_ball` | 黑色圆核、绿色针壳和生气白眼 |
| `ingredient_silver_fish.png` | 小银鱼 / `silver_fish` | 黄银扁身、厚唇、黑鳍与环斑，怪鱼感较强 |
| `ingredient_happy_blob.png` | 快乐坨坨 / `happy_blob` | 橙色螺旋软团、举起的小手和大笑表情 |
| `ingredient_twin_tail_snake.png` | 双尾蛇 / `twin_tail_snake` | 绿色圆头、红蓝双尾组成心形轮廓 |
| `ingredient_stick_bug.png` | 棍棍虫 / `stick_bug` | 干竹节身体、两道深色环和四只小脚 |

## 5. 场景与岗位道具

| 文件名 | 名称 | 说明与摆放建议 |
| --- | --- | --- |
| `environment_kitchen_main.png` | 主魔法厨房 | 左侧采集庭院、中部加工台、右侧巨型汤锅与灶台，上层为仓储搁架；下方留有员工和数值浮层空间，可作为主玩法全屏背景 |
| `environment_title_keyart.png` | 主界面主视觉 | 锅长与汤灵围绕魔法锅、高等精灵和地精分列两侧，远景通往城堡；中央纵向区域刻意降噪，用于叠放 LOGO 与菜单按钮 |
| `environment_castle_outer_wall.png` | 城堡城墙外 | 黄昏城门、双塔、吊闸、护城河桥与汤车补给点；下方道路留作抵达、守城和商队事件舞台 |
| `environment_royal_palace.png` | 皇宫 | 蓝金宴会厅、挑高拱窗、中央长毯与巨型礼仪汤碗；用于宫廷委托、外交剧情和章节宴会 |
| `environment_magic_forest.png` | 魔法密林 | 巨树、青蓝发光蘑菇、魔法水池与蜿蜒林径；用于采集关卡、迷路事件和灰魔法线索 |
| `environment_ranger_camp.png` | 游侠驻扎地 | 叶帐篷、树上平台、索桥、射箭靶与营火汤锅；用于游侠任务中枢、训练与补给 |
| `environment_goblin_nest.png` | 地精巢穴 | 地下市集与工坊、吊篮、圆形洞门、药瓶摊和绿晶石；用于交易、潜入和机关事件 |
| `environment_giant_cave.png` | 巨人山洞 | 超大汤碗木勺、瀑布入口、巨型床铺与铜锅炉灶；用于巨人委托、巨量烹饪挑战与宴席剧情 |
| `prop_gather_patch.png` | 魔法采集地 | 蓝蘑菇、灯芯草、小白花和草地组合；用于采集岗位节点或教程高亮 |
| `prop_world_signpost.png` | 岗位路牌 | 三块无文字箭头木牌；用于岗位地图、分支入口或员工派遣区 |
| `prop_warehouse.png` | 魔法仓库 | 开门木柜、无标签罐、篮筐和袋装材料；用于仓储容量区 |
| `prop_processing_table.png` | 加工台 | 砧板、研钵、菜刀、碗和魔法瓶；用于处理阶段岗位 |
| `prop_magic_cauldron.png` | 魔法汤锅 | 铜锅、青色汤液、食材与旋涡蒸汽；主烹饪视觉焦点 |
| `prop_cooking_stove.png` | 魔法灶台 | 砖铜炉体、橙色火窗、卷曲管道和蓝色控温晶体；用于加热岗位或火力状态 |

## 6. UI 与风味图标

| 文件名 | 名称 | 接入说明 |
| --- | --- | --- |
| `logo_soups_and_sprites.png` | 《汤灵纪行》双语 LOGO | 中文“汤灵纪行”配英文“SOUPS & SPRITES”，铜锅、青色汤灵与紫色魔法星组成横向透明标志；建议主界面显示宽度 420–720 px |
| `logo_title_text_only.png` | 《汤灵纪行》星饰文字双语 LOGO | 中文“汤灵纪行”和英文“SOUPS & SPRITES”采用奶油金字面、铜棕描边与轻微高光，文字外围配置一枚中型紫星、两枚蓝星与两枚小紫星；不含锅、汤灵或场景图形，作为当前主页默认标题，建议显示宽度 420–680 px |
| `icon_app.png` | 游戏启动 ICON | 铜锅、汤灵与魔法星的无文字方形标志；重要内容位于中央安全区，可适配圆形及圆角矩形平台遮罩 |
| `ui_panel_main.png` | 主信息面板 | 木质与黄铜边框、羊皮纸内区；无文字，适合制作 9-Slice 后承载中文动态内容 |
| `ui_button_primary.png` | 主操作按钮 | 木框、琥珀色按钮面和青色端点宝石；无文字，适合制作 9-Slice 与正常/悬停/按下色调状态 |
| `flavor_cold.png` | 寒味 | 雪花、冰滴和霜星；建议 HUD 显示 32–64 px |
| `flavor_spicy.png` | 辣味 | 火焰包围红辣椒；建议 HUD 显示 32–64 px |
| `flavor_sour.png` | 酸味 | 青柠酸液、柑橘切片和气泡；建议 HUD 显示 32–64 px |
| `flavor_magic.png` | 魔法味 | 紫色四芒星、青色环带和魔法微粒；建议 HUD 显示 32–64 px |

## 7. Unity 接入步骤

### 7.1 自动绑定食材与员工

1. 使用 Unity 2022.3.12f1 打开项目并等待 PNG 导入完成。
2. 执行菜单 `Soup/Art Assets/Link Completed Icons`。
3. 脚本会把 `Ingredients` 中的 19 张正式图标绑定到 `IngredientItem`，并同步覆盖同名采集岗位图标。
4. 脚本会把 `Characters` 中的 5 张图标绑定到对应 `EmployeeItem`。
5. 生成素材优先级高于 `Assets/Docs/美术素材/完成后上传` 中的旧草图，但旧图不会被删除。

显式映射维护在 `Assets/Scripts/Game/Editor/ArtIconLinker.cs`。导入器会自动设置：

- `Texture Type = Sprite (2D and UI)`；
- `Alpha Is Transparency = true`；
- `Generate Mip Maps = false`。

### 7.2 手动接入场景、道具与 UI

- 主厨房背景：作为全屏 `Image`、`SpriteRenderer` 背景或 IMGUI `Texture2D` 使用；保持等比裁切，避免拉伸人物活动区。
- 主界面：将 `environment_title_keyart.png` 作为全屏等比裁切背景，再将 `logo_title_text_only.png` 以透明 Sprite 叠放在中央上方；菜单按钮沿中央纵向安全区排列。星饰文字版以少量紫蓝星点缀层次，但不重复背景已有的锅子与汤灵元素，是主页默认方案。
- 图形版 LOGO：`logo_soups_and_sprites.png` 保留作为宣传图、商店页、加载页或没有角色主视觉的独立版面使用，不建议与当前主界面背景同时叠放。
- 启动图标：在 `Project Settings > Player > Icon` 中将 `icon_app.png` 设置为默认图标；发布到移动端时可从此母版派生各平台要求的尺寸和前景/背景分层版本。
- 扩展场景：六张均采用与主厨房一致的 1672×941 横幅规格，可作为章节地图背景、对话舞台或关卡选址底图；底部已留出角色活动层。
- NPC / 剧情角色：按需绑定到未来的对话、事件或关卡数据；当前 `Soup/Art Assets/Link Completed Icons` 只处理 `employee_` 员工，不会自动绑定 `character_` 文件。
- 岗位道具：放在场景对应分区之上；推荐所有岗位采用一致的屏幕高度，再通过 Sorting Layer 控制员工前后关系。
- `ui_panel_main.png` 与 `ui_button_primary.png`：先在 Sprite Editor 中设置 Border 再使用 `Image.Type = Sliced`。
- 风味图标：使用固定正方形容器，颜色之外仍依靠雪花、辣椒、柠檬和星环形状区分，兼顾色弱识别。
- 如需运行时 `Resources.Load`，再将实际使用的 UI 副本放入 `Assets/Resources/UI/Generated`；当前源文件保持在美术目录，避免所有大图无条件进入构建包。

### 7.3 推荐导入参数

| 类型 | Max Size | Compression | Filter Mode | Pixels Per Unit |
| --- | ---: | --- | --- | ---: |
| 食材 / 风味图标 | 1024 或 2048 | Normal / High Quality | Bilinear | 100 |
| 员工 / 岗位道具 | 2048 | Normal / High Quality | Bilinear | 100 |
| UI 9-Slice | 2048 | None 或 High Quality | Bilinear | 100 |
| 主厨房背景 | 2048 或 4096 | High Quality | Bilinear | 100 |
| 主视觉背景 | 2048 或 4096 | High Quality | Bilinear | 100 |
| 透明 LOGO | 2048 | None 或 High Quality | Bilinear | 100 |
| 启动 ICON | 2048 | High Quality | Bilinear | 100 |

移动资源时必须在 Unity Project 窗口内移动，以便 `.meta` 与 GUID 同步迁移；不要只在文件管理器中移动 PNG。

## 8. 最终生成提示词集

本批采用“公共风格提示词 + 单项主体覆盖”的方式，便于后续继续生成同风格资产。

### 8.1 公共角色提示词

```text
Use case: stylized-concept. Production-ready 2D Unity employee cutout.
Preserve the supplied sketch identity and proportions. Charming hand-painted
candy-color light dark-fantasy; thick smooth dark-brown outline; clean
vector-like edges; restrained painterly texture; soft cel-shaded highlights;
large readable silhouette. One centered full-body character, no crop.
True transparent RGBA background; no text, logo, watermark, frame or shadow.
```

主体覆盖：姜饼小精灵；蓝蘑菇寄生的蘑菇人；淡青围巾幽灵；锅盖盾与汤勺剑勇者；穿芥黄围裙、拿着食物的鼠形吱吱。

### 8.2 公共食材提示词

```text
Use case: stylized-concept. Production-ready 2D Unity ingredient icon.
Image 1 is the official style anchor; Image 2 is the subject sketch.
Preserve subject identity. Thick smooth dark-brown outline, clean vector-like
edges, restrained painterly texture, candy colors and cel-shaded highlights.
Square canvas, one centered object, readable at 96 px, generous padding.
True transparent RGBA; no text, logo, watermark, frame, scene or shadow.
```

主体覆盖依次对应第 4 节的 19 个造型。三种无草图蘑菇额外要求：变异蘑菇为蓝紫非对称斑纹；肥大蘑菇为大菌盖短粗菌柄；奇异蘑菇为卷曲虹彩菌盖，且不能只做色相替换。

### 8.3 场景提示词

```text
Production-ready wide 16:9 side-view magical giant's kitchen in a crooked
timber-and-stone cottage. Left gathering garden with blue mushrooms and herbs;
center processing bench and mill; right enormous brass cauldron and stove;
high pantry shelf above. Oversized utensils establish tiny-worker scale.
Keep lower lanes clear for employees and upper corners calm for HUD overlays.
Warm amber versus cool teal light, hand-painted light dark-fantasy, no text,
characters, UI, border, logo, watermark, photorealism or 3D render.
```

### 8.4 公共岗位道具提示词

```text
Production-ready transparent Unity station sprite using the official style
anchor. One centered readable object at 160 px, thick dark-brown outline,
candy-color hand-painted 2D cel shading, clear gameplay silhouette.
True transparent RGBA; no character, text, scene, shadow, frame, logo,
watermark or checkerboard.
```

主体覆盖：魔法采集地、无文字三向路牌、开门仓库、带研钵与砧板的加工台、青色汤液铜锅、砖铜魔法灶台。

### 8.5 UI 与风味提示词

```text
Production-ready Unity UI sprite using the supplied UI reference and official
style anchor. Front orthographic view, thick dark-brown outline, wood, brass,
cream parchment and teal magic accents. Blank center for runtime Chinese text.
True transparent RGBA outside the object; no baked text, scene, character,
shadow, logo, watermark or checkerboard.
```

风味主体覆盖：雪花冰滴、火焰辣椒、酸液青柠、紫星青色魔法环。

### 8.6 透明通道修复提示词

部分生成结果首次返回 RGB 棋盘格。本批对这些文件再次使用 imagegen 编辑，核心提示词如下，并在部署前用 Pillow 读取通道进行机器验收：

```text
Background-removal edit for a Unity sprite. Preserve the subject, colors,
outline, scale and composition. Return a true RGBA PNG, with pixels outside
the outer subject contour assigned alpha=0 and the subject visible. Do not
render transparency as checkerboard or a solid background. Do not redraw,
crop, resize, recolor, add a shadow, scene, text, frame, logo or watermark.
```

### 8.7 第二批 NPC / 剧情角色提示词

```text
Use case: stylized-concept. Production-ready full-body 2D Unity game NPC
sprite for a magical soup roguelite. Use the official employee sprite as the
line, eye, palette and material style anchor. Charming hand-painted candy-color
light dark-fantasy; thick smooth dark-brown outline; clean vector-like edges;
restrained painterly texture; soft cel-shaded highlights; large expressive
eyes where appropriate. Square canvas, one centered full-body character in a
readable three-quarter pose, every limb and prop visible, generous padding,
strong silhouette at 128 px. True transparent RGBA with alpha=0 outside the
character; no scene, floor, shadow, text, logo, watermark or frame.
```

主体覆盖：铜锅礼帽与木勺的锅长；蒸汽白胡须与汤滴法杖的长老；拿巨碗和树勺的友善巨人；苔藓岩石与发光汤核的石头人；背食材桶的苔绿巨魔；灰袍尖帽与魔雾法杖的灰魔法师；叶斗篷弓箭手树精灵；蓝金白袍月牙杖高等精灵；背锅具药瓶并持黄铜秤的地精商人。

### 8.8 第二批章节场景提示词

```text
Use case: stylized-concept. Production-ready wide 2D Unity game environment
background for a magical soup roguelite. Use the official kitchen background
as the palette, lighting, material and rounded-shape style anchor. Charming
hand-painted candy-color light dark-fantasy, clean painterly surfaces and
dark-brown shape accents. Wide 16:9 side-view establishing composition with a
clear walkable lower foreground, strong midground focal set pieces, layered
depth and calmer upper corners for HUD. Cool teal ambience balanced by warm
amber focal light. Opaque full-bleed landscape PNG; no characters, text,
lettering, UI, logo, watermark, border, modern objects or pseudo-text.
```

主体覆盖：带汤车补给点的黄昏城墙外；以礼仪汤碗为焦点的蓝金皇宫宴会厅；发光蘑菇与古树构成的魔法密林；带树屋索桥、箭靶和营火锅的游侠营地；地下市场工坊式地精巢穴；带瀑布、巨型家具和铜锅炉灶的巨人山洞。

### 8.9 主界面主视觉、LOGO 与启动 ICON 提示词

主视觉：

```text
Use case: stylized-concept. Production-ready 16:9 Unity title-screen key art
for “汤灵纪行 / Soups & Sprites”. Use the official kitchen, soup spirit,
Pot Chief, High Elf Mage and Goblin Merchant as identity and style references.
Show the spirit and Pot Chief brewing luminous turquoise-violet soup, with the
mage and merchant framing the outer sides; a moonlit forest path leads toward
distant castle towers. Keep the central vertical 35% calm and dark enough for
a centered logo and stacked menu buttons. Warm amber hearth light versus cool
teal moonlight, candy-color hand-painted light dark-fantasy. Opaque full-bleed
background; no text, title, logo, UI, watermark, border or extra characters.
```

双语 LOGO：

```text
Use case: logo-brand. Transparent title logo built from a rounded copper
cauldron, turquoise soup-spirit steam, violet four-point star and crescent
orbit. Large chunky cream-gold Chinese fantasy lettering with dark-brown
outline; smaller uppercase English subtitle centered beneath.
Text (verbatim): “汤灵纪行” and “SOUPS & SPRITES”, each exactly once.
Centered horizontal lockup, readable at 420 px wide. True transparent RGBA;
no plaque, scene, mockup, extra text, watermark or unrelated symbol.
```

启动 ICON：

```text
Use case: logo-brand. Square launcher icon: one friendly luminous turquoise
soup spirit rising from a compact round copper cauldron and orbiting one violet
four-point magic star. Deep midnight navy-to-teal full-bleed background with a
subtle rounded copper rim. Bold readable silhouette at 32–64 px; keep important
shapes inside the central 76% adaptive-mask safe area. Opaque square PNG; no
text, initials, wordmark, character cast, thin filigree, mockup or watermark.
```

### 8.10 主页星饰文字 LOGO 修订提示词

```text
Use case: precise-object-edit. Transparent Unity home-screen title logo.
Preserve the Chinese text “汤灵纪行” and English text “SOUPS & SPRITES” exactly,
including every glyph, spacing, size, alignment, cream-gold fill, copper bevel,
dark-brown outline, canvas proportions and transparency. Add only one medium
violet four-point star above the title, one small cyan-blue star near the upper
left, one small deep-blue star near the upper right, and at most two tiny
purple/blue diamonds beside the English subtitle. Keep decorations separate
from all letter strokes and match the existing painted style. True transparent
RGBA. No pot, bowl, spoon, soup, spirit, elf, face, steam, bubble, ingredient,
crescent, orbit, plaque, frame, panel, scene, new text or watermark.
```

## 9. 后续批次建议

本批优先覆盖“核心回合可玩性”，尚未批量制作以下数量较大的叙事内容：

1. 基于本次六张章节场景继续制作昼夜、天气、危机状态变体，以及首领宴会厅与结局画面；
2. 全套遗物独立图标；
3. 本次九名 NPC 的对话半身像、表情差分，以及随机事件插画；
4. 员工行走、工作、偷吃、受击与庆祝动画帧；
5. 蒸汽、寒气、辣味爆发、酸味结算和魔法倍增 VFX；
6. UI 的悬停、按下、禁用状态与图标小尺寸人工像素级清稿。

继续生产时应复用 `employee_elf.png` 作为唯一风格锚点，并以本文件第 8 节为提示词模板，避免后续批次发生画风漂移。
