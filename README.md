# 回（Hui）· 双人 2D 横板闯关

Unity 双人协作平台解谜原型。当前仓库的**唯一开发基准是 Tuning Lab**，正式关卡从它派生，旧 `World` 架构、旧编辑器和旧测试房不再作为开发依据。

> **当前进度（2026-11-04）**：核心玩家、速度/冲刺、红热锻造、A 形态、大炮、L 形转向器、钥匙与门、重生点和传送点的基础版本已经进入 Tuning Lab；关卡编辑器和验证链路已建立。冰块、绳子、地刺、美术资源以及重生点/传送点的补全仍在进行，完整主线关卡尚未完成。

## 先看这里：参考索引

### 开发基准（代码和关卡有冲突时优先看这里）

1. [当前项目基准](docs/current-baseline.md)：唯一架构、场景边界、菜单入口、验证层级。
2. [Tuning Lab 关卡编辑器](docs/tuning-lab-level-editor.md)：关卡数据模型、代码地图、扩展和验证方式。
3. [冲刺与对撞：现行规则](docs/冲刺与对撞-现行规则.md)：按当前代码整理的冲刺、对撞、锻造、形态和大炮规则。
4. [Tuning Lab 使用手册](docs/tuning-lab.md)：调试台操作与常用快捷键。

### 设计参考（用于规划，不得直接覆盖当前代码基准）

- [策划案总览](docs/策划案/回-概述.md)
- [机制策划案](docs/策划案/回-机制.md)
- [关卡策划案](docs/策划案/回-关卡%28未完成%29.md)
- [剧情策划案](docs/策划案/回-剧情.md)
- [美学策划案](docs/策划案/回-美学.md)

### 协作与历史

- [Issue 流程](docs/agents/issue-tracker.md)：需求、验收和状态约定。
- [策划 → 程序交接](docs/agents/策划-程序交接.md)：设计变更如何落到代码和关卡。
- [最近更改](docs/recent-changes.md)：近期合并记录。
- [速度等级：基准与教训](docs/速度等级-基准与教训.md)：历史复盘，不是当前规范。

## 当前架构

```text
Project/nameless/
├─ Assets/
│  ├─ Scenes/
│  │  ├─ TuningLab.unity          # 规则基准场景，禁止手改生成物
│  │  └─ Levels/                  # 正式关卡和调试房
│  └─ TuningLab/
│     ├─ *.cs                     # 玩家、相机、规则、机关和显示
│     ├─ Level/
│     │  ├─ LabLevelData.cs       # 格子、绳、出生点、相机和分区数据
│     │  └─ LabLevel.cs           # 数据 → 场景生成
│     ├─ Editor/                  # Tuning Lab 菜单和自检
│     └─ Tests/                   # PlayMode 验证
└─ ProjectSettings/               # Unity 项目配置
```

架构约束：

- 规则改动进入 `Assets/TuningLab/`，不要恢复 `Assets/Scripts` 或另建第二套玩家逻辑。
- 可编辑、可保存、可随分区移动的关卡内容进入 `LabLevelData`，不要手改生成的碰撞体。
- 编辑器预览和 Play 共用 `LabLevel.Build`，避免两套关卡结果。
- `TuningLab.unity` 是规则基准；正式关卡通过菜单从它派生。
- 工程目录仍然是 `Project/nameless/`。仓库名虽然是 Hui，暂不改目录名，避免破坏 Unity 项目引用。

## 当前进度

### 已完成并进入基准

