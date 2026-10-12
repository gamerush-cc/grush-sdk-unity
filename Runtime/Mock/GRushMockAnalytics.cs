using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace GRushSdk
{
    /// <summary>
    /// モックが分析イベントに掛ける、GameRush と同じ検査。GameRush と同じ順に確かめ、最初に当たった理由で拒む。
    /// 通ったイベントは、GameRush へ渡すのと同じ形の JSON に作り直す。
    /// </summary>
    internal static class GRushAnalyticsRules
    {
        public const int MaxKeyLength = 64;
        public const int MaxShortKeyLength = 32;
        public const int MaxFieldKeyLength = 32;
        public const int MaxFields = 3;
        public const int MaxTextCodePoints = 40;
        public const int MinStep = 1;
        public const int MaxStep = 100;
        public const double MaxAbsCustomValue = 1e9;
        public const double MaxAmount = 1e12;
        public const double MaxAbsEndingBalance = 1e15;

        /// <summary>通れば作り直したイベントの JSON、拒めば null と理由。</summary>
        public static string Sanitize(GRushAnalyticsEvent analyticsEvent, out string reason)
        {
            reason = null;
            if (analyticsEvent == null)
            {
                reason = "shape";
                return null;
            }
            var kind = analyticsEvent.Kind;
            if (
                kind != GRushAnalyticsEvent.KindOnboarding
                && kind != GRushAnalyticsEvent.KindFunnel
                && kind != GRushAnalyticsEvent.KindCustom
                && kind != GRushAnalyticsEvent.KindEconomy
            )
            {
                reason = "kind";
                return null;
            }
            List<KeyValuePair<string, string>> fields;
            if (!SanitizeFields(analyticsEvent.Fields, out fields))
            {
                reason = "fields";
                return null;
            }
            var parts = new List<string>();
            parts.Add("\"kind\":" + GRushAnalyticsEvent.Escape(kind));
            if (kind == GRushAnalyticsEvent.KindCustom)
            {
                reason = SanitizeCustom(analyticsEvent, parts);
            }
            else if (kind == GRushAnalyticsEvent.KindEconomy)
            {
                reason = SanitizeEconomy(analyticsEvent, parts);
            }
            else
            {
                reason = SanitizeStep(analyticsEvent, parts);
            }
            if (reason != null)
            {
                return null;
            }
            if (fields != null)
            {
                var builder = new StringBuilder("\"fields\":{");
                for (var index = 0; index < fields.Count; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(',');
                    }
                    builder
                        .Append(GRushAnalyticsEvent.Escape(fields[index].Key))
                        .Append(':')
                        .Append(GRushAnalyticsEvent.Escape(fields[index].Value));
                }
                parts.Add(builder.Append('}').ToString());
            }
            return "{" + string.Join(",", parts.ToArray()) + "}";
        }

        private static string SanitizeStep(GRushAnalyticsEvent analyticsEvent, List<string> parts)
        {
            if (analyticsEvent.Step < MinStep || analyticsEvent.Step > MaxStep)
            {
                return "step";
            }
            string stepName;
            if (!OptionalText(analyticsEvent.StepName, out stepName))
            {
                return "text";
            }
            if (analyticsEvent.Kind == GRushAnalyticsEvent.KindFunnel)
            {
                if (!IsKey(analyticsEvent.Funnel, MaxKeyLength, false))
                {
                    return "name";
                }
                parts.Add("\"funnel\":" + GRushAnalyticsEvent.Escape(analyticsEvent.Funnel));
                parts.Add("\"step\":" + analyticsEvent.Step.ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(analyticsEvent.FunnelSessionId))
                {
                    if (!IsKey(analyticsEvent.FunnelSessionId, MaxKeyLength, false))
                    {
                        return "name";
                    }
                    parts.Add("\"funnelSessionId\":" + GRushAnalyticsEvent.Escape(analyticsEvent.FunnelSessionId));
                }
            }
            else
            {
                parts.Add("\"step\":" + analyticsEvent.Step.ToString(CultureInfo.InvariantCulture));
            }
            if (stepName != null)
            {
                parts.Add("\"stepName\":" + GRushAnalyticsEvent.Escape(stepName));
            }
            return null;
        }

        private static string SanitizeCustom(GRushAnalyticsEvent analyticsEvent, List<string> parts)
        {
            if (!IsKey(analyticsEvent.Name, MaxKeyLength, false))
            {
                return "name";
            }
            var value = analyticsEvent.Value;
            if (!IsFinite(value) || Math.Abs(value) > MaxAbsCustomValue)
            {
                return "value";
            }
            parts.Add("\"name\":" + GRushAnalyticsEvent.Escape(analyticsEvent.Name));
            parts.Add("\"value\":" + GRushAnalyticsEvent.Number(value));
            return null;
        }

        private static string SanitizeEconomy(GRushAnalyticsEvent analyticsEvent, List<string> parts)
        {
            var flowType = analyticsEvent.FlowType;
            if (flowType != "source" && flowType != "sink")
            {
                return "flowType";
            }
            if (!IsKey(analyticsEvent.Currency, MaxShortKeyLength, false))
            {
                return "currency";
            }
            var amount = analyticsEvent.Amount;
            if (!IsFinite(amount) || amount <= 0 || amount > MaxAmount)
            {
                return "amount";
            }
            var endingBalance = analyticsEvent.EndingBalance;
            if (!IsFinite(endingBalance) || Math.Abs(endingBalance) > MaxAbsEndingBalance)
            {
                return "endingBalance";
            }
            if (!IsKey(analyticsEvent.TransactionType, MaxShortKeyLength, false))
            {
                return "transactionType";
            }
            string itemSku;
            if (!OptionalText(analyticsEvent.ItemSku, out itemSku))
            {
                return "text";
            }
            parts.Add("\"flowType\":" + GRushAnalyticsEvent.Escape(flowType));
            parts.Add("\"currency\":" + GRushAnalyticsEvent.Escape(analyticsEvent.Currency));
            parts.Add("\"amount\":" + GRushAnalyticsEvent.Number(amount));
            parts.Add("\"endingBalance\":" + GRushAnalyticsEvent.Number(endingBalance));
            parts.Add("\"transactionType\":" + GRushAnalyticsEvent.Escape(analyticsEvent.TransactionType));
            if (itemSku != null)
            {
                parts.Add("\"itemSku\":" + GRushAnalyticsEvent.Escape(itemSku));
            }
            return null;
        }

        // null と空は省略。キーと値を作り直し、値は1行に直したものを持つ。直した結果が空の値は拒む。
        private static bool SanitizeFields(
            IDictionary<string, string> fields,
            out List<KeyValuePair<string, string>> normalized
        )
        {
            normalized = null;
            if (fields == null)
            {
                return true;
            }
            if (fields.Count > MaxFields)
            {
                return false;
            }
            var list = new List<KeyValuePair<string, string>>();
            foreach (var entry in fields)
            {
                var text = LineText(entry.Value);
                if (!IsKey(entry.Key, MaxFieldKeyLength, true) || string.IsNullOrEmpty(text))
                {
                    return false;
                }
                list.Add(new KeyValuePair<string, string>(entry.Key, text));
            }
            normalized = list.Count == 0 ? null : list;
            return true;
        }

        // 省略（null）はそのまま省略。1行に直した結果が空なら省略と同じ。長すぎるか孤立サロゲートを含めば false。
        private static bool OptionalText(string value, out string text)
        {
            text = null;
            if (value == null)
            {
                return true;
            }
            var line = LineText(value);
            if (line == null)
            {
                return false;
            }
            text = line.Length == 0 ? null : line;
            return true;
        }

        /// <summary>
        /// 1行のテキストに直す。制御文字・行と段落の区切り・双方向の上書きと isolate の連なりを1つの空白にし、
        /// 前後の空白を落とす。長さはコードポイントで数える。見えない文字だけなら空文字、
        /// null・長すぎる・孤立サロゲートを含むなら null。
        /// </summary>
        internal static string LineText(string value)
        {
            if (value == null)
            {
                return null;
            }
            var builder = new StringBuilder(value.Length);
            var inRun = false;
            foreach (var c in value)
            {
                if (IsLineBreakingOrControl(c))
                {
                    if (!inRun)
                    {
                        builder.Append(' ');
                    }
                    inRun = true;
                    continue;
                }
                inRun = false;
                builder.Append(c);
            }
            var text = Trim(builder.ToString());
            var codePoints = 0;
            for (var index = 0; index < text.Length; index++)
            {
                var c = text[index];
                if (char.IsHighSurrogate(c))
                {
                    if (index + 1 >= text.Length || !char.IsLowSurrogate(text[index + 1]))
                    {
                        return null;
                    }
                    index++;
                }
                else if (char.IsLowSurrogate(c))
                {
                    return null;
                }
                codePoints++;
            }
            if (codePoints > MaxTextCodePoints)
            {
                return null;
            }
            return IsVisiblyBlank(text) ? string.Empty : text;
        }

        private static bool IsLineBreakingOrControl(char c)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            return category == UnicodeCategory.Control
                || category == UnicodeCategory.LineSeparator
                || category == UnicodeCategory.ParagraphSeparator
                || (c >= '\u202A' && c <= '\u202E')
                || (c >= '\u2066' && c <= '\u2069');
        }

        // JavaScript の trim と同じ空白（空白の区切り文字・タブなど・BOM）を落とす。制御文字と行の区切りは
        // LineText で空白に直してあるので、ここに来ない。
        private static string Trim(string value)
        {
            var start = 0;
            var end = value.Length;
            while (start < end && IsTrimmed(value[start]))
            {
                start++;
            }
            while (end > start && IsTrimmed(value[end - 1]))
            {
                end--;
            }
            return value.Substring(start, end - start);
        }

        private static bool IsTrimmed(char c)
        {
            return c == '\uFEFF'
                || c == '\t'
                || c == '\v'
                || c == '\f'
                || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.SpaceSeparator;
        }

        // 空白・制御文字・書式文字・既定で描かない文字と、空白にしか見えない字形の文字だけなら true。
        private static bool IsVisiblyBlank(string text)
        {
            for (var index = 0; index < text.Length; index++)
            {
                var codePoint = char.ConvertToUtf32(text, index);
                var category = CharUnicodeInfo.GetUnicodeCategory(text, index);
                if (char.IsHighSurrogate(text[index]))
                {
                    index++;
                }
                if (
                    category == UnicodeCategory.Control
                    || category == UnicodeCategory.Format
                    || category == UnicodeCategory.SpaceSeparator
                    || category == UnicodeCategory.LineSeparator
                    || category == UnicodeCategory.ParagraphSeparator
                    || IsDefaultIgnorable(codePoint)
                    || codePoint == 0x115F
                    || codePoint == 0x1160
                    || codePoint == 0x3164
                    || codePoint == 0xFFA0
                    || codePoint == 0x2800
                )
                {
                    continue;
                }
                return false;
            }
            return true;
        }

        // Unicode の Default_Ignorable_Code_Point。
        private static bool IsDefaultIgnorable(int codePoint)
        {
            return codePoint == 0x00AD
                || codePoint == 0x034F
                || codePoint == 0x061C
                || (codePoint >= 0x115F && codePoint <= 0x1160)
                || (codePoint >= 0x17B4 && codePoint <= 0x17B5)
                || (codePoint >= 0x180B && codePoint <= 0x180F)
                || (codePoint >= 0x200B && codePoint <= 0x200F)
                || (codePoint >= 0x202A && codePoint <= 0x202E)
                || (codePoint >= 0x2060 && codePoint <= 0x206F)
                || codePoint == 0x3164
                || (codePoint >= 0xFE00 && codePoint <= 0xFE0F)
                || codePoint == 0xFEFF
                || codePoint == 0xFFA0
                || (codePoint >= 0xFFF0 && codePoint <= 0xFFF8)
                || (codePoint >= 0x1BCA0 && codePoint <= 0x1BCA3)
                || (codePoint >= 0x1D173 && codePoint <= 0x1D17A)
                || (codePoint >= 0xE0000 && codePoint <= 0xE0FFF);
        }

        // 名前の鍵（英数字と ._:-）か、field のキー（英数字と _）。
        private static bool IsKey(string value, int maxLength, bool fieldKey)
        {
            if (string.IsNullOrEmpty(value) || value.Length > maxLength)
            {
                return false;
            }
            foreach (var c in value)
            {
                var alphanumeric =
                    (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                var punctuation = fieldKey ? c == '_' : c == '.' || c == '_' || c == ':' || c == '-';
                if (!alphanumeric && !punctuation)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    /// <summary>
    /// モックが受けた分析イベント。1件ずつログに出し、直近の分をメモリに持つ。どこへも送らない。
    /// 拒んだ理由は、GameRush と同じく理由ごとに1回だけ警告する。ほかのスレッドから呼ばれても壊れないよう lock で囲む。
    /// </summary>
    internal static class GRushMockAnalytics
    {
        public const int MaxEvents = 200;

        private static readonly object Gate = new object();
        private static readonly List<string> Events = new List<string>();
        private static readonly HashSet<string> Warned = new HashSet<string>();

        public static bool Record(GRushAnalyticsEvent analyticsEvent)
        {
            string reason;
            var json = GRushAnalyticsRules.Sanitize(analyticsEvent, out reason);
            if (json == null)
            {
                bool first;
                lock (Gate)
                {
                    first = Warned.Add(reason);
                }
                if (first)
                {
                    Debug.LogWarning("[GRushMock] analytics event rejected (" + reason + ").");
                }
                return false;
            }
            lock (Gate)
            {
                Events.Add(json);
                if (Events.Count > MaxEvents)
                {
                    Events.RemoveRange(0, Events.Count - MaxEvents);
                }
            }
            Debug.Log("[GRushMock] analytics " + json);
            return true;
        }

        public static string[] Snapshot()
        {
            lock (Gate)
            {
                return Events.ToArray();
            }
        }

        public static void Reset()
        {
            lock (Gate)
            {
                Events.Clear();
                Warned.Clear();
            }
        }
    }
}