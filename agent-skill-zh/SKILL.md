---
name: bimdown-zh
version: 1.1.2
description: AI与建筑信息的桥梁。像写代码一样读写BIM数据！让智能体自动完成建筑建模、按图建模、工程量统计或模型审查，甚至只是为你建一栋专属的数据化房屋！
---

# BimDown 智能体技能与规范 (BimDown Agent Skill & Schema Rules)

> **你的使命 (Your Mission)：** 建立 AI 智能体和建筑数据 (Building Data) 的桥梁。使用此技能，你可以像读写代码一样理解和创建建筑信息模型 (BIM)。它使你能够执行建筑设计、按图建模、工程量计算和模型审查等工作。当然，仅仅用来为你建立一个专属的房屋模型也是非常有趣且完全支持的！

## 环境准备 (Setup / Prerequisites)
在执行任何 `bimdown` 命令前，确保 CLI 已全局安装。
> **安全红线 (SECURITY RULE)**: 在自主运行 `npm install` 之前，你**必须显式地向用户请求许可**。

```bash
npm install -g bimdown-cli
```

你是一个在 BimDown 项目环境中运行的 AI 编程助手。
BimDown 是一种开源、原生服务于AI的建筑数据格式，它使用 CSV 来存储语义，使用 SVG 来定义几何图形。

## 核心架构与基本概念 (Core Architecture & Base Concepts)

- **全局单位是米 (Global Unit is METERS)**: CSV/SVG 中的所有坐标、宽度和结构属性必须严格使用米制 (METERS)。BimDown 用于模拟真实世界的尺寸。
- **计算字段是只读的 (Computed Fields are READ-ONLY)**: 任何标记为 `computed: true`（或列在 `virtual_fields` 中）的字段都由 CLI 自动计算。**请勿**将这些字段写入 CSV 文件。你可以使用 `bimdown query` 来检索它们的值。
- **双重属性结构 (Dual Nature)**: 属性数据存在于 `{name}.csv` 中，而二维几何图形存在于同级的 `{name}.svg` 文件中。这两个文件中的 `id` 字段必须完全匹配。
- **SVG 衍生的虚拟列 (SVG-derived virtual columns)**: 当你在 SVG 中绘制几何图形时，CLI 会自动为 `bimdown query` 计算以下字段 —— **请勿**将它们写入 CSV：
  - 线元素 (Line elements - 墙、梁、管道等): `length`, `start_x`, `start_y`, `end_x`, `end_y`
  - 多边形元素 (Polygon elements - 楼板、屋顶等): `area`, `perimeter`
  - 所有元素: `level_id` (从文件夹名称推断，例如 `lv-1/` → `lv-1`)
- **CSV+SVG 链接状态的具体示例**:
  > `lv-1/wall.csv` (注: 没有 `level_id` 列 —— 它是自动推断的):
  > `id,thickness,material`
  > `w-1,0.2,concrete`
  >
  > `lv-1/wall.svg`:
  > `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 -10 10 10"> <g transform="scale(1,-1)"> <path id="w-1" d="M 0 0 L 10 0" stroke-width="0.2" /> </g> </svg>`
  >
  > 在此之后，运行 `bimdown query . "SELECT id, length, level_id FROM wall"` 会返回 `w-1, 10.0, lv-1` —— `length` 和 `level_id` 都是自动计算的。

## 项目目录结构 (Project Directory Structure)

```
project/
  project_metadata.json        # 项目根标记 (格式版本、名称、单位)
  global/                      # 仅限全局的文件 — 必须在这里，不能在 lv-N/ 中
    grid.csv
    level.csv
    mesh.csv
  lv-1/                        # 每个楼层的文件
    wall.csv + wall.svg        # 带有几何图形的元素有成对的 CSV+SVG
    door.csv                   # 宿主元素仅有 CSV（在宿主墙上的参数化位置）
    space.csv                  # 空间 (spaces): CSV 提供种子点 + space.svg 提供边界 (由 build 命令自动计算)
    ...
  lv-2/
    ...
```

