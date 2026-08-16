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
