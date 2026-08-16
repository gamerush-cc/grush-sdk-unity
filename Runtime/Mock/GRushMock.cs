using System;

namespace GRushSdk
{
    public static class GRushMock
    {
        public static bool SignedIn = true;
        public static string DisplayName = "Editor Player";
        public static string AvatarUrl = null;
        public static bool GrantProfileConsent = true;
        public static double UnreliableDropRate = 0.0;

        public static GRushMockPeer AddPeer(string displayName)
        {
            return AddPeer(displayName, null);
        }

        public static GRushMockPeer AddPeer(string displayName, string avatarUrl)
        {
            return GRushMockHub.Instance.AddPeer(displayName, avatarUrl);
        }

        public static void RemovePeer(GRushMockPeer peer)
        {
            GRushMockHub.Instance.RemovePeer(peer);
        }

        public static void Reset()
        {
            GRushMockHub.Reset();
        }
    }

    public sealed class GRushMockPeer
    {
        public int Index { get; internal set; }
        public string PseudoId { get; internal set; }
        public string DisplayName { get; internal set; }
        public string AvatarUrl { get; internal set; }

        public event Action<GRushMessage> Received;

        public GRushResult<bool> Send(byte[] payload)
        {
            return Send(payload, GRushChannel.Reliable, GRushRoom.Everyone);
        }

        public GRushResult<bool> Send(byte[] payload, GRushChannel channel, int to)
        {
            if (payload == null || payload.Length > GRush.MaxMessageBytes)
            {
                return GRushResult<bool>.Failure(GRushErrorCode.InvalidParams, "Payload is invalid.");
            }
            GRushMockHub.Instance.Route(Index, payload, payload.Length, channel, to);
            return GRushResult<bool>.Success(true);
        }

        internal void Deliver(GRushMessage message)
        {
            var handler = Received;
            if (handler != null)
            {
                handler(message);
            }
        }
    }
}