**关键规则 (Key rules)**:
- `level.csv`, `grid.csv`, `mesh.csv` 必须放在 `global/` 目录中，绝对不能放在 `lv-N/` 目录中
- 每层的元素 (如 wall, door, slab, space 等) 放在相应的 `lv-N/` 目录中
- 文件夹名称 (例如 `lv-1`) 将自动成为元素的 `level_id` — **请勿**将 `level_id` 写入 CSV 中。

## 创建/修改建筑的推荐工作流 (Recommended Workflow)

1. **首先规划空间布局 (Plan spatial layout first)**: 在写入任何文件之前，仔细思考空间关系 — 墙的位置、房间的相邻关系、门/窗的布置。可以在脑海中或纸上绘制坐标草图。
2. **其次编写 SVG 几何图 (Write SVG geometry first)**: 以正确的坐标创建 `.svg` 文件 (墙、楼板、柱)。几何图决定一切。
3. **然后编写 CSV 属性 (Write CSV attributes second)**: 创建带有元素属性的 `.csv` 文件 (如材质、厚度等)。记住: 不要包含像 `level_id`、`length`、`area` 等自动计算的字段。
4. **渲染并视觉验证 (Render and visually verify)**: 运行 `bimdown render <dir> -o render.png` 并**查看生成的 PNG 图像**以确认布局正确。检查墙体是否正确连接、房间是否封闭、门窗是否在正确位置。**请将渲染输出文件及其他非 BimDown 文件保存在项目目录外部** — 项目目录中只能包含 BimDown 的 CSV/SVG 文件，否则会被 `build` 命令拒绝。
5. **构建 (Build)**: 运行 `bimdown build <dir>` 验证数据结构，检查几何关系（墙体连接、宿主元素边界），并计算空间边界（从种子点生成 `space.svg`）。
6. **迭代 (Iterate)**: 如果渲染或构建过程提示有问题，修复 SVG 几何图并重新渲染，直到布局看起来正确为止。
7. **发布 (Publish)**: 运行 `bimdown publish <dir>` 上传项目并获取可分享的 3D 预览 URL。在每个项目首次发布前，请先请求用户同意。

## 工作流标准参考(SOPs)

在开始任何建筑设计或建模任务前，**请务必阅读相关的参考SOP文档**:

- **从头设计一座建筑 (Designing a building from scratch)** (根据用户说明或要求): 请阅读 [`references/building-design.md`](./references/building-design.md) 获取从体块到机电 (MEP)的完整 BIM 设计工作流。
- **根据现有图纸建模 (Modeling from existing plans)** (平面图图像、草图或已知尺寸): 请阅读 [`references/bim-modeling.md`](./references/bim-modeling.md) 获取元素创建顺序、依赖关系以及最佳实践。

这些是详细的标准操作程序。在**写入任何文件前**请阅读相关文档。

## 命令行工具与最佳实践 (CLI Tools & Best Practices)

1. **`bimdown query <dir> <sql> --json`**: 在所有表格中运行 DuckDB SQL 查询，包括 SVG 衍生的虚拟列。
   - **示例**: `bimdown query ./proj "SELECT id, length FROM wall WHERE length > 5.0" --json`
2. **`bimdown render <dir> [-l level] [-o output.png] [-w width]`**: 将某个楼层渲染为 PNG 蓝图图像（默认宽度2048px）。输出文件后缀 `.svg` 即可导出SVG。**在修改几何图形后始终要运行渲染，并查看PNG图像来视觉确认结果。**
3. **`bimdown build <dir>`**: 验证项目数据，检查几何学连通性（墙体连接度、宿主元素边界），并计算空间边界（生成 `space.svg`）。**每次修改 CSV 或 SVG 后务必运行此命令！** 命令别名为 `bimdown validate`。
4. **`bimdown schema [table]`**: 打印任何元素类型的完整属性结构。在创建元素之前用它来查询字段。
5. **`bimdown diff <dirA> <dirB>`**: 比较项目快照，输出 `+`、`-`、`~` 的结构差异。
6. **`bimdown init <dir>`**: 创建包含正确目录结构的新 BimDown 空项目。
7. **`bimdown publish <dir> [--expires 7d]`**: 将项目发布至 BimClaw 并返回模型预览分享链接。用于让用户通过3D查看器审查模型。**安全红线 (SECURITY WARNING)**: 会将项目数据上传至外部服务器。针对每个项目，首次运行此命令前必须询问用户并获得明确同意。
8. **`bimdown info <dir>`**: 打印项目摘要信息（楼层数、元素数）。
9. **`bimdown resolve-topology <dir>`**: 为机电(MEP)管线自动检测共点端点，生成 `mep_nodes` 并填充连接字段。
10. **`bimdown merge <dirs...> -o <output>`**: 将多个项目目录合并为一个整合模型，自动解决 ID 冲突。
11. **`bimdown sync <dir>`**: 将数据水合到 DuckDB，然后再脱水写回 CSV/SVG，以此来应用默认计算值。
12. **下载他人共享的项目**: 若用户提供分享链接如 `https://bim-claw.com/s/<token>`，在末尾加上 `/download` 下载压缩包：`curl -L https://bim-claw.com/s/<token>/download -o project.zip && unzip project.zip -d project/`

