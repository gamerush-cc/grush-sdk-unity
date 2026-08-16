using System;
using System.Threading.Tasks;

namespace GRushSdk
{
    public static class GRush
    {
        public const int RequiredProtocolVersion = 1;
        public const int MaxMessageBytes = 8 * 1024;

        private static IGRushBackend backend;
        private static GRushPlayerApi player;
        private static GRushNetApi net;
        private static GRushLeaderboardsApi leaderboards;

        public static IGRushBackend Backend
        {
            get
            {
                if (backend == null)
                {
                    backend = GRushBackendFactory.Create();
                }
                return backend;
            }
        }

        public static int ProtocolVersion
        {
            get { return Backend.ProtocolVersion; }
        }

        public static bool IsAvailable
        {
            get { return Backend.IsAvailable && Backend.ProtocolVersion >= RequiredProtocolVersion; }
        }

        public static GRushPlayerApi Player
        {
            get
            {
                if (player == null)
                {
                    player = new GRushPlayerApi();
                }
                return player;
            }
        }

        public static GRushNetApi Net
        {
            get
            {
                if (net == null)
                {
                    net = new GRushNetApi();
                }
                return net;
            }
        }

        public static GRushLeaderboardsApi Leaderboards
        {
            get
            {
                if (leaderboards == null)
                {
                    leaderboards = new GRushLeaderboardsApi();
                }
                return leaderboards;
            }
        }

        public static void UseBackend(IGRushBackend replacement)
        {
            net = null;
            player = null;
            leaderboards = null;
            backend = replacement ?? GRushBackendFactory.Create();
        }

        internal static Task<GRushResult<string>> CallAsync(string method, string paramsJson)
        {
            var completion = new TaskCompletionSource<GRushResult<string>>();
            if (!IsAvailable)
            {
                completion.SetResult(GRushResult<string>.Unsupported());
                return completion.Task;
            }
            Backend.Call(
                method,
                paramsJson,
                response =>
                    completion.TrySetResult(
                        response.Ok
                            ? GRushResult<string>.Success(response.ResultJson)
                            : GRushResult<string>.Failure(response.Code, response.Message)
                    )
            );
            return completion.Task;
        }
    }

    internal static class GRushBackendFactory
    {
        public static IGRushBackend Create()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (GRushWebGLBackend.IsPresent())
            {
                return new GRushWebGLBackend();
            }
            return new GRushUnsupportedBackend();
#else
            return new GRushMockBackend();
#endif
        }
    }

    public sealed class GRushPlayerApi
    {
        public Task<GRushResult<GRushPlayer>> GetSelfAsync()
        {
            return CallPlayerAsync("player.getSelf");
        }

        public Task<GRushResult<GRushPlayer>> RequestProfileAsync()
        {
            return CallPlayerAsync("player.requestProfile");
        }

        public Task<GRushResult<GRushPlayer>> RevokeProfileAsync()
        {
            return CallPlayerAsync("player.revokeProfile");
        }

        private static async Task<GRushResult<GRushPlayer>> CallPlayerAsync(string method)
        {
            var response = await GRush.CallAsync(method, null);
            if (!response.Ok)
            {
                return GRushResult<GRushPlayer>.Failure(response.Code, response.Message);
            }
            var wire = GRushWire.Parse<GRushPlayerWire>(response.Value);
            if (wire == null || string.IsNullOrEmpty(wire.pseudoId))
            {
                return GRushResult<GRushPlayer>.Failure(
                    GRushErrorCode.Internal,
                    "GameRush returned an unreadable player."
                );
            }
            return GRushResult<GRushPlayer>.Success(GRushWire.ToPlayer(wire));
        }
    }
}
