# FORK.md —— `features`（功能分支）

> **本分支管所有服务端插件改动**：竞技场奖励配置、建筑无敌、游戏内提示汉化。
> ⚠️ **只碰 `plugin/` 与 `tools/`**，**不碰面板前端**（`panel/`）。
> ⚠️ 客户端不用装任何东西（100% 服务端）。

---

## 分支结构（本 fork 现在只有两个分支）

| 分支 | 管什么 |
|---|---|
| **`localization`** | 面板汉化（纯前端 `panel/`）|
| **`features`** | ← **你在看的这个**：所有插件改动 |

⭐ **为什么只分两个**：**按"改动性质"分，不按"每次需求"分**。
两个分支**零文件重叠** ⇒ 永不冲突、可独立 rebase、可独立提 PR。

---

## 本分支包含三块改动

| # | 改动 | 核心文件 |
|---|---|---|
| **1** | **竞技场奖励可配置**（上游没有这功能）| `PrizeOverrides.cs`（新增）|
| **2** | **游戏内提示汉化**（35 条 + 8 阶位名）| `Tables.cs`、`Plugin.cs` |
| **3** | **城镇 + 竞技场建筑无敌**（零维护）| `Indestructible.cs`（新增）|
| — | **可复现构建脚本** | `tools/build-patched-arena.sh` |

**合计**：12 文件，+432 / -151

---

## ① 竞技场奖励可配置

**需求**：上游奖励**写死在代码里**，改一次要重新编译。
要求"能按自己的服务器调奖励" ⇒ 做成 **cfg 驱动**。

**8 个阶位的奖励**：

| 阶 | 奖励 |
|---|---|
| T1 | `TrophyDeer:2, Coins:999, Upgrader0Armor/Weapon:1, 各Token:10` |
| T2 | `AncientSeed:3, Upgrader1` |
| T3 | `WitheredBone:10, Upgrader2` |
| T4 | `DragonEgg:3, Upgrader3` |
| T5 | `GoblinTotem:5, Upgrader4` |
| T6 | `TrophySeekerBrute:3, Upgrader5, ShardSlotChiselAncient:1` |
| T7 | `BellFragment:9, Upgrader6, ShardSlotChiselLegendary:1` |
| T8 | `HatefulBlood:3, Upgrader7, ShardSlotChiselMythic:1` |

⭐ **BOSS 材料对应**（为什么是这些数字）：
`Fader` = `BellFragment × 9`；`Kall Fimbulbringer` = `HatefulBlood × 3`；
`Queen` = `TrophySeekerBrute × 3`

⚠️ **物品代码以服务器上的 `物品代码速查表.md` 为准**，不要凭印象写。

---

## ② 游戏内提示汉化

**问题**：进竞技场后所有提示都是英文，而服务器面向中文玩家。

**根源**：上游 `Tables.cs` 是 **"法语→英语"的翻译表**，`Language` 写死 `fr`，
阶位名还**硬编码不走翻译表**。

### ⭐ 关键决策：用 `Packs` 多语言结构，而不是加一张中文表

```csharp
Packs = { fr = {...}, en = {...}, zh = {...} }   // zh 是新增
```

⭐ **为什么**：**以法语作 key** ⇒ **35 个调用点零改动**、**向后兼容**；
以后加语言（日语、韩语）**只加数据、不改代码**。

### `Normalize()` 做语言规范化

| 输入 | 结果 |
|---|---|
| `fr` / `FR` | `fr` |
| `en` / `EN` | `en` |
| `zh` / `ZH` | `zh` |
| 其他/非法 | 回落 `fr` |

⚠️ 原逻辑是"**非 en 即 fr**"，不规范化的话 `zh` 会被当成 `fr`。

⚠️ **假阴性陷阱**：.NET 字符串是 **UTF-16** 存储，
用 UTF-8 去搜中文**必然搜不到**（不是没编进去，是查错编码）；
反射测试时注意**参数会在设 `Language` 之前求值**（测试顺序错会得到未翻译结果）。

---

## ③ 城镇 + 竞技场建筑无敌 ⭐ 本分支最核心

**需求原话**：
- "不想要连续扫描城内建筑的行为"
- "城镇生成时标记为不可摧毁，后续零维护"
- "竞技场的也改成无敌，否则每次挑战我都要过去修建筑"

**根因**：建筑因**战斗伤害**损毁 ⇒ 要不断修 ⇒ 巡检+自动修理只是**治标**。
**治本** = **生成时就标记为不可摧毁**。

### 完整字段表（照抄即可）

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

### ⭐ 关键决策

**1. 标记写在 ZDO 实例上，不是 prefab**

⚠️ **最重要的一条**：改 prefab 会让**所有**用该 prefab 的建筑都变无敌 ——
包括**玩家自己摆的墙**。写在 ZDO 上 ⇒ 只影响生成的这批。

**判据**：玩家用同一 prefab 自己摆的墙，必须仍然能拆能打坏。做不到就是改错层了。

**2. 打标记 ⇒ 周期巡检可以整体删掉**

| 旧做法 | 新做法 |
|---|---|
| 每 30 s 遍历全部 ZDO 判定属主 | **生成时一次性写 ZDO** |
| 周期性巡检 + 自动修理 | **零维护**，无扫描开销 |
| `Dead objects` 持续暴涨（实测 24h **+18 万**）| 不再产生 |

⭐ 建筑既然永不损毁，"计数/巡检/修理"三个动作就都没意义了 —— 一起删。
`Census()` 保留为**按需调用**，接到面板的"重建城镇"按钮上。

