using System;
using System.Text;
using UnityEngine;

namespace GRushSdk
{
    [Serializable]
    internal class GRushProfileWire
    {
        public string displayName;
        public string avatarUrl;
    }

    [Serializable]
    internal class GRushPlayerWire
    {
        public string pseudoId;
        public bool isGuest;
        public bool profileConsent;
        public GRushProfileWire profile;
    }

    [Serializable]
    internal class GRushPeerWire
    {
        public int index;
        public string pseudoId;
        public string displayName;
        public string avatarUrl;
    }

    [Serializable]
    internal class GRushJoinWire
    {
        public string roomId;
        public string roomCode;
        public int localPeerIndex;
        public GRushPeerWire[] peers;
        public int hostIndex;
        public int epoch;
        public string transport;
        public double serverTimeMs;
    }

    [Serializable]
    internal class GRushErrorWire
    {
        public string code;
        public string message;
    }

    [Serializable]
    internal class GRushLeaderboardWire
    {
        public string key;
        public string title;
        public string sort;
        public string valueType;
        public string aggregation;
        public string period;
        public double minValue;
        public double maxValue;
    }

    [Serializable]
    internal class GRushLeaderboardListWire
    {
        public GRushLeaderboardWire[] leaderboards;
    }

    [Serializable]
    internal class GRushLeaderboardEntryWire
    {
        public int rank;
        public string pseudoId;
        public string displayName;
        public string avatarUrl;
        public bool isGuest;
        public double value;
        public string submittedAt;
        public bool isSelf;
    }

    [Serializable]
    internal class GRushLeaderboardPageWire
    {
        public string key;
        public string title;
        public string sort;
        public string valueType;
        public string period;
        public string periodKey;
        public bool verified;
        public GRushLeaderboardEntryWire[] entries;
        public int total;
    }

    [Serializable]
    internal class GRushLeaderboardPageEnvelope
    {
        public GRushLeaderboardPageWire leaderboard;
    }

    [Serializable]
    internal class GRushScoreResultWire
    {
        public bool accepted;
        public bool updated;
        public double value;
        public int rank;
        public bool verified;
    }

    [Serializable]
    internal class GRushScoreResultEnvelope
    {
        public GRushScoreResultWire result;
    }

    [Serializable]
    internal class GRushSubmitParamsWire
    {
        public string key;
        public double value;
    }

    [Serializable]
    internal class GRushLeaderboardQueryWire
    {
        public string key;
        public int limit;
        public int offset;
        public int range;
    }

    [Serializable]
    internal class GRushJoinParamsWire
    {
        public string mode;
        public string roomCode;
    }

    internal static class GRushWire
    {
        public static string Escape(string value)
        {
            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            foreach (var c in value)
            {
                if (c == '"' || c == '\\')
                {
                    builder.Append('\\').Append(c);
                }
                else if (c < ' ')
                {
                    builder.Append("\\u").Append(((int)c).ToString("x4"));
                }
                else
                {
                    builder.Append(c);
                }
            }
            return builder.Append('"').ToString();
        }

        public static string JoinParams(string mode, string roomCode)
        {
            var builder = new StringBuilder("{");
            builder.Append("\"mode\":").Append(Escape(string.IsNullOrEmpty(mode) ? "default" : mode));
            if (!string.IsNullOrEmpty(roomCode))
            {
                builder.Append(",\"roomCode\":").Append(Escape(roomCode));
            }
            return builder.Append('}').ToString();
        }

        public static GRushPlayer ToPlayer(GRushPlayerWire wire)
        {
            var player = new GRushPlayer
            {
                PseudoId = wire == null ? null : wire.pseudoId,
                IsGuest = wire != null && wire.isGuest,
                ProfileConsent = wire != null && wire.profileConsent,
            };
            if (wire != null && wire.profileConsent && wire.profile != null)
            {
                player.DisplayName = wire.profile.displayName;
                player.AvatarUrl = wire.profile.avatarUrl;
            }
            return player;
        }

        public static GRushPeer ToPeer(GRushPeerWire wire)
        {
            return new GRushPeer
            {
                Index = wire.index,
                PseudoId = wire.pseudoId,
                DisplayName = wire.displayName,
                AvatarUrl = wire.avatarUrl,
            };
        }

        public static string LeaderboardQuery(string key, int limit, int offset, int range)
        {
            var builder = new StringBuilder("{");
            builder.Append("\"key\":").Append(Escape(key ?? string.Empty));
            if (limit > 0)
            {
                builder.Append(",\"limit\":").Append(limit);
            }
            if (offset > 0)
            {
                builder.Append(",\"offset\":").Append(offset);
            }
            if (range > 0)
            {
                builder.Append(",\"range\":").Append(range);
            }
            return builder.Append('}').ToString();
        }

        public static string SubmitParams(
            string key,
            double value,
            string metadataJson,
            string operationId
        )
        {
            var builder = new StringBuilder("{");
            builder.Append("\"key\":").Append(Escape(key ?? string.Empty));
            builder
                .Append(",\"value\":")
                .Append(value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(metadataJson))
            {
                // metadata は呼び出し側が組み立てた生 JSON をそのまま載せる。
                // 壊れた JSON はサーバが 400 で弾く。
                builder.Append(",\"metadata\":").Append(metadataJson);
            }
            if (!string.IsNullOrEmpty(operationId))
            {
                builder.Append(",\"operationId\":").Append(Escape(operationId));
            }
            return builder.Append('}').ToString();
        }

        public static GRushLeaderboard ToLeaderboard(GRushLeaderboardWire wire)
        {
            return new GRushLeaderboard
            {
                Key = wire.key,
                Title = wire.title,
                Sort = wire.sort,
                ValueType = wire.valueType,
                Aggregation = wire.aggregation,
                Period = wire.period,
                MinValue = wire.minValue,
                MaxValue = wire.maxValue,
            };
        }

        public static GRushLeaderboardPage ToLeaderboardPage(
            GRushLeaderboardPageWire wire,
            string rawJson
        )
        {
            var entries = new GRushLeaderboardEntry[wire.entries == null ? 0 : wire.entries.Length];
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = wire.entries[index];
                entries[index] = new GRushLeaderboardEntry
                {
                    Rank = entry.rank,
                    PseudoId = entry.pseudoId,
                    DisplayName = entry.displayName,
                    AvatarUrl = entry.avatarUrl,
                    IsGuest = entry.isGuest,
                    Value = entry.value,
                    SubmittedAt = entry.submittedAt,
                    IsSelf = entry.isSelf,
                };
            }
            return new GRushLeaderboardPage
            {
                Key = wire.key,
                Title = wire.title,
                Sort = wire.sort,
                ValueType = wire.valueType,
                Period = wire.period,
                PeriodKey = wire.periodKey,
                Verified = wire.verified,
                Entries = entries,
                Total = wire.total,
                RawJson = rawJson,
            };
        }

        public static T Parse<T>(string json)
            where T : class
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }
            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