## 关键文件与几何规则 (Critical File & Geometry Rules)

- **ID 格式 (ID format)**:
  - **网格和标高 (Grid and Level)** 允许使用前缀后的任何字符串：标高：`lv-` + 任何字符串 (例如 `lv-1`, `lv-A`, `lv-B2`)；网格：`gr-` + 任何字符串 (例如 `gr-1`, `gr-A`, `gr-B2`)
  - **所有其他元素** 使用 `{prefix}-{number}` (仅允许数字): wall → `w-{n}`, column → `c-{n}`, slab → `sl-{n}`, space → `sp-{n}`, door → `d-{n}`, window → `wn-{n}`, ...
  - **务必运行 `bimdown build` 来确认你生成的 ID 符合要求。**
- **SVG 坐标 Y 轴翻转 (SVG Coordinate Y-Flip)**: `.svg` 文件中的所有几何图形 **必须 (MUST)** 包裹在 Y 轴翻转组中：`<g transform="scale(1,-1)"> ... </g>`。这只是一个固定模板 — 你**无需**进行任何坐标转换。在翻转组内部直接使用正常的笛卡尔坐标系 (向右为X正向，向上为Y正向) 即可。
- **CSV 与 自动计算字段对比**: 只能把你想要的数据写入**那些未被标记为自动计算 (computed)**的字段中。具体而言：`level_id`, `length`, `area`, `start_x/y`, `end_x/y`, `perimeter`, `volume`, `bbox_*` 全都是自动计算生成的 — 永远不要把它们写在 CSV 中。
- **垂直高度定位 (Vertical positioning)** (墙、柱以及其它垂直向元素):
  - `level_id`: 由包含它的文件夹名称自动推断得出 — 请勿写入CSV
  - `base_offset`: 从元素所在标高算起的垂直偏移量（米）。默认为 0。一般留空即可。
  - `top_level_id`: 约束元素顶部高度的标高。**留空**以默认连接到上方紧邻的下一个标高。如果元素需要跨层连接其他标高，才需要专门设置。
  - `top_offset`: 从顶部标高算起的垂直偏移量（米）。默认为 0。一般留空即可。
  - `height`: 基于标高高度和顶部/底部偏移自动算出 — 请勿写入CSV。
  - **对于大多数单层墙体**: 让 `top_level_id`, `top_offset`, 和 `base_offset` 全部留空 — 系统会自动根据各个楼层的实际标高高度计算出墙体的实际高度。

## 生成窍门 (Generation Tips)

### 常用典型数值参考表 (Typical Values in meters)
| Element | Field | Typical Range (米) |
|---------|-------|--------------|
| Wall - 轻隔墙 (partition) | thickness | 0.1 – 0.15 |
| Wall - 外墙 (exterior) | thickness | 0.2 – 0.3 |
| Wall - 结构承重墙 (structural) | thickness | 0.3 – 0.6 |
| Door - 单开门 (single) | width × height | 0.9 × 2.1 |
| Door - 双开门 (double) | width × height | 1.8 × 2.1 |
| Window - 窗户 | width × height | 1.2–1.8 × 1.5 |
| Window - 窗户 | base_offset (窗台高度) | 0.9 (标准高度), 0.0 (落地窗) |
| Column - 柱 | size_x × size_y | 0.3–0.6 × 0.3–0.6 |
| Slab - 楼板 | thickness | 0.15 – 0.25 |
| 楼层层高 | elevation diff | 3.0 – 4.0 |

