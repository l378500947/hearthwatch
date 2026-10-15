# 本 fork 的改动索引（l378500947/hearthwatch）

> 这是 **[l378500947/hearthwatch](https://github.com/l378500947/hearthwatch)** 相对**上游**
> 的改动记录。上游仓库见 `origin` 之外的那个 remote。
>
> **为什么有这个文件**：分支会越攒越多，过一段时间光看 commit message 已经记不清
> 每个分支到底改了什么、为什么改。此文件就是那份"回头能看懂"的索引。

---

## ⚠️ 先记住两件事

### 1. 分支是**链式堆叠**的，不是各自独立的

```
main (上游基准)
 ├── zh-cn-localization ................. 独立，只动面板前端
 └── arena-prize-overrides
      └── arena-zh-messages
           └── arena-indestructible-pieces  ★ 最全
```

⭐ **`arena-indestructible-pieces` 包含下面全部 4 个提交** —— 要一次性拿全部改动就用它。

⚠️ **代价**：因为是从下往上堆的，**rebase 上游时要从最底层开始**
（先 `arena-prize-overrides`，再逐层往上），否则冲突会重复出现。

### 2. 分支与上游的差异面（决定 rebase 好不好受）

| 分支 | 冲突面 | 说明 |
|---|---|---|
| `zh-cn-localization` | **极小** | 只改 `i18n.jsx` **3 行**，其余全是**新增文件**（新语言包不会冲突）|
| `arena-prize-overrides` 起 | 中等 | 动了 `Arena.cs` / `City.cs` / `Plugin.cs` 等上游也在改的文件 |
| `arena-indestructible-pieces` | 中等 | 同上 + 新增 `Indestructible.cs` |

---

## 分支一览

| 分支 | 一句话 | 详细说明 |
|---|---|---|
| **`zh-cn-localization`** | **面板汉化**（14 个模块，1222 个 key）| 见该分支的 `FORK.md` |
| **`arena-prize-overrides`** | **竞技场奖励配置**功能（上游没有）+ 可复现构建脚本 | 同上 |
| **`arena-zh-messages`** | **竞技场游戏内提示汉化**（35 条 + 8 个阶位名）| 同上 |
| **`arena-indestructible-pieces`** | ⭐ **城镇 + 竞技场建筑无敌**（生成时打标记，零维护）| 同上 |

> ⚠️ `arena-city-indestructible`（城市无敌的初版实现）**已删除** ——
> 它的功能被 `arena-indestructible-pieces` 完全包含，且后者把实现重构成了共享类。
> 保留两个做同一件事的分支只会造成混淆。

---

## 部署现状

| 改动 | 正式服是否已部署 |
|---|---|
| 面板汉化 `zh-cn-localization` | ✅ 已部署 |
| 竞技场奖励 `arena-prize-overrides` | ✅ 已部署 |
| 游戏内提示汉化 `arena-zh-messages` | ✅ 已部署 |
| **城镇 + 竞技场无敌** `arena-indestructible-pieces` | ⏳ **测试服已验证，正式服待部署** |

⚠️ **正式服部署需要重启容器** —— 先多信号确认无人在线（RCON `players` + 面板人数 +
SmoothServer `peers`），再动手。

---

## 怎么用这些分支

**跑最全的版本**：
```bash
git checkout arena-indestructible-pieces
```

**只要面板汉化**（干净、好提 PR）：
```bash
git checkout zh-cn-localization
```

**编译插件**（任一带插件改动的分支）：
```bash
./tools/build-patched-arena.sh
```
⚠️ 构建产物（`.dll`）**不入库**，`refs/`（游戏原版 DLL，有版权）同样不入库。

---

## 往上游提 PR 时

| 分支 | 适合提 PR 吗 |
|---|---|
| `zh-cn-localization` | ⭐ **适合** —— 新增文件为主，冲突面 3 行，是干净的功能贡献 |
| 其余分支 | ⚠️ **不适合** —— 含服务器专属的玩法调整（奖励数值、无敌建筑），不是通用功能 |

---

## 维护约定

- **一件事一分支** —— 每类改动独立分支，不混在一个分支里
- **每个分支都有自己的 `FORK.md`** —— 记录该分支的：改了什么 / 为什么 / 怎么验证 / 怎么回滚
- **`README.md` 不要动** —— 它是上游的文件，改它会增加 rebase 冲突。本 fork 的记录一律放 `FORK.md`
- **构建产物与游戏原版 DLL 不入库** —— 见 `.gitignore`
