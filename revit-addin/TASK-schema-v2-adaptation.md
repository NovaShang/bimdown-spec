# Revit Addin 任务：适配 Schema V2

## 背景

BimDown spec 做了一次大重构（详见最近 3 个 commit）。核心改动：

1. **Door/Window 变 CSV-only** — 不再导出 SVG，位置用 `position`（0-1 沿墙参数）代替绝对坐标
2. **Space 变 seed point** — 不再导出 polygon SVG，只导出 x,y 种子点 + name
3. **Wall thickness 写 CSV** — 不再依赖 SVG stroke-width，thickness 是 CSV 必填字段
4. **`location_param` 改名为 `position`** — hosted_element.yaml 里的字段名变了
5. **Material enum 更新** — 15 个大类：concrete, steel, wood, clt, glass, aluminum, brick, stone, gypsum, insulation, copper, pvc, ceramic, fiber_cement, composite
6. **新增元素类型** — roof, ceiling, opening, mesh（纯 mesh fallback）
7. **ID level-scoped** — 同一楼层内唯一即可
8. **base_offset 默认 0** — 可省略
9. **top_level_id 默认上一层** — 可省略

## 需要改的文件和具体操作

### 1. Extractor 改动

#### `Extractors/HostedElementExtractor.cs`
- **`location_param` → `position`**：字段名改了
  - `FieldNames` 里 `"location_param"` → `"position"`
  - `ComputedFieldNames` 里也要改（`position` 不再是 computed，是 required CSV 字段）
  - 把 `"position"` 从 `ComputedFieldNames` 移到 `FieldNames`（非 computed）
  - Extract 方法里 `dict["location_param"]` → `dict["position"]`
  - 值的计算逻辑不变（ComputeNormalizedParameter 返回 0-1）

#### `Extractors/LineElementExtractor.cs`
- **thickness 需要写入 CSV**
  - 当前 start_x/end_x 等都在 `ComputedFieldNames`（SVG 注入）
  - 对于 wall/structure_wall：需要在 `FieldNames` 里加一个 `"thickness"`，从 `Wall.Width` 读取（已经在 UnitConverter 转成米）
  - 注意：不是所有 line_element 都有 thickness（beam、duct 等没有），只有 wall 类需要
  - 方案 A：在 LineElementExtractor 里判断 category 加 thickness
  - 方案 B（推荐）：thickness 已经在 wall 的 CompositeExtractor 里通过 inline field 处理？检查 `ArchitectureTableExporters.cs` 的 wall 定义

#### `Extractors/MaterializedExtractor.cs`
- **Material 值映射到 enum**
  - 当前导出的是 Revit material name（如 `"Concrete, Cast-in-Place gray"`）
  - 需要加一个 mapping function，把 Revit material name 映射到 enum 值
  - 映射逻辑参考 `scripts/migrate-sample-data.ts` 里的 `MATERIAL_MAP`：
    ```
    包含 "concrete" → "concrete"
    包含 "steel" 或 "metal" → "steel"
    包含 "wood" 或 "lumber" → "wood"
    包含 "glass" → "glass"
    包含 "aluminum" → "aluminum"
    包含 "brick" → "brick"
    包含 "gypsum" → "gypsum"
    包含 "copper" → "copper"
    包含 "stone" → "stone"
    其他 → "composite"
    ```

### 2. SVG 改动

#### `Svg/SvgWriter.cs`
- **Door/Window 不再生成 SVG**
  - 在 `WriteAll()` 或 `RenderTable()` 里，跳过 door 和 window 表
  - Space 也不再生成 SVG polygon
  - 具体：`SvgTableConfig` 里 door/window/space 的配置移除或标记为 skip

#### `Svg/SvgTableConfig.cs`
- **移除 door、window、space** 的 SVG 配置
- **新增 room_separator**（如果有的话，render type = Line，无 thickness）

#### `Svg/SvgReader.cs`
- **`ResolveHostedParameters()`**：可能不再需要，因为 door/window 没有 SVG 了
  - Import 时 door/window 的 position 直接从 CSV 读
  - 但为了向后兼容（读旧格式），可以保留但不依赖

### 3. Export 改动

#### `ExportCommand.cs`
- **Pass 3（SVG 写入）**：door/window/space 不再写 SVG 文件
- Wall 的 SVG stroke-width 不再承担 thickness 语义，但仍然写（作为渲染提示）

### 4. Import 改动

