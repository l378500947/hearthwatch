// ============================================================
//  Hearthwatch Arena —— 奖励可配置化补丁（patch: PrizeOverrides）
//  作者：由 Hermes 为亮哥生成，非上游代码。
//  作用：让管理员在 cfg 里按 tier 覆盖 Prize（通关大奖），
//        不必反编译 DLL，也不必每次更新后重打补丁改数值。
//
//  设计要点：
//   1. cfg 值留空 = 沿用 Tables.cs 内置默认（保证不破坏原行为）
//   2. 解析失败的行会被跳过并记入 MissingPrefabs，面板可见
//      （复用上游已有的自我诊断机制，不引入新的失败模式）
//   3. 不修改 Tables.cs，只在其消费点包一层 —— 改动面最小
//   4. 支持热重载：解析结果缓存在 SettingChanged 时失效
// ============================================================

using System;
using System.Collections.Generic;
using System.Globalization;

namespace HearthwatchArena
{
    /// <summary>
    /// 把 cfg 里的 "Item:Count, Item:Count" 字符串解析成奖励表。
    /// 返回 null 表示「未配置或全部解析失败」→ 调用方应回退到内置默认。
    /// </summary>
    internal static class PrizeOverrides
    {
        // tier(1..8) → 解析好的奖励数组；null 值表示该 tier 未覆盖
        private static readonly Dictionary<int, (string item, int count)[]> _cache
            = new Dictionary<int, (string item, int count)[]>();

        // 由 Plugin 在 cfg 加载/重载时调用，写入原始字符串
        private static readonly Dictionary<int, string> _raw = new Dictionary<int, string>();

        public static void SetRaw(int tier, string value)
        {
            if (_raw.TryGetValue(tier, out var old) && old == value) return; // 无变化
            _raw[tier] = value;
            _cache.Remove(tier);   // 失效缓存，下次取用时重新解析
        }

        /// <summary>
        /// 取该 tier 的奖励表。
        /// 配置了且解析成功 → 返回自定义；否则返回 fallback（内置默认）。
        /// </summary>
        public static (string item, int count)[] For(int tier, (string item, int count)[] fallback)
        {
            if (_cache.TryGetValue(tier, out var cached) && cached != null)
                return cached;

            if (!_raw.TryGetValue(tier, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                _cache[tier] = null;   // 记录「未覆盖」，避免重复判断
                return fallback;
            }

            var parsed = Parse(raw);
            if (parsed == null || parsed.Length == 0)
            {
                // 配置了但一行都没解析成功 → 回退默认，并报警
                ReportBad(raw);
                _cache[tier] = null;
                return fallback;
            }

            _cache[tier] = parsed;
            return parsed;
        }

        /// <summary>
        /// 解析 "Ruby:2, Coins:60" → 数组。
        /// 分隔符兼容中英文逗号与分号；数量支持 :x 或 *x 或 x（默认 1）。
        /// 单行解析失败只跳过该行，不影响其它行。
        /// </summary>
        private static (string item, int count)[] Parse(string raw)
        {
            var result = new List<(string, int)>();
            var parts = raw.Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                var s = part.Trim();
                if (s.Length == 0) continue;

                string name;
                int count = 1;

                var sepIdx = s.IndexOfAny(new[] { ':', '*', '×' });
                if (sepIdx > 0)
                {
                    name = s.Substring(0, sepIdx).Trim();
                    var numStr = s.Substring(sepIdx + 1).Trim();
                    if (!int.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count <= 0)
                    {
                        ReportBad(part);
                        continue;
                    }
                }
                else
                {
                    name = s;
                }

                if (name.Length == 0) continue;

                // 名字合法性交给游戏运行时的 GetPrefab 校验（上游已有机制），
                // 这里不预判，避免与游戏版本更新脱节。
                result.Add((name, count));
            }

            return result.Count == 0 ? null : result.ToArray();
        }

        private static void ReportBad(string bad)
        {
            try
            {
                // 复用上游的 missingPrefabs 上报通道，面板可见
                if (!ArenaBuilder.MissingPrefabs.Contains("cfg:" + bad))
                    ArenaBuilder.MissingPrefabs.Add("cfg:" + bad);
            }
            catch { /* 上报失败不影响主流程 */ }
        }

        /// <summary>cfg 热重载时清空全部缓存。</summary>
        public static void Clear()
        {
            _cache.Clear();
        }
    }
}
