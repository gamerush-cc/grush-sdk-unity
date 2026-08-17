using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace GRushSdk.Editor
{
    internal sealed class GRushGameSummary
    {
        public string Id;
        public string Title;
        public string Visibility;
        public string ReviewStatus;
        public string RequestedVisibility;
    }

    internal sealed class GRushLeaderboardDraft
    {
        public string Key = "";
        public string Title = "";
        public int SortIndex;
        public int ValueTypeIndex;
        public int AggregationIndex;
        public int PeriodIndex;
        public bool HasRange;
        public double MinValue;
        public double MaxValue = 1000000;

        public static readonly string[] Sorts = { "desc", "asc" };
        public static readonly string[] ValueTypes = { "int", "float", "duration_ms" };
        public static readonly string[] Aggregations = { "best", "last", "sum" };
        public static readonly string[] Periods = { "all_time", "daily", "weekly", "monthly" };

        public string ToRequestBody()
        {
            var builder = new StringBuilder("{");
            builder.Append("\"title\":").Append(GRushJsonText.Escape(Title));
            builder.Append(",\"sort\":").Append(GRushJsonText.Escape(Sorts[SortIndex]));
            builder
                .Append(",\"valueType\":")
                .Append(GRushJsonText.Escape(ValueTypes[ValueTypeIndex]));
            builder
                .Append(",\"aggregation\":")
                .Append(GRushJsonText.Escape(Aggregations[AggregationIndex]));
            builder.Append(",\"period\":").Append(GRushJsonText.Escape(Periods[PeriodIndex]));
            if (HasRange)
            {
                builder.Append(",\"minValue\":").Append(GRushJsonText.Number(MinValue));
                builder.Append(",\"maxValue\":").Append(GRushJsonText.Number(MaxValue));
            }
            return builder.Append('}').ToString();
        }
    }

    internal static class GRushEditorApi
    {
        public static IEnumerator ListGames(
            GRushEditorClient client,
            Action<List<GRushGameSummary>> onDone,
            Action<string> onError
        )
        {
            var slot = new GRushHttpSlot();
            yield return client.Get("/api/games", slot);
            if (!slot.Result.Ok || slot.Result.Json == null)
            {
                onError("ゲームの一覧を取得できませんでした: " + slot.Result.Message());
                yield break;
            }
            var games = new List<GRushGameSummary>();
            foreach (var game in slot.Result.Json.Get("games").Items)
            {
                games.Add(
                    new GRushGameSummary
                    {
                        Id = game.Get("id").AsString(""),
                        Title = game.Get("title").AsString(""),
                        Visibility = game.Get("visibility").AsString(""),
                        ReviewStatus = game.Get("review_status").AsString(""),
                        RequestedVisibility = game.Get("requested_visibility").AsString(null),
                    }
                );
            }
            onDone(games);
        }

        /// <summary>
        /// **再試行しない。** サーバに冪等キーが無いので、応答を取りこぼした
        /// まま送り直すと2つ目のゲームができる。
        /// </summary>
        public static IEnumerator CreateGame(
            GRushEditorClient client,
            string title,
            string description,
            string visibility,
            Action<GRushGameSummary> onDone,
            Action<string> onError
        )
        {
            var body = new StringBuilder("{");
            body.Append("\"title\":").Append(GRushJsonText.Escape(title));
            body.Append(",\"description\":").Append(GRushJsonText.Escape(description));
            body.Append(",\"visibility\":").Append(GRushJsonText.Escape(visibility));
            body.Append(",\"acceptTerms\":true}");

            var slot = new GRushHttpSlot();
            yield return client.Post("/api/games", body.ToString(), slot);
            if (!slot.Result.Ok || slot.Result.Json == null)
            {
                onError(
                    "ゲームを作れませんでした: "
                        + slot.Result.Message()
                        + "（自動では再試行しません。Studio に作られていないか確認してください）"
                );
                yield break;
            }
            var game = slot.Result.Json.Get("game");
            onDone(
                new GRushGameSummary
                {
                    Id = game.Get("id").AsString(""),
                    Title = game.Get("title").AsString(title),
                    Visibility = game.Get("visibility").AsString(""),
                    ReviewStatus = game.Get("review_status").AsString(""),
                    RequestedVisibility = game.Get("requested_visibility").AsString(null),
                }
            );
        }

        /// <summary>
        /// key での冪等な適用。無ければ作り、内容が違えば更新し、同じなら
        /// 何もしない。投稿が1件でもある枠の意味づけ（sort / valueType /
        /// aggregation / period）を変えると 409 で返る。
        /// </summary>
        public static IEnumerator DeclareLeaderboard(
            GRushEditorClient client,
            string gameId,
            GRushLeaderboardDraft draft,
            Action onDone,
            Action<string> onError
        )
        {
            var slot = new GRushHttpSlot();
            yield return client.Put(
                "/api/games/" + gameId + "/leaderboards/" + Uri.EscapeDataString(draft.Key),
                draft.ToRequestBody(),
                slot
            );
            if (!slot.Result.Ok)
            {
                onError("ランキングを宣言できませんでした: " + slot.Result.Message());
                yield break;
            }
            onDone();
        }
    }
}
