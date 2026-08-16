using System;
using UnityEngine;

namespace GRushSdk
{
    public sealed class GRushMockBackend : IGRushBackend
    {
        private const string PseudoIdKey = "grush.mock.pseudoId";
        private const string ConsentKey = "grush.mock.profileConsent";

        private Action<GRushNetEvent> netHandler;
        private int localIndex = -1;

        public bool IsAvailable
        {
            get { return true; }
        }

        public int ProtocolVersion
        {
            get { return GRush.RequiredProtocolVersion; }
        }

        public void Call(string method, string paramsJson, Action<GRushRpcResponse> onDone)
        {
            var response = Handle(method, paramsJson);
            if (onDone != null)
            {
                GRushDispatcher.Post(() => onDone(response));
            }
        }

        public void Send(byte[] payload, int count, GRushChannel channel, int to)
        {
            if (localIndex < 0)
            {
                return;
            }
            GRushMockHub.Instance.Route(localIndex, payload, count, channel, to);
        }

        public void SetNetEventHandler(Action<GRushNetEvent> handler)
        {
            netHandler = handler;
        }

        private GRushRpcResponse Handle(string method, string paramsJson)
        {
            switch (method)
            {
                case "player.getSelf":
                    return GRushRpcResponse.Success(PlayerJson(Consented));
                case "player.requestProfile":
                    return RequestProfile();
                case "player.revokeProfile":
                    Consented = false;
                    return GRushRpcResponse.Success(PlayerJson(false));
                case "net.join":
                    return Join(paramsJson);
                case "net.leave":
                    GRushMockHub.Instance.Leave(localIndex);
                    localIndex = -1;
                    return GRushRpcResponse.Success("null");
                default:
                    return GRushRpcResponse.Failure(
                        GRushErrorCode.Unsupported,
                        "The mock backend does not implement " + method + "."
                    );
            }
        }

        private GRushRpcResponse RequestProfile()
        {
            if (!GRushMock.SignedIn)
            {
                return GRushRpcResponse.Failure(
                    GRushErrorCode.SignInRequired,
                    "The player is a guest and has no profile."
                );
            }
            if (!GRushMock.GrantProfileConsent)
            {
                return GRushRpcResponse.Failure(
                    GRushErrorCode.ConsentDeclined,
                    "The player declined to share their profile."
                );
            }
            Consented = true;
            return GRushRpcResponse.Success(PlayerJson(true));
        }

        private GRushRpcResponse Join(string paramsJson)
        {
            var request = GRushWire.Parse<GRushJoinParamsWire>(paramsJson);
            var wire = GRushMockHub.Instance.Join(
                request == null ? "default" : request.mode,
                request == null ? null : request.roomCode,
                PseudoId,
                Consented ? GRushMock.DisplayName : null,
                OnHubEvent
            );
            localIndex = wire.localPeerIndex;
            return GRushRpcResponse.Success(JsonUtility.ToJson(wire));
        }

        private void OnHubEvent(GRushNetEvent netEvent)
        {
            var handler = netHandler;
            if (handler != null)
            {
                handler(netEvent);
            }
        }

        private string PlayerJson(bool consented)
        {
            var wire = new GRushPlayerWire
            {
                pseudoId = PseudoId,
                isGuest = !GRushMock.SignedIn,
                profileConsent = consented && GRushMock.SignedIn,
                profile = new GRushProfileWire
                {
                    displayName = GRushMock.DisplayName,
                    avatarUrl = GRushMock.AvatarUrl,
                },
            };
            return JsonUtility.ToJson(wire);
        }

        private static bool Consented
        {
            get { return PlayerPrefs.GetInt(ConsentKey, 0) == 1; }
            set
            {
                PlayerPrefs.SetInt(ConsentKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private static string PseudoId
        {
            get
            {
                var stored = PlayerPrefs.GetString(PseudoIdKey, string.Empty);
                if (!string.IsNullOrEmpty(stored))
                {
                    return stored;
                }
                var created = Guid.NewGuid().ToString();
                PlayerPrefs.SetString(PseudoIdKey, created);
                PlayerPrefs.Save();
                return created;
            }
        }
    }
}
