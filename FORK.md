# FORK.md —— `arena-indestructible-pieces` ★ 最全分支

> **本分支是干什么的**：让**城镇和竞技场生成的建筑"永不损毁、玩家拆不动"**，
> 且**不再靠周期扫描 + 自动修理**来维持。
>
> ⭐ **本分支包含整条链的全部改动**（奖励 + 游戏内汉化 + 无敌建筑）——
> **要一次性拿全部改动就用它**。
> ⚠️ 纯服务端插件改动，客户端不用装东西。

---

## 改了什么

### 本层新增的改动（无敌建筑）

| 类型 | 文件 |
|---|---|
| **新增** | `plugin/HearthwatchArena/Indestructible.cs` ← **共享的无敌标记实现** |
| **改** | `Arena.cs` ← `Place()` 调用标记 |
| **改** | `City.cs` ← `PlacePiece()` 调用标记；**删掉每 30 秒的巡检**；删死字段 |

### 加上下层带的（见各自 FORK.md）

`arena-prize-overrides`（奖励）+ `arena-zh-messages`（游戏内汉化）

**合计**：12 文件，+432 / -151

---

## 为什么这么做

| 需求原话 | 问题 |
|---|---|
| "不想要连续扫描城内建筑的行为" | 每 30 s 遍历全部 ZDO 做巡检，**开销大** |
| "城镇生成时标记为不可摧毁，后续零维护" | 建筑反复消失，要人工补 |
| "竞技场的也改成无敌，否则每次挑战我都要过去修建筑" | 打完一场**建筑就坏**，得手动修 |

**根因**：建筑会因**战斗伤害**损毁 ⇒ 需要不断修 ⇒ 巡检 + 自动修理是**治标**。
**治本** = **生成时就标记为不可摧毁**。

---

## ⭐ 核心实现：全在 ZDO 上，绝不改 prefab

```csharp
internal static class Indestructible
{
    public static void Mark(ZDO zdo, GameObject prefab)
    {
        zdo.Set(HasFieldsKey, true);                    // 总开关
        zdo.Set(HealthKey, -1f);                        // 免伤
        var wear = prefab.GetComponent<WearNTear>();
        if (wear != null)
        {
            zdo.Set(HasFieldsWearNTearKey, true);       // 声明覆盖 WearNTear
            zdo.Set(MaxHealthKey, -1f);                 // 阻止修理
            zdo.RemoveFloat(BuildingSkillKey);
        }
        var piece = prefab.GetComponent<Piece>();
        if (piece != null)
        {
            zdo.Set(HasFieldsPieceKey, true);           // 声明覆盖 Piece
            zdo.Set(CanBeRemovedKey, false);            // 锤子拆不掉
        }
    }
}
```

| 字段 | 值 | 治什么 |
|---|---|---|
| `health` | `-1f` | **被攻击不掉血**（负数即免伤）|
| `WearNTear.m_health` | `-1f` | **修理不会把它恢复成正血量** |
| `BuildingSkillLevel` | 移除 | 玩家建造技能会改写血量，清掉才稳定 |
| `Piece.m_canBeRemoved` | `false` | **锤子不能拆** |
| `HasFields` | `true` | ⭐ 总开关：不声明，下面的覆盖**全部静默失效** |
| `HasFieldsWearNTear` / `HasFieldsPiece` | `true` | ⭐ 各自子类的覆盖声明 |

---

## ⭐ 关键决策

### 1. 标记写在 **ZDO 实例**上，不是 prefab

⚠️ **这是最重要的一条**：改 prefab 会让**所有**用该 prefab 的建筑都变无敌 ——
包括**玩家自己摆的墙**。

写在 ZDO 上 ⇒ **只影响生成的这批**；玩家自建的同款建筑**仍然正常可摧毁**。

**判据**：玩家用同一个 prefab 自己摆的墙必须仍然能拆能打坏。做不到就是改错层了。

### 2. 打标记 ⇒ 周期巡检可以整体删掉

| 旧做法 | 新做法 |
|---|---|
| 每 30 s 遍历全部 ZDO 判定属主 | **生成时一次性写 ZDO** |
| 周期性巡检 + 自动修理 | **零维护**，无扫描开销 |
| `Dead objects` 持续暴涨（实测 24h **+18 万**）| 不再产生 |

⭐ **建筑既然永不损毁，"计数/巡检/修理"三个动作就都没意义了** —— 一起删。
`Census()` 保留为**按需调用**，接到面板的"重建城镇"按钮上。

### 3. ⚠️ 参考实现有 bug，别照抄

