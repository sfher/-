# 学院路 · 双人 2D 横板闯关

Game Jam 原型仓库（主题：碰撞 / 质感）。当前阶段：玩法验证。

> 项目名暂未定，定下来后改这里和仓库名。

## 方向

- **美术**：几何图形，可爱的画风
- **分工**：去中心化，不强制指派，谁有想法谁做，走 PR 合入

## 环境

- Unity **6000.6.4f1**
- 渲染管线 URP 2D（Universal 2D 模板）
- 输入：Input System 1.20（`Active Input Handling = Input System Package (New)`，旧版 `Input.` API 不可用）

## 打开

1. Unity Hub 添加 `Project/nameless`
2. 用 6000.6.4f1 打开
3. 菜单 **GameJam → Build Prototype Scene**，生成 `Assets/Scenes/Prototype.unity`
4. Play

场景由脚本生成，所以第一次打开仓库时没有现成场景是正常的；生成一次后正常提交即可。

## 操作

| 玩家 | 移动 | 跳跃 |
| --- | --- | --- |
| P1 | A / D | 空格 |
| P2 | J / L | Enter |

两人共用一个键盘。I / K（P2）和 W / S（P1）暂未使用，留给蹲下或上方向操作。之后接手柄时再换成 Input System 的 control scheme。

## 结构

```
学院路/
├── Project/nameless/               Unity 工程
│   └── Assets/
│       ├── Scripts/
│       │   ├── PlayerMotor.cs          移动、跳跃、地面检测、被撞控制权让出
│       │   └── CoopCamera.cs           双人中点跟随 + 按距离缩放
│       └── Editor/
│           └── PrototypeSceneBuilder.cs 一键生成原型场景
├── docs/agents/                    工程约定（issue / 域模型）
└── AGENTS.md
```

## 当前实现的范围

- 速度驱动的动态刚体（`Rigidbody2D`），水平用 `Mathf.MoveTowards` 逼近目标速度，不是直接赋值，这样物理推挤才有机会生效
- 跳跃：coyote time、跳跃缓冲、可变跳跃高度、上升/下落不同重力
- 双人碰撞：被撞一方在 0.12s 内交出水平控制权，由物理完成推挤
- 地面检测同时认 `Ground` 和 `Player` 两层，所以可以踩在队友头上起跳
- 被踩的一方同样可以起跳，不做压制（已与策划确认，暂时作为特性保留，不是 bug）
- `MotionState` 状态机：`Idle` / `Move` / `Jump` / `Fall`，通过 `StateChanged` 事件外抛，供动画和音效挂接。加冲刺就是加一个枚举值和一个分支

还没做：碰撞冲量调参、顿帧与震屏、Tilemap 关卡、手柄、Cinemachine 相机。

## 协作

分支 + PR，规则见 `AGENTS.md`。Issue 在 GitHub Issues 上追踪，见 `docs/agents/issue-tracker.md`。
