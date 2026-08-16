using System;

namespace GRushSdk
{
    public enum GRushChannel
    {
        Reliable,
        Unreliable,
    }

    [Serializable]
    public struct GRushPlayer
    {
        public string PseudoId;
        public bool IsGuest;
        public bool ProfileConsent;
        public string DisplayName;
        public string AvatarUrl;
    }

    [Serializable]
    public struct GRushPeer
    {
        public int Index;
        public string PseudoId;
        public string DisplayName;
        public string AvatarUrl;
    }

    public struct GRushMessage
    {
        public int From;
        public GRushChannel Channel;
        public uint Seq;
        public byte[] Payload;
        public bool IsStale;
    }

    public struct GRushRoomStats
    {
        public string Transport;
    }

    [Serializable]
    public struct GRushLeaderboard
    {
        public string Key;
        public string Title;
        public string Sort;
        public string ValueType;
        public string Aggregation;
        public string Period;
        public double MinValue;
        public double MaxValue;
    }

    /// <summary>
    /// ランキングの1行。<see cref="DisplayName"/> と <see cref="AvatarUrl"/> は
    /// その相手がこのゲームでの公開に同意しているときだけ入る。ゲストは
    /// 常に匿名なので <see cref="IsGuest"/> を見て固定ラベルを出す。
    /// </summary>
    [Serializable]
    public struct GRushLeaderboardEntry
    {
        public int Rank;
        public string PseudoId;
        public string DisplayName;
        public string AvatarUrl;
        public bool IsGuest;
        public double Value;
        public string SubmittedAt;
        public bool IsSelf;
    }

    /// <summary>
    /// <see cref="Verified"/> は常に false。スコアはゲームの自己申告であり
    /// GameRush は値を検証していないので、表示側はそれを隠さないこと。
    /// </summary>
    [Serializable]
    public struct GRushLeaderboardPage
    {
        public string Key;
        public string Title;
        public string Sort;
        public string ValueType;
        public string Period;
        public string PeriodKey;
        public bool Verified;
        public GRushLeaderboardEntry[] Entries;
        public int Total;

        /// <summary>
        /// 応答の生 JSON。<c>metadata</c> は任意形状の JSON であり
        /// UnityEngine.JsonUtility では型に落とせない（手書きのパーサを
        /// SDK に持たせると壊れ方が読めない）。metadata が要るゲームは
        /// これを自前のパーサへ渡す。Godot 側は JSON を素で扱えるため
        /// <c>metadata</c> をそのまま返しており、非対称なのはこの理由。
        /// </summary>
        public string RawJson;
    }

    [Serializable]
    public struct GRushScoreResult
    {
        public bool Accepted;
        public bool Updated;
        public double Value;
        public int Rank;
        public bool HasRank;
        public bool Verified;
    }

    public static class GRushChannels
    {
        public const string Reliable = "reliable";
        public const string Unreliable = "unreliable";

        public static string ToWire(GRushChannel channel)
        {
            return channel == GRushChannel.Unreliable ? Unreliable : Reliable;
        }

        public static GRushChannel FromWire(string channel)
        {
            return channel == Unreliable ? GRushChannel.Unreliable : GRushChannel.Reliable;
        }

        public static int ToCode(GRushChannel channel)
        {
            return channel == GRushChannel.Unreliable ? 1 : 0;
        }

        public static GRushChannel FromCode(int code)
        {
            return code == 1 ? GRushChannel.Unreliable : GRushChannel.Reliable;
        }
    }
}