**3. ⚠️ 参考实现有 bug，别照抄**

`JereKuusela/valheim-infinity_hammer` 的 `behavior/NoCreator.cs`：

```csharp
if (piece.m_primaryTarget)                    // 🔴 copy-paste 残留的无关条件
    zdo.Set(Hashes.PieceCanBeRemoved, false); //    ⇒ 普通建筑根本不写 ZDO
piece.m_canBeRemoved = false;                 // 🔴 只改运行时实例，重启即失效
```

⚠️ 它"看起来能用"是因为该 mod 另有 patch **强制绕过**，
**不是**靠这个 ZDO 标记。**读参考实现要分清"它写的"和"真正让它生效的"。**

**4. 两个生成器 ⇒ 抽共享类**

城镇（`CityService.PlacePiece`）与竞技场（`ArenaBuilder.Place`）是两套独立生成器。
⭐ **第二个调用点出现时就抽成 `Indestructible.Mark()`**，而不是复制一份：
两处行为**必须永远一致**（否则会出现"城里不坏、竞技场坏"的怪现象）。

---

## 构建脚本

```bash
./tools/build-patched-arena.sh
```

| 特性 | 说明 |
|---|---|
| **可配置** | `REPO` / `WORK` / `IMG` / `CONTAINER` 都是变量 |
| **不污染源码树** | 先复制到 `.arena-build/` 再编译 |
| **用容器编译** | 走 dotnet SDK 容器，宿主机不需要装 .NET |

**验证产物与生产一致**：DLL 大小 + 字符串条数相同
（⚠️ PDB 路径**必然有差异**，这是正常的）。

⚠️ **`refs/`（游戏原版 DLL，有版权）与构建产物 `.dll` 都不入库** —— 见 `.gitignore`。

---

## 怎么验证

### 编译
```bash
./tools/build-patched-arena.sh     # 应 0 错 0 警
```

### 静态验证（IL 级）
用 `Mono.Cecil` 反汇编，确认 `Indestructible.Mark` 的 IL 含：
`ldc.i4.1`+`Set(int,bool)`、`ldc.r4 -1`+`Set(int,single)`、
`GetComponent<WearNTear>()`、`RemoveFloat`、
`GetComponent<Piece>()`+**`ldc.i4.0`**+`Set(int,bool)`；
并确认**城镇与竞技场两个生成器都调用了同一个 `Mark`**。

⚠️ key 是 `static readonly` + 运行时 `GetStableHashCode()`，**不是编译期常量**
⇒ **别去 DLL 字节流里搜哈希整数，搜不到是正常的**。要搜就搜 key 字符串（**UTF-16LE**）。

### 游戏内实测（不可省）

| 动作 | 预期 |
|---|---|
| 敲/攻击建筑 | 不掉血 |
| 用锤子拆 | 拆不掉 |

### ⛔ 改造已有建筑必须**重建**
新标记只写进**改动之后新生成**的 ZDO。**老建筑没有标记** ⇒
必须触发重建（面板"重建城镇"按钮 / 重新建竞技场）。
**别拿老建筑的行为判断功能是否生效。**

### ⚠️ 与 ValheimEnforcer 的相互作用

| 设置 | 值 | 效果 |
|---|---|---|
| `EnableStructureValidation` | `false` | ⭐ 总开关关 ⇒ 下面检测都不跑 |
| `DetectExcessiveStructureHealth` | `true` | **语义就是"检测血量异常的建筑"** |
| `StructureValidationExemptAdmins` | `true` | 管理员豁免 |

⚠️ **上生产前确认 `EnableStructureValidation` 的状态** ——
若被打开，`health = -1` 可能被当作异常结构上报。

---

## 怎么回滚

```bash
git checkout main
```
⚠️ 本分支含三块改动，**回滚是整体的**。要单独退某一块，
从 `main` 重新挑那部分提交。

---

## ⚠️ rebase 上游时的注意事项

**主要冲突面**：`Arena.cs` / `City.cs` / `Plugin.cs` / `Tables.cs`
（上游也在改这些的可能性最高）。

**新增文件不会冲突**：`PrizeOverrides.cs`、`Indestructible.cs`、`build-patched-arena.sh`

**特别提醒**：
- `City.cs` 里的**巡检逻辑是我们删掉的** —— 若上游也改了同一段，
  **保留"删掉"的结果**（这是我们的意图）
- `Tables.cs` 上游若加了**新提示条目** ⇒ 补进 `Packs` 的**三个语言**里（`fr`/`en`/`zh`）

**判据**：rebase 后编译 **0 错 0 警**；
⚠️ 若出现 `CS0649: field never assigned` 之类警告，
说明**又留下了死字段**（本项目删巡检后就遇到过 `_cityMissingFromSave`）。

⭐ **本分支 rebase 完全不影响 `localization` 分支**（零文件重叠）。

---

## 部署现状

| 改动 | 正式服 | 测试服 |
|---|---|---|
| 竞技场奖励配置 | ✅ 已部署 | ✅ |
| 游戏内提示汉化 | ✅ 已部署 | ✅ |
| **城镇 + 竞技场无敌** | ⏳ **待部署** | ✅ 已验证 |

⚠️ 正式服部署**需要重启容器** —— 先多信号确认无人在线
（RCON `players` + 面板人数 + SmoothServer `peers`）。

---

## 相关

- 面板汉化 → `localization` 分支
- 全局索引 → `docs/fork-changes.md`（`main` 分支）
