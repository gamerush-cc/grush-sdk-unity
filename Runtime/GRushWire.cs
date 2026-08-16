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
