using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GRushSdk
{
    /// <summary>エコノミーのイベントの向き。入手が Source、消費が Sink。</summary>
    public enum GRushEconomyFlowType
    {
        Source,
        Sink,
    }

    /// <summary>
    /// GameRush の標準の取引の種類。ほかの値（英数字と <c>._:-</c> で 32 文字まで）も渡せる。
    /// </summary>
    public static class GRushEconomyTransactionType
    {
        public const string TimedReward = "TimedReward";
        public const string Onboarding = "Onboarding";
        public const string Shop = "Shop";
        public const string Gameplay = "Gameplay";
        public const string ContextualPurchase = "ContextualPurchase";
    }

    /// <summary>
    /// 1件の分析イベント。<see cref="GRushAnalyticsApi"/> が作り、<see cref="IGRushAnalyticsBackend"/> へ渡す。
    /// 値は確かめる前のもので、確かめるのは GameRush（エディタではモック）。
    /// </summary>
    public sealed class GRushAnalyticsEvent
    {
        public const string KindOnboarding = "onboarding";
        public const string KindFunnel = "funnel";
        public const string KindCustom = "custom";
        public const string KindEconomy = "economy";

        public string Kind { get; internal set; }
        public int Step { get; internal set; }
        public string StepName { get; internal set; }
        public string Funnel { get; internal set; }
        public string FunnelSessionId { get; internal set; }
        public string Name { get; internal set; }
        public double Value { get; internal set; }
        /// <summary><c>"source"</c> か <c>"sink"</c>。列挙に無い値を渡されたときは null。</summary>
        public string FlowType { get; internal set; }
        public string Currency { get; internal set; }
        public double Amount { get; internal set; }
        public double EndingBalance { get; internal set; }
        public string TransactionType { get; internal set; }
        public string ItemSku { get; internal set; }
        public IDictionary<string, string> Fields { get; internal set; }

        internal GRushAnalyticsEvent() { }

        /// <summary>
        /// GameRush のランタイムの <c>log(kind, params)</c> へ渡す params の JSON。kind は含めない。
        /// 有限でない数は JSON に書けないので null にし、GameRush にその値を拒ませる。
        /// </summary>
        public string ParamsJson()
        {
            var parts = new List<string>();
            switch (Kind)
            {
                case KindOnboarding:
                    parts.Add("\"step\":" + Step.ToString(CultureInfo.InvariantCulture));
                    AddText(parts, "stepName", StepName);
                    break;
                case KindFunnel:
                    AddText(parts, "funnel", Funnel);
                    AddText(parts, "funnelSessionId", FunnelSessionId);
                    parts.Add("\"step\":" + Step.ToString(CultureInfo.InvariantCulture));
                    AddText(parts, "stepName", StepName);
                    break;
                case KindCustom:
                    AddText(parts, "name", Name);
                    parts.Add("\"value\":" + Number(Value));
                    break;
                case KindEconomy:
                    AddText(parts, "flowType", FlowType);
                    AddText(parts, "currency", Currency);
                    parts.Add("\"amount\":" + Number(Amount));
                    parts.Add("\"endingBalance\":" + Number(EndingBalance));
                    AddText(parts, "transactionType", TransactionType);
                    AddText(parts, "itemSku", ItemSku);
                    break;
            }
            if (Fields != null)
            {
                parts.Add("\"fields\":" + FieldsJson(Fields));
            }
            return "{" + string.Join(",", parts.ToArray()) + "}";
        }

        internal static string Number(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return "null";
            }
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        internal static string FieldsJson(IDictionary<string, string> fields)
        {
            var builder = new StringBuilder("{");
            var first = true;
            foreach (var entry in fields)
            {
                if (!first)
                {
                    builder.Append(',');
                }
                first = false;
                builder
                    .Append(GRushWire.Escape(entry.Key ?? string.Empty))
                    .Append(':')
                    .Append(entry.Value == null ? "null" : GRushWire.Escape(entry.Value));
            }
            return builder.Append('}').ToString();
        }

        private static void AddText(List<string> parts, string name, string value)
        {
            if (value != null)
            {
                parts.Add("\"" + name + "\":" + GRushWire.Escape(value));
            }
        }
    }

    /// <summary>分析イベントを受ける backend。WebGL では GameRush のランタイム、エディタではモック。</summary>
    public interface IGRushAnalyticsBackend
    {
        bool IsAnalyticsAvailable { get; }
        bool LogAnalytics(GRushAnalyticsEvent analyticsEvent);
    }

    /// <summary>
    /// 作者のアナリティクスへ送るイベント。ほかの API と違い、どれも同期で bool を返し、例外を投げない。
    /// true は GameRush へ渡したことで、保存されたことではない。形が不正なとき、ProtocolVersion 6 未満の
    /// GameRush、GameRush の外では false。名前・値・fields にプレイヤーを識別できる情報を入れない。
    /// </summary>
    public sealed class GRushAnalyticsApi
    {
        /// <summary>GameRush の中で動いていて、分析イベントを送れるか。</summary>
        public bool IsAvailable
        {
            get
            {
                try
                {
                    var backend = GRush.Backend as IGRushAnalyticsBackend;
                    return GRush.IsAnalyticsAvailable && backend != null && backend.IsAnalyticsAvailable;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// オンボーディングのステップ。step は 1〜100。stepName と fields の値は1行のテキストで 40 文字まで。
        /// fields は3つまでで、キーは英数字と _ で 32 文字まで。
        /// </summary>
        public bool LogOnboardingStep(
            int step,
            string stepName = null,
            IDictionary<string, string> fields = null
        )
        {
            return Log(
                new GRushAnalyticsEvent
                {
                    Kind = GRushAnalyticsEvent.KindOnboarding,
                    Step = step,
                    StepName = stepName,
                    Fields = fields,
                }
            );
        }

        /// <summary>
        /// ファネルのステップ。funnel と funnelSessionId は英数字と ._:- で 64 文字まで。funnelSessionId は
        /// null で省略できる。同じファネルを1回のプレイで何度も通るなら、通るたびに別の funnelSessionId を渡す。
        /// </summary>
        public bool LogFunnelStep(
            string funnel,
            string funnelSessionId,
            int step,
            string stepName = null,
            IDictionary<string, string> fields = null
        )
        {
            return Log(
                new GRushAnalyticsEvent
                {
                    Kind = GRushAnalyticsEvent.KindFunnel,
                    Funnel = funnel,
                    FunnelSessionId = funnelSessionId,
                    Step = step,
                    StepName = stepName,
                    Fields = fields,
                }
            );
        }

        /// <summary>カスタムイベント。name は英数字と ._:- で 64 文字まで。value は絶対値 1e9 まで。</summary>
        public bool LogCustomEvent(
            string name,
            double value = 1,
            IDictionary<string, string> fields = null
        )
        {
            return Log(
                new GRushAnalyticsEvent
                {
                    Kind = GRushAnalyticsEvent.KindCustom,
                    Name = name,
                    Value = value,
                    Fields = fields,
                }
            );
        }

        /// <summary>
        /// 通貨の入手と消費。amount は正の値で 1e12 まで、endingBalance はその後の残高で絶対値 1e15 まで。
        /// currency と transactionType は英数字と ._:- で 32 文字まで。transactionType の標準の値は
        /// <see cref="GRushEconomyTransactionType"/>。itemSku は1行のテキストで 40 文字まで。
        /// </summary>
        public bool LogEconomyEvent(
            GRushEconomyFlowType flowType,
            string currency,
            double amount,
            double endingBalance,
            string transactionType,
            string itemSku = null,
            IDictionary<string, string> fields = null
        )
        {
            return Log(
                new GRushAnalyticsEvent
                {
                    Kind = GRushAnalyticsEvent.KindEconomy,
                    FlowType = FlowTypeName(flowType),
                    Currency = currency,
                    Amount = amount,
                    EndingBalance = endingBalance,
                    TransactionType = transactionType,
                    ItemSku = itemSku,
                    Fields = fields,
                }
            );
        }

        private static string FlowTypeName(GRushEconomyFlowType flowType)
        {
            switch (flowType)
            {
                case GRushEconomyFlowType.Source:
                    return "source";
                case GRushEconomyFlowType.Sink:
                    return "sink";
                default:
                    return null;
            }
        }

        private static bool Log(GRushAnalyticsEvent analyticsEvent)
        {
            try
            {
                var backend = GRush.Backend as IGRushAnalyticsBackend;
                if (!GRush.IsAnalyticsAvailable || backend == null)
                {
                    return false;
                }
                return backend.LogAnalytics(analyticsEvent);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