### 房间边界闭合性 (Room Boundary Connectivity)
房间由**墙 (walls)、幕墙 (curtain walls)、柱 (columns) 和 房间分隔线 (room separators)**围合而成。为了使边界成功闭合：
- 线元素的端点必须在共享坐标上精确相遇。
- 示例: w-1 在 (10,0) 结束 → w-2 必须从 (10,0) 开始以形成 L 形连接。
- 运行 CLI 的 `build` 命令时，它会提示那些未闭合边界的端点，并在成功闭合时自动从闭合回路中生成空间边界图形。

### 门/窗放置规则 (Door/Window Placement Rules)

**推荐方式：使用 `host_x, host_y`**（宿主X/Y坐标）代替 `position`。只需提供洞口中心点的 2D 坐标 — `bimdown build` 命令将自动为你寻找最近的墙面并计算出相应的 `position`。

```csv
id,host_x,host_y,width,height,operation,material
d-1,5.0,3.0,0.9,2.1,single,wood
```

运行 `bimdown build` 后，CSV文件会被自动重写，加上 `host_id` 和 `position` 来替代你写的 `host_x/host_y`。如果你想强制锁定一面特定的墙，也可以加上 `host_id` 配合 `host_x/host_y` 共同使用。

**预备方案：手动定义 `position`** = 也就是从墙**起点** (SVG path 中的 M 坐标位置) 到洞口**中心**的距离（米）。

**验证规则 (适用于以上两种放置方式)**:
- 必须满足条件: `position - width/2 >= 0` 并且 `position + width/2 <= wall_length`
- 同一堵墙上的多个洞口必须互不重叠。
- 运行 CLI `build`命令会针对越界或发生重叠的安置情况发出警告。

### SVG 文件模板 (SVG File Template)
SVG文件必须始终遵循如下结构:
```xml
<svg xmlns="http://www.w3.org/2000/svg">
  <g transform="scale(1,-1)">
    <!-- 在这里放入各种元素，直接使用正常的笛卡尔坐标系统 (即X正轴向右，Y正轴向上) -->
  </g>
</svg>
```

## 核心数据结构参考 (Base Schema Reference)

所有元素均继承自 `element` 类表结构:
- **必须写入到 CSV 中的**: `id` (必需), `number`, `base_offset` (默认为 0), `mesh_file`
- **仅供查询用** (由系统计算得来，切勿写入CSV): `level_id`, `created_at`, `updated_at`, `volume`, `bbox_min_x`, `bbox_min_y`, `bbox_min_z`, `bbox_max_x`, `bbox_max_y`, `bbox_max_z`

**几何学基础字段类** — 这些字段仅供查询 (由程序的SVG系统生成，严禁写入到 CSV 中):
- **线元素 (`line_element`)** 例如 wall, beam, 等: `start_x`, `start_y`, `end_x`, `end_y`, `length`
- **点元素 (`point_element`)** 例如 column, equipment, 等: `x`, `y`, `rotation`
- **多边形元素 (`polygon_element`)** 例如 slab, roof, 等: `points`, `area`, `perimeter`

**基于宿主的元素 (`hosted_element`)**: 推荐优先使用 `host_x`/`host_y`，或使用 `host_id` + `position`。(详情见上述“门/窗放置规则”)

**垂直多层跨度 (`vertical_span`)**: 仅写入 `top_level_id`, `top_offset` — 详情见上述“垂直高度定位”部分。仅供查询的是 `height`。

**可用材质枚举 (`materialized`)**: concrete (混凝土), steel (钢), wood (木材), clt (正交胶合木), glass (玻璃), aluminum (铝), brick (砖), stone (石材), gypsum (石膏), insulation (保温层), copper (铜), pvc (PVC), ceramic (陶瓷), fiber_cement (纤维水泥), composite (复合材料)

## 核心建筑元素结构 (Core Schema Topologies)

下面是一份精选的核心建筑要素清单，这些是**最常用**的建筑实体分类结构。

> **重要提示 (IMPORTANT)**: 该项目实际上可支持的所有实体元素完整列表如下:
> `beam`, `brace`, `cable_tray`, `ceiling`, `column`, `conduit`, `curtain_wall`, `door`, `duct`, `equipment`, `foundation`, `grid`, `level`, `mep_node`, `mesh`, `opening`, `pipe`, `railing`, `ramp`, `roof`, `room_separator`, `slab`, `space`, `stair`, `structure_column`, `structure_slab`, `structure_wall`, `terminal`, `wall`, `window`
> 
> 如果用户请你修改或生成下方未包含的建筑元素，**运行** `bimdown schema <table_name>` 命令即可获取它们的具体属性需求以开展工作！

### Table: `door` (前缀 Prefix: `d`)
- **Geometry (几何)**: 仅 CSV。 使用 `host_x, host_y` 或 `host_id` + `position` 放置在墙面上。
```yaml
id_prefix: d
name: door
bases:
  - hosted_element
  - materialized
host_type: wall

fields:
  - name: width
    type: float
    required: true

  - name: height
    type: float

  - name: operation
    type: enum
    values:
      - single_swing
      - double_swing
      - sliding
      - folding
      - revolving

  - name: hinge_position
    type: enum
    values:
      - start
      - end

  - name: swing_side
    type: enum
    values:
      - left
      - right

```

### Table: `grid` (前缀 Prefix: `gr`)
- **Geometry (几何)**: 仅 CSV
```yaml
id_prefix: gr
name: grid

fields:
  - name: id
    type: string
    required: true

  - name: number
    type: string
    required: true

  - name: start_x
    type: float
    required: true

  - name: start_y
    type: float
    required: true

  - name: end_x
    type: float
    required: true

  - name: end_y
    type: float
    required: true
```

### Table: `level` (前缀 Prefix: `lv`)
- **Geometry (几何)**: 仅 CSV
```yaml
id_prefix: lv
name: level

fields:
  - name: id
    type: string
    required: true

  - name: number
    type: string
    required: true

  - name: name
    type: string

  - name: elevation
    type: float
    required: true
```

### Table: `space` (前缀 Prefix: `sp`)
- **Geometry (几何)**: 必须含 SVG
```yaml
id_prefix: sp
name: space
bases:
  - element

fields:
  - name: x
    type: float
    required: true
    description: Seed point X coordinate (room interior point)

  - name: y
    type: float
    required: true
    description: Seed point Y coordinate (room interior point)

  - name: name
    type: string

  - name: boundary_points
    type: string
    computed: true
    description: Space boundary polygon vertices (computed by build from surrounding walls)

  - name: area
    type: float
    computed: true
    description: Space area in square meters (computed from boundary polygon)

```

### Table: `wall` (前缀 Prefix: `w`)
- **Geometry (几何)**: 必须含 SVG
- **重要提醒**: 单面墙必须是一条完整的直线段(从起点到终点)。**切勿**因为要放置门或窗户而人为将墙分割成多段。门窗只需通过 `position` 或 `host_x/host_y` 参数挂靠在这面统一连贯的宿主墙上即可。
```yaml
id_prefix: w
name: wall
bases:
  - line_element
  - vertical_span
  - materialized

fields:
  - name: thickness
    type: float
    required: true
    description: Wall thickness in meters. SVG stroke-width should match but CSV is source of truth.

```

### Table: `window` (前缀 Prefix: `wn`)
- **Geometry (几何)**: 仅 CSV。 使用 `host_x, host_y` 或 `host_id` + `position`。务必设置 `base_offset` 参数 (窗台高度，一般标准为0.9米)。
```yaml
id_prefix: wn
name: window
bases:
  - hosted_element
  - materialized
host_type: wall

fields:
  - name: width
    type: float
    required: true

  - name: height
    type: float

```

## 其余资源 (Additional Resources)

如果需要关于 BimDown 格式的更多详细信息，或需要在 Autodesk Revit 与 BimDown 之间进行格式转换的工具，请参考官方 GitHub 仓库:
**[https://github.com/NovaShang/BimDown](https://github.com/NovaShang/BimDown)**