#### `Import/ArchitectureImporters.cs`
- **DoorImporter / WindowImporter**：
  - 读 CSV `position` 字段（不再读 SVG + ResolveHostedParameters）
  - 从 position(0-1) + host wall curve → 计算 Revit 放置点
  - 逻辑：`point = hostCurve.Evaluate(position, normalized: true)`
- **SpaceImporter**：
  - 读 CSV `x`, `y` → 创建 Room 时用作放置点
  - 不再读 SVG polygon

#### `Import/TableImporterBase.cs`
- SVG geometry merge 步骤可以跳过对 door/window/space 的处理

### 5. 新增 Table Exporter

#### `Tables/ArchitectureTableExporters.cs`
新增 3 个 exporter：

**RoofExporter:**
- Category: `BuiltInCategory.OST_Roofs`
- Extractors: ElementExtractor + PolygonElementExtractor + MaterializedExtractor
- 额外字段：`roof_type`（从 RoofType 推断 flat/gable/hip/shed/mansard）、`slope`（角度）、`thickness`
- SVG: polygon（跟 slab 一样）

**CeilingExporter:**
- Category: `BuiltInCategory.OST_Ceilings`
- Extractors: ElementExtractor + PolygonElementExtractor + MaterializedExtractor
- 额外字段：`height_offset`（距楼层标高的偏移）
- SVG: polygon

**OpeningExporter:**
- Category: `BuiltInCategory.OST_SWallRectOpening` + `BuiltInCategory.OST_FloorOpening` 等
- Extractors: ElementExtractor + HostedElementExtractor
- 额外字段：`width`, `height`, `shape`（rect/round/arch）
- 无 SVG（CSV-only，跟 door/window 一样）

#### `Tables/GlobalTableExporters.cs`（或新文件）
**MeshExporter:**
- 遍历不认识的 category 或明确的 mesh-only category（railing、generic model 等）
- 导出到 `global/mesh.csv`
- 暂时只导出 CSV 元数据（id, category, name, level_id, x, y, z, rotation）
- GLB 导出先不做（需要额外库），mesh_file 字段留空

### 6. 新增 Table Importer

#### `Import/ArchitectureImporters.cs`
- **RoofImporter**: 从 polygon + slope 创建 Revit roof
- **CeilingImporter**: 从 polygon 创建 ceiling
- **OpeningImporter**: 从 host_id + position 创建 wall opening

这三个可以后做，优先保证导出正确。

### 7. ID 改动

- 当前 ShortIdGenerator 生成全局唯一 ID
- 新 spec 允许 level-scoped ID，但 Revit 端生成全局唯一仍然没问题（是 spec 的超集）
- **不需要改**，保持全局唯一即可

### 8. Test 更新

#### `Tests/SvgRoundTripTests.cs`
- 移除 door/window/space 的 SVG roundtrip 测试
- 新增 room_separator SVG 测试（如果实现了的话）

#### 其他 test 文件
- `DoorTests.cs` / `WindowTests.cs`: 验证导出有 `position` 字段、无 SVG 文件
- `WallTests.cs`: 验证 CSV 有 `thickness` 字段
- `SpaceTests.cs`: 验证导出有 `x`, `y`、无 SVG

## 优先级

1. **P0 — 导出必须能过 CLI validate**
   - `location_param` → `position`
   - wall thickness in CSV
   - material enum mapping
   - door/window/space 不生成 SVG
   - 跑 `cd cli && npx bimdown validate <output_dir>` 验证

2. **P1 — 新元素类型导出**
   - roof, ceiling, opening exporter
   - mesh.csv（GLB 先不做）

3. **P2 — Import 适配**
   - door/window/space 从 CSV 读 position/x,y
   - roof/ceiling/opening importer

## 验证方式

```bash
# 构建
dotnet build revit-addin/BimDown.RevitAddin.csproj

# 跑测试（需要 Revit 2026）
dotnet run --project revit-addin/Tests/BimDown.RevitTests.csproj -- --treenode-filter "/*/*/*/*[Category!=SampleProject]"

# 导出后用 CLI 校验
cd cli && npx bimdown validate <导出目录>
```

## 参考

- 新 spec YAML: `spec/csv-schema/` 下所有 yaml 文件
- 迁移脚本（material mapping 逻辑）: `scripts/migrate-sample-data.ts`
- 新 SKILL.md（格式说明）: `agent/pi_files/skills/bimdown/SKILL.md`
- CLI registry（ID prefix、SVG 规则）: `cli/src/schema/registry.ts`
