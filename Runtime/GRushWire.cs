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
    internal class GRushPlayerStateWire
    {
        public string pseudoId;
        public int revision;
        public string updatedAt;
    }

    [Serializable]
    internal class GRushReportResultWire
    {
        public bool reported;
    }

    [Serializable]
    internal class GRushPlayerStateQueryWire
    {
        public string[] pseudoIds;
    }

    [Serializable]
    internal class GRushPlayerStateEnvelope
    {
        public GRushPlayerStateWire state;
    }

    [Serializable]
    internal class GRushPlayerStateListEnvelope
    {
        public GRushPlayerStateWire[] states;
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

        public static string PlayerStateParams(string payloadJson, int baseRevision)
        {
            var builder = new StringBuilder("{");
            if (baseRevision >= 0)
            {
                builder.Append("\"baseRevision\":").Append(baseRevision).Append(',');
            }
            // payload は呼び出し側が組み立てた生 JSON をそのまま載せる。
            // 壊れた JSON と 4KB 超はサーバが 400 で弾く。
            // **必ず最後に置く。** そうすると取り出しが「"payload": から
            // 末尾の } まで」で厳密に決まり、中身に何が入っても壊れない。
            builder
                .Append("\"payload\":")
                .Append(string.IsNullOrEmpty(payloadJson) ? "null" : payloadJson);
            return builder.Append('}').ToString();
        }

        public static string PlayerStateReportParams(string pseudoId)
        {
            return "{\"pseudoId\":" + Escape(pseudoId ?? string.Empty) + "}";
        }

        public static string PlayerStateQuery(string[] pseudoIds)
        {
            var builder = new StringBuilder("{\"pseudoIds\":[");
            if (pseudoIds != null)
            {
                for (var index = 0; index < pseudoIds.Length; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(',');
                    }
                    builder.Append(Escape(pseudoIds[index] ?? string.Empty));
                }
            }
            return builder.Append("]}").ToString();
        }

        public static GRushPlayerState ToPlayerState(GRushPlayerStateWire wire)
        {
            return new GRushPlayerState
            {
                PseudoId = wire.pseudoId,
                Revision = wire.revision,
                UpdatedAt = wire.updatedAt,
            };
        }

        /// <summary>
        /// <see cref="PlayerStateParams"/> が組んだ params から payload を取り出す。
        /// payload は必ず末尾にあるので、手書きの JSON パーサを持たずに済む。
        /// </summary>
        public static string ExtractPayloadJson(string paramsJson)
        {
            if (string.IsNullOrEmpty(paramsJson))
            {
                return null;
            }
            const string marker = "\"payload\":";
            var start = paramsJson.IndexOf(marker, System.StringComparison.Ordinal);
            if (start < 0)
            {
                return null;
            }
            start += marker.Length;
            var end = paramsJson.LastIndexOf('}');
            if (end <= start)
            {
                return null;
            }
            return paramsJson.Substring(start, end - start).Trim();
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
