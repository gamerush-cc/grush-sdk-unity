using System.Threading.Tasks;

namespace GRushSdk
{
    public sealed class GRushNetApi
    {
        private GRushRoom room;
        private bool handlerBound;

        public GRushRoom Room
        {
            get { return room; }
        }

        public Task<GRushResult<GRushRoom>> JoinAsync()
        {
            return JoinAsync("default", null);
        }

        public Task<GRushResult<GRushRoom>> JoinAsync(string mode)
        {
            return JoinAsync(mode, null);
        }

        public async Task<GRushResult<GRushRoom>> JoinAsync(string mode, string roomCode)
        {
            if (!GRush.IsAvailable)
            {
                return GRushResult<GRushRoom>.Unsupported();
            }
            BindHandler();
            var previous = room;
            room = null;
            if (previous != null)
            {
                previous.CloseLocally("replaced");
            }
            var response = await GRush.CallAsync("net.join", GRushWire.JoinParams(mode, roomCode));
            if (!response.Ok)
            {
                return GRushResult<GRushRoom>.Failure(response.Code, response.Message);
            }
            var wire = GRushWire.Parse<GRushJoinWire>(response.Value);
            if (wire == null || string.IsNullOrEmpty(wire.roomId))
            {
                return GRushResult<GRushRoom>.Failure(
                    GRushErrorCode.Internal,
                    "GameRush returned an unreadable room."
                );
            }
            room = new GRushRoom(this, wire);
            return GRushResult<GRushRoom>.Success(room);
        }

        internal void Forget(GRushRoom closed)
        {
            if (room == closed)
            {
                room = null;
            }
        }

        private void BindHandler()
        {
            if (handlerBound)
            {
                return;
            }
            handlerBound = true;
            GRush.Backend.SetNetEventHandler(OnNetEvent);
        }

        private void OnNetEvent(GRushNetEvent netEvent)
        {
            var current = room;
            if (current != null)
            {
                current.Apply(netEvent);
            }
        }
    }
}
