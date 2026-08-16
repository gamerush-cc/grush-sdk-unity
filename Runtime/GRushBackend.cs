using System;

namespace GRushSdk
{
    public struct GRushRpcResponse
    {
        public bool Ok;
        public string ResultJson;
        public GRushErrorCode Code;
        public string Message;

        public static GRushRpcResponse Success(string resultJson)
        {
            return new GRushRpcResponse { Ok = true, ResultJson = resultJson };
        }

        public static GRushRpcResponse Failure(GRushErrorCode code, string message)
        {
            return new GRushRpcResponse { Ok = false, Code = code, Message = message };
        }
    }

    public enum GRushNetEventKind
    {
        Message = 1,
        PeerJoin = 2,
        PeerLeave = 3,
        Host = 4,
        TransportChange = 5,
        Close = 6,
    }

    public struct GRushNetEvent
    {
        public GRushNetEventKind Kind;
        public int From;
        public GRushChannel Channel;
        public uint Seq;
        public byte[] Payload;
        public string Detail;
    }

    public interface IGRushBackend
    {
        bool IsAvailable { get; }
        int ProtocolVersion { get; }
        void Call(string method, string paramsJson, Action<GRushRpcResponse> onDone);
        void Send(byte[] payload, int count, GRushChannel channel, int to);
        void SetNetEventHandler(Action<GRushNetEvent> handler);
    }

    public sealed class GRushUnsupportedBackend : IGRushBackend
    {
        public bool IsAvailable
        {
            get { return false; }
        }

        public int ProtocolVersion
        {
            get { return 0; }
        }

        public void Call(string method, string paramsJson, Action<GRushRpcResponse> onDone)
        {
            if (onDone != null)
            {
                onDone(
                    GRushRpcResponse.Failure(
                        GRushErrorCode.Unsupported,
                        "GameRush GameAPI is not available here."
                    )
                );
            }
        }

        public void Send(byte[] payload, int count, GRushChannel channel, int to) { }

        public void SetNetEventHandler(Action<GRushNetEvent> handler) { }
    }
}
