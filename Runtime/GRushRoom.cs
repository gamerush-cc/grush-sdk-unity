using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GRushSdk
{
    public sealed class GRushRoom
    {
        public const int Everyone = -1;

        private readonly List<GRushPeer> peers = new List<GRushPeer>();
        private readonly Dictionary<int, uint> lastSeq = new Dictionary<int, uint>();
        private readonly double serverOffsetMs;
        private readonly GRushNetApi owner;
        private string transport;
        private int hostPeerId;

        public string RoomId { get; private set; }
        public string RoomCode { get; private set; }
        public int RoomEpoch { get; private set; }
        public int LocalPeerId { get; private set; }
        public bool IsClosed { get; private set; }

        public int HostPeerId
        {
            get { return hostPeerId; }
        }

        public IReadOnlyList<GRushPeer> Peers
        {
            get { return peers; }
        }

        public GRushRoomStats Stats
        {
            get { return new GRushRoomStats { Transport = transport }; }
        }

        public bool IsHost
        {
            get { return hostPeerId == LocalPeerId; }
        }

        public event Action<GRushMessage> Message;
        public event Action<GRushPeer> PeerJoined;
        public event Action<int> PeerLeft;
        public event Action<int> HostChanged;
        public event Action<string> TransportChanged;
        public event Action<string> Closed;

        internal GRushRoom(GRushNetApi owner, GRushJoinWire wire)
        {
            this.owner = owner;
            RoomId = wire.roomId;
            RoomCode = wire.roomCode;
            RoomEpoch = wire.epoch;
            LocalPeerId = wire.localPeerIndex;
            hostPeerId = wire.hostIndex;
            transport = wire.transport;
            serverOffsetMs = wire.serverTimeMs - LocalNowMs();
            if (wire.peers != null)
            {
                foreach (var peer in wire.peers)
                {
                    peers.Add(GRushWire.ToPeer(peer));
                }
            }
        }

        public long ServerTimeMs()
        {
            return (long)(LocalNowMs() + serverOffsetMs);
        }

        public uint LastSeqFrom(int peerId)
        {
            uint seq;
            return lastSeq.TryGetValue(peerId, out seq) ? seq : 0;
        }

        public GRushResult<bool> Send(byte[] payload)
        {
            return Send(payload, GRushChannel.Reliable, Everyone);
        }

        public GRushResult<bool> Send(byte[] payload, GRushChannel channel)
        {
            return Send(payload, channel, Everyone);
        }

        public GRushResult<bool> Send(byte[] payload, GRushChannel channel, int to)
        {
            return Send(payload, payload == null ? 0 : payload.Length, channel, to);
        }

        public GRushResult<bool> Send(byte[] payload, int count, GRushChannel channel, int to)
        {
            if (IsClosed)
            {
                return GRushResult<bool>.Failure(
                    GRushErrorCode.Unavailable,
                    "The room is already closed."
                );
            }
            if (payload == null || count < 0 || count > payload.Length)
            {
                return GRushResult<bool>.Failure(GRushErrorCode.InvalidParams, "Payload is invalid.");
            }
            if (count > GRush.MaxMessageBytes)
            {
                return GRushResult<bool>.Failure(
                    GRushErrorCode.InvalidParams,
                    "Payload exceeds the 8KB message limit."
                );
            }
            GRush.Backend.Send(payload, count, channel, to);
            return GRushResult<bool>.Success(true);
        }

        public async Task<GRushResult<bool>> LeaveAsync()
        {
            if (IsClosed)
            {
                return GRushResult<bool>.Success(true);
            }
            var response = await GRush.CallAsync("net.leave", null);
            MarkClosed("left");
            return response.Ok
                ? GRushResult<bool>.Success(true)
                : GRushResult<bool>.Failure(response.Code, response.Message);
        }

        internal void CloseLocally(string reason)
        {
            MarkClosed(reason);
        }

        internal void Apply(GRushNetEvent netEvent)
        {
            switch (netEvent.Kind)
            {
                case GRushNetEventKind.Message:
                    RaiseMessage(netEvent);
                    return;
                case GRushNetEventKind.PeerJoin:
                    ApplyPeerJoin(netEvent.Detail);
                    return;
                case GRushNetEventKind.PeerLeave:
                    peers.RemoveAll(peer => peer.Index == netEvent.From);
                    Raise(PeerLeft, netEvent.From);
                    return;
                case GRushNetEventKind.Host:
                    hostPeerId = netEvent.From;
                    Raise(HostChanged, netEvent.From);
                    return;
                case GRushNetEventKind.TransportChange:
                    transport = netEvent.Detail;
                    Raise(TransportChanged, netEvent.Detail);
                    return;
                case GRushNetEventKind.Close:
                    MarkClosed(netEvent.Detail);
                    return;
            }
        }

        private void RaiseMessage(GRushNetEvent netEvent)
        {
            var previous = LastSeqFrom(netEvent.From);
            lastSeq[netEvent.From] = netEvent.Seq;
            Raise(
                Message,
                new GRushMessage
                {
                    From = netEvent.From,
                    Channel = netEvent.Channel,
                    Seq = netEvent.Seq,
                    Payload = netEvent.Payload,
                    IsStale = netEvent.Seq != 0 && netEvent.Seq <= previous,
                }
            );
        }

        private void ApplyPeerJoin(string json)
        {
            var wire = GRushWire.Parse<GRushPeerWire>(json);
            if (wire == null)
            {
                return;
            }
            var peer = GRushWire.ToPeer(wire);
            peers.RemoveAll(existing => existing.Index == peer.Index);
            peers.Add(peer);
            Raise(PeerJoined, peer);
        }

        private void MarkClosed(string reason)
        {
            if (IsClosed)
            {
                return;
            }
            IsClosed = true;
            peers.Clear();
            owner.Forget(this);
            Raise(Closed, reason);
        }

        private static void Raise<T>(Action<T> handler, T payload)
        {
            if (handler != null)
            {
                handler(payload);
            }
        }

        private static double LocalNowMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