| 范围 | 当前状态 | 依据 |
| --- | --- | --- |
| Tuning Lab 单一基准、场景派生和关卡编辑器 | 已完成 | [Issue #16](https://github.com/sfher/hui/issues/16)、[Issue #3](https://github.com/sfher/hui/issues/3) |
| 玩家移动、跳跃、冲刺、五档速度和相机 | 已完成基础版本 | [机制策划案](docs/策划案/回-机制.md) |
| 红热锻造、A 形态、L 形转向器和大炮 | 已完成基础版本 | [Issue #14](https://github.com/sfher/hui/issues/14)、[#15](https://github.com/sfher/hui/issues/15)、[#21](https://github.com/sfher/hui/issues/21) |
| 钥匙、门、重生点、传送点 | 基础功能已完成 | [Issue #18](https://github.com/sfher/hui/issues/18)、[#19](https://github.com/sfher/hui/issues/19)、[#20](https://github.com/sfher/hui/issues/20) |
| 纯数据 Self Check、真实物理 Play Check、PlayMode 测试 | 已建立 | [当前项目基准](docs/current-baseline.md) |

### 当前进行中或待处理

| Issue | 主题 | 当前判断 |
| --- | --- | --- |
| [#29](https://github.com/sfher/hui/issues/29) | 冰块 | 尚未进入当前基准；包含破坏、编组和冰面加速。对应主线 L3/L4。 |
| [#27](https://github.com/sfher/hui/issues/27) | 绳子 | 已有 `LabVerticalRope` 基础实现，但策划中的完整交互、反馈和关卡用途仍待补齐。 |
| [#34](https://github.com/sfher/hui/issues/34) | 地刺 | 待加入并接入关卡编辑器；对应主线绳子进阶与 L9。 |
| [#32](https://github.com/sfher/hui/issues/32) | 重生点 | 已补齐：按唯一玩家接触激活，多点互斥，双人同时接触不记录，传送不重置记录状态；关卡编辑器支持摆放。 |
| [#33](https://github.com/sfher/hui/issues/33) | 传送点 | 基础传送已在；选择状态和重置地图时排除重生点待补全，优先级可低。 |
| [#35](https://github.com/sfher/hui/issues/35) | 大炮方向 bug | 已知小 bug：B 从开口朝上的 A 进入时，射出方向可能与冲刺方向相反。 |
| [#26](https://github.com/sfher/hui/issues/26) | 像素美术资源 | 资源清单和规格已明确，当前仍是程序生成占位显示。 |
| [#36](https://github.com/sfher/hui/issues/36) | 项目目录命名 | 已决定保留 `Project/nameless/`；只同步文档描述，不改 Unity 工程目录。 |

### 按策划案的主线覆盖情况

这是“机制是否具备”的进度，不代表对应关卡已经制作完成。

| 区段 | 设计目标 | 当前状态 |
| --- | --- | --- |
| L0 | 移动、跳跃、冲刺入门 | 基础能力已具备 |
| L1 | 横碰、上冲、重生点 | 机制具备；重生点仍需修复多点状态 |
| L2 | 钥匙、门、伤害物、绳子初见 | 钥匙/门具备；伤害物和绳子内容未齐 |
| L3 | 冰块破坏与滑行 | 待 #29 |
| L4 / L4+ | L 转向、传送点、冰面组合 | L 转向和传送点基础具备；冰面与完整关卡待补 |
| L5 / L6 | 滑道、绳子、转换器组合 | 部分机制具备，组合关卡未完成 |
| L7 | 大炮 | 基础功能具备，仍有 #35 |
| L8 | 竖绳与 `|` 形态 | 绳子基础存在，完整交互待 #27 |
| L9 | 地刺交互 | 待 #34 |
| L10 | 全部传送门、最终门、最大速度、结局/Boss | 仍属策划阶段，未形成完整关卡 |

## 打开和验证工程

1. 用 Unity Hub 添加 `Project/nameless`。
2. 使用 Unity **6000.6.4f1** 打开（URP 2D，Input System 1.20）。
3. 菜单执行 **GameJam → Build Tuning Lab**，生成或更新基准场景。
4. 按 Play 验证调试台；正式关卡从 Tuning Lab 派生，不要手改基准场景生成物。
5. 使用 **GameJam → Run Level Baseline Self Check** 做纯数据验证，使用 **GameJam → Run Level Play Check** 做真实物理验证。

Unity 打开工程可能重写 `ProjectSettings/`。提交前检查 `git status`：只丢弃纯序列化噪音，真实的图层、输入和构建场景改动必须保留。

## 关卡工作流

- **GameJam → New Level From Tuning Lab**：创建正式关卡。
- **GameJam → Convert Current Scene To Level**：把 Tuning Lab 副本转为关卡。
- **GameJam → Tuning Lab Level Editor**：编辑地形、绳、出生点、相机和连续关卡分区。
- **GameJam → Run Level Baseline Self Check**：验证数据归一化、矩形合并、分区和相机规则。
- **GameJam → Run Level Play Check**：创建临时关卡并用真实物理验证。

## 操作

| 玩家 | 移动 | 上 / 下 | 跳跃 | 冲刺 |
| --- | --- | --- | --- | --- |
| P1 / A | A / D | W / S | 空格 | 左 Shift |
| P2 / B | ← / → | ↑ / ↓ | `/` | `.` |

调试台常用：`R` 重置，`P` 像素网格，`Tab` 格线。关卡编辑器工具和验证方式见 [编辑器手册](docs/tuning-lab-level-editor.md)。

## 目录速查

- 规则、玩家、相机和机关：`Project/nameless/Assets/TuningLab/`
- 关卡数据与生成：`Project/nameless/Assets/TuningLab/Level/`
- 基准场景：`Project/nameless/Assets/Scenes/TuningLab.unity`
- 正式关卡：`Project/nameless/Assets/Scenes/Levels/`
- 设计草稿：`docs/策划案/`
- 当前事实与入口：[docs/current-baseline.md](docs/current-baseline.md)