`JereKuusela/valheim-infinity_hammer`（同为服务端可用）的 `behavior/NoCreator.cs`：

```csharp
if (piece.m_primaryTarget)                    // 🔴 copy-paste 残留的无关条件
    zdo.Set(Hashes.PieceCanBeRemoved, false); //    ⇒ 普通建筑根本不写 ZDO
piece.m_canBeRemoved = false;                 // 🔴 只改运行时实例，重启即失效
```

⚠️ 它"看起来能用"是因为该 mod 另有 patch **强制绕过**，
**不是**靠这个 ZDO 标记。**读参考实现要分清"它写的"和"真正让它生效的"。**

### 4. 两个生成器 ⇒ **抽共享类**

城镇（`CityService.PlacePiece`）和竞技场（`ArenaBuilder.Place`）是两套独立生成器。

⭐ **第二个调用点出现时就抽成 `Indestructible.Mark()`**，而不是复制一份：
两处行为**必须永远一致**（否则会出现"城里不坏、竞技场坏"的怪现象），
而摘除是**纯重构、零逻辑变更**，风险极低。

---

## 怎么验证

### 第一层 · 编译产物（可静态证）

用 `Mono.Cecil` 反汇编，确认 `Indestructible.Mark` 的 IL 里有：

| 期望 IL | 含义 |
|---|---|
| `ldc.i4.1` + `ZDO::Set(int,bool)` | `HasFields = true` |
| `ldc.r4 -1` + `Set(int,single)` | `health = -1` |
| `GetComponent<WearNTear>()` + `ldc.r4 -1` + `Set` | `WearNTear.m_health = -1` |
| `RemoveFloat` | 移除 `BuildingSkillLevel` |
| `GetComponent<Piece>()` + **`ldc.i4.0`** + `Set(int,bool)` | `m_canBeRemoved = false` |

并确认**两个生成器都调用了同一个 `Mark`**。

⚠️ 这些 key 是 `static readonly` + 运行时 `GetStableHashCode()`，
**不是编译期常量** ⇒ **别去 DLL 字节流里搜哈希整数，搜不到是正常的。**
要搜就搜 **key 字符串（UTF-16LE）**。

### 第二层 · 游戏内实测（不可省）

| 动作 | 预期 |
|---|---|
| 敲/攻击建筑 | 不掉血 |
| 用锤子拆 | 拆不掉 |

### ⛔ 改造已有建筑必须**重建**

新标记只写进**改动之后新生成**的 ZDO。**老建筑没有标记** ⇒
必须触发一次重建（面板的"重建城镇"按钮 / 重新建竞技场）。
**别拿老建筑的行为判断功能是否生效。**

### ⚠️ 与 ValheimEnforcer 的相互作用

| 设置 | 值 | 效果 |
|---|---|---|
| `EnableStructureValidation` | `false` | ⭐ 总开关关 ⇒ 下面检测都不跑 |
| `DetectExcessiveStructureHealth` | `true` | **语义就是"检测血量异常的建筑"** |
| `StructureValidationExemptAdmins` | `true` | 管理员豁免 |

⚠️ **上生产前确认 `EnableStructureValidation` 的状态** ——
如果哪天被打开，`health = -1` 可能被当作异常结构上报。

---

## 怎么回滚

```bash
git checkout arena-zh-messages    # 退回：保留汉化，去掉无敌
```

⚠️ 回滚后建筑恢复可损毁，需要重新启用巡检/修理逻辑。

---

## ⚠️ rebase 上游时的注意事项

**这是链的最顶层 ⇒ rebase 要先把下面几层重建好**（见各分支 FORK.md）。

**主要冲突面**：`Arena.cs` / `City.cs`（上游也在改这两处的可能性最高）。

**`Indestructible.cs` 不会冲突**（新增文件）。

⚠️ **特别注意**：`City.cs` 里的巡检逻辑是我们**删掉**的。
如果上游也改了同一段（比如改巡检周期），会撞 —— 此时**保留"删掉"的结果**（我们的意图）。

**判据**：rebase 后编译，**应为 0 错 0 警**；
⚠️ 若出现 `CS0649: field never assigned` 之类的警告，说明**又留下了死字段**，
要一并清掉（本项目删巡检后就遇到过 `_cityMissingFromSave` 这个死字段）。

---

## 相关

- 奖励配置 → `arena-prize-overrides`
- 游戏内汉化 → `arena-zh-messages`
- 面板汉化 → `zh-cn-localization`
- 分支总览见 `docs/fork-changes.md`
