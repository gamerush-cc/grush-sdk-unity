using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GRushSdk
{
    /// <summary>
    /// エディタ用のランキング。作者が実サーバへ繋がずに投稿と表示を試せないと
    /// ランキングを組み込んだゲームを一切テストできないため、モックは付属品
    /// ではなく本体の一部として扱う。
    ///
    /// 実サーバ側の縛り（値域・自己ベスト集約・自己申告であること）は同じ形で
    /// 再現する。session 束縛と有効プレイ 10 秒はエディタでは意味を持たない
    /// ので省く。
    /// </summary>
    public static class GRushMockLeaderboards
    {
        private sealed class Entry
        {
            public string PseudoId;
            public string DisplayName;
            public string AvatarUrl;
            public bool IsGuest;
            public double Value;
            public bool IsSelf;
        }

        private sealed class Board
        {
            public string Key;
            public string Title;
            public string Sort = "desc";
            public string ValueType = "int";
            public string Aggregation = "best";
            public string Period = "all_time";
            public double MinValue;
            public double MaxValue = 1000000;
            public readonly List<Entry> Entries = new List<Entry>();
        }

        private static readonly Dictionary<string, Board> boards = new Dictionary<string, Board>();

        /// <summary>
        /// エディタで使うランキング枠を宣言する。実環境では Studio が持つ役割。
        /// </summary>
        public static void Define(
            string key,
            string title,
            string sort = "desc",
            string valueType = "int",
            string aggregation = "best",
            double minValue = 0,
            double maxValue = 1000000
        )
        {
            boards[key] = new Board
            {
                Key = key,
                Title = string.IsNullOrEmpty(title) ? key : title,
                Sort = sort,
                ValueType = valueType,
                Aggregation = aggregation,
                MinValue = minValue,
                MaxValue = maxValue,
            };
        }

        /// <summary>他プレイヤーの行を積む。順位表示や同意の出し分けの確認に使う。</summary>
        public static void AddRival(
            string key,
            string displayName,
            double value,
            string avatarUrl = null,
            bool isGuest = false
        )
        {
            var board = Ensure(key);
            board.Entries.Add(
                new Entry
                {
                    PseudoId = "mock-rival-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    DisplayName = isGuest ? null : displayName,
                    AvatarUrl = isGuest ? null : avatarUrl,
                    IsGuest = isGuest,
                    Value = value,
                    IsSelf = false,
                }
            );
        }

        public static void Reset()
        {
            boards.Clear();
        }

        private static Board Ensure(string key)
        {
            Board board;
            if (!boards.TryGetValue(key, out board))
            {
                board = new Board { Key = key, Title = key };
                boards[key] = board;
            }
            return board;
        }

        internal static bool TryGet(string key, out object handle)
        {
            Board board;
            var found = boards.TryGetValue(key, out board);
            handle = board;
            return found;
        }

        internal static string ListJson()
        {
            var builder = new StringBuilder("{\"leaderboards\":[");
            var first = true;
            foreach (var board in boards.Values)
            {
                if (!first)
                {
                    builder.Append(',');
                }
                first = false;
                builder.Append('{');
                builder.Append("\"key\":").Append(GRushWire.Escape(board.Key));
                builder.Append(",\"title\":").Append(GRushWire.Escape(board.Title));
                builder.Append(",\"sort\":").Append(GRushWire.Escape(board.Sort));
                builder.Append(",\"valueType\":").Append(GRushWire.Escape(board.ValueType));
                builder.Append(",\"aggregation\":").Append(GRushWire.Escape(board.Aggregation));
                builder.Append(",\"period\":").Append(GRushWire.Escape(board.Period));
                builder.Append(",\"minValue\":").Append(Num(board.MinValue));
                builder.Append(",\"maxValue\":").Append(Num(board.MaxValue));
                builder.Append('}');
            }
            return builder.Append("]}").ToString();
        }

        internal static GRushRpcResponse Submit(string key, double value, string selfPseudoId)
        {
            Board board;
            if (!boards.TryGetValue(key, out board))
            {
                return GRushRpcResponse.Failure(
                    GRushErrorCode.InvalidParams,
                    "No mock leaderboard named " + key + ". Call GRushMockLeaderboards.Define first."
                );
            }
            if (value < board.MinValue || value > board.MaxValue)
            {
                return GRushRpcResponse.Failure(
                    GRushErrorCode.InvalidParams,
                    "Score is outside the declared range."
                );
            }

            var self = board.Entries.Find(entry => entry.IsSelf);
            var updated = true;
            if (self == null)
            {
                self = new Entry
                {
                    PseudoId = selfPseudoId,
                    DisplayName = GRushMock.DisplayName,
                    AvatarUrl = GRushMock.AvatarUrl,
                    IsGuest = !GRushMock.SignedIn,
                    Value = value,
                    IsSelf = true,
                };
                board.Entries.Add(self);
            }
            else if (board.Aggregation == "sum")
            {
                self.Value += value;
            }
            else if (board.Aggregation == "best")
            {
                var better =
                    board.Sort == "desc" ? value > self.Value : value < self.Value;
                if (better)
                {
                    self.Value = value;
                }
                else
                {
                    updated = false;
                }
            }
            else
            {
                self.Value = value;
            }

            var ranked = Ranked(board);
            var rank = ranked.FindIndex(entry => entry.IsSelf) + 1;

            var builder = new StringBuilder("{\"result\":{");
            builder.Append("\"accepted\":true");
            builder.Append(",\"updated\":").Append(updated ? "true" : "false");
            builder.Append(",\"value\":").Append(Num(self.Value));
            builder.Append(",\"rank\":").Append(rank);
            builder.Append(",\"verified\":false");
            return GRushRpcResponse.Success(builder.Append("}}").ToString());
        }

        internal static GRushRpcResponse Page(string key, int limit, int offset, bool aroundMe)
        {
            Board board;
            if (!boards.TryGetValue(key, out board))
            {
                return GRushRpcResponse.Failure(
                    GRushErrorCode.InvalidParams,
                    "No mock leaderboard named " + key + ". Call GRushMockLeaderboards.Define first."
                );
            }

            var ranked = Ranked(board);
            var start = offset;
            var count = limit > 0 ? limit : 20;

            if (aroundMe)
            {
                var selfIndex = ranked.FindIndex(entry => entry.IsSelf);
                if (selfIndex < 0)
                {
                    // 自分がまだ載っていないときは空。先頭を返すと「1位」と読める。
                    return GRushRpcResponse.Success(PageJson(board, new List<Entry>(), 0, ranked.Count));
                }
                var range = limit > 0 ? limit : 5;
                start = Math.Max(0, selfIndex - range);
                count = range * 2 + 1;
            }

            var slice = new List<Entry>();
            for (var index = start; index < ranked.Count && slice.Count < count; index++)
            {
                slice.Add(ranked[index]);
            }
            return GRushRpcResponse.Success(PageJson(board, slice, start, ranked.Count));
        }

        private static List<Entry> Ranked(Board board)
        {
            var ranked = new List<Entry>(board.Entries);
            ranked.Sort(
                (a, b) => board.Sort == "asc" ? a.Value.CompareTo(b.Value) : b.Value.CompareTo(a.Value)
            );
            return ranked;
        }

        private static string PageJson(Board board, List<Entry> entries, int startIndex, int total)
        {
            var builder = new StringBuilder("{\"leaderboard\":{");
            builder.Append("\"key\":").Append(GRushWire.Escape(board.Key));
            builder.Append(",\"title\":").Append(GRushWire.Escape(board.Title));
            builder.Append(",\"sort\":").Append(GRushWire.Escape(board.Sort));
            builder.Append(",\"valueType\":").Append(GRushWire.Escape(board.ValueType));
            builder.Append(",\"period\":").Append(GRushWire.Escape(board.Period));
            builder.Append(",\"periodKey\":\"\"");
            builder.Append(",\"verified\":false");
            builder.Append(",\"total\":").Append(total);
            builder.Append(",\"entries\":[");
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (index > 0)
                {
                    builder.Append(',');
                }
                builder.Append('{');
                builder.Append("\"rank\":").Append(startIndex + index + 1);
                builder.Append(",\"pseudoId\":").Append(GRushWire.Escape(entry.PseudoId ?? ""));
                builder.Append(",\"displayName\":");
                builder.Append(
                    entry.DisplayName == null ? "null" : GRushWire.Escape(entry.DisplayName)
                );
                builder.Append(",\"avatarUrl\":");
                builder.Append(entry.AvatarUrl == null ? "null" : GRushWire.Escape(entry.AvatarUrl));
                builder.Append(",\"isGuest\":").Append(entry.IsGuest ? "true" : "false");
                builder.Append(",\"value\":").Append(Num(entry.Value));
                builder.Append(",\"submittedAt\":\"\"");
                builder.Append(",\"isSelf\":").Append(entry.IsSelf ? "true" : "false");
                builder.Append('}');
            }
            return builder.Append("]}}").ToString();
        }

        private static string Num(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
