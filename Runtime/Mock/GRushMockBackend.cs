using System;
using UnityEngine;

namespace GRushSdk
{
    public sealed class GRushMockBackend
        : IGRushBackend,
            IGRushShareBackend,
            IGRushLocaleBackend,
            IGRushLaunchBackend,
            IGRushAnalyticsBackend
    {
        private const string PseudoIdKey = "grush.mock.pseudoId";
        private const string ConsentKey = "grush.mock.profileConsent";

        private Action<GRushNetEvent> netHandler;
        private Action<string> localeHandler;
        private int localIndex = -1;

        public bool IsAvailable
        {
            get { return true; }
        }

        // モックが実装している一番新しい機能（起動パラメータ）の版数。
        public int ProtocolVersion
        {
            get { return GRush.LaunchProtocolVersion; }
        }

        public bool IsAnalyticsAvailable
        {
            get { return true; }
        }

        public bool LogAnalytics(GRushAnalyticsEvent analyticsEvent)
        {
            return GRushMockAnalytics.Record(analyticsEvent);
        }

        public void Call(string method, string paramsJson, Action<GRushRpcResponse> onDone)
        {
            var response = Handle(method, paramsJson);
            if (onDone != null)
            {
                GRushDispatcher.Post(() => onDone(response));
            }
        }

        public void Share(string paramsJson, byte[] image, Action<GRushRpcResponse> onDone)
        {
            Debug.Log(
                "[GRushMock] share " + paramsJson + " image=" + (image == null ? 0 : image.Length) + "B"
            );
            var response = GRushMock.ShareAvailable
                ? GRushRpcResponse.Success(
                    GRushMock.ShareStatus == GRushShareStatus.Opened
                        ? "{\"status\":\"opened\"}"
                        : "{\"status\":\"cancelled\"}"
                )
                : GRushRpcResponse.Failure(GRushErrorCode.Unavailable, "Sharing is unavailable here.");
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

        public string LaunchCurrentJson()
        {
            return LaunchJson();
        }

        // Web の層と同じく entries の配列で渡す。
        private static string LaunchJson()
        {
            var builder = new System.Text.StringBuilder("{\"entries\":[");
            var first = true;
            if (GRushMock.LaunchParams != null)
            {
                foreach (var pair in GRushMock.LaunchParams)
                {
                    if (pair.Key == null || pair.Value == null)
                    {
                        continue;
                    }
                    if (!first)
                    {
                        builder.Append(',');
                    }
                    first = false;
                    builder.Append("{\"key\":").Append(GRushWire.Escape(pair.Key));
                    builder.Append(",\"value\":").Append(GRushWire.Escape(pair.Value)).Append('}');
                }
            }
            return builder.Append("]}").ToString();
        }

        public string LocaleCurrentJson()
        {
            return GRushMockLocale.CurrentJson();
        }

        public void SetLocaleChangedHandler(Action<string> handler)
        {
            if (localeHandler != null)
            {
                GRushMockLocale.Changed -= OnLocaleChanged;
            }
            localeHandler = handler;
            if (handler != null)
            {
                GRushMockLocale.Changed += OnLocaleChanged;
            }
        }

        private void OnLocaleChanged(string json)
        {
            var handler = localeHandler;
            if (handler != null)
            {
                GRushDispatcher.Post(() => handler(json));
            }
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
                case "leaderboard.list":
                    return GRushRpcResponse.Success(GRushMockLeaderboards.ListJson());
                case "leaderboard.submit":
                    return SubmitScore(paramsJson);
                case "leaderboard.top":
                    return PageForQuery(paramsJson, false);
                case "leaderboard.aroundMe":
                    return PageForQuery(paramsJson, true);
                case "leaderboard.friends":
                    return GRushRpcResponse.Failure(
                        GRushErrorCode.ConsentDeclined,
                        "Friend leaderboards require per-game friend consent."
                    );
                case "playerState.getMine":
                    return GRushRpcResponse.Success(
                        mockPlayerState == null ? "{\"state\":null}" : mockPlayerState
                    );
                case "playerState.setMine":
                    return SetPlayerState(paramsJson);
                case "playerState.get":
                    return GRushRpcResponse.Success(GRushMockPlayerStates.GetJson(paramsJson));
                case "playerState.report":
                    return GRushMockPlayerStates.HandleReport(paramsJson);
                case "playerState.sample":
                    return Sample(paramsJson);
                case "inbox.send":
                    return GRushMockInbox.HandleSend(paramsJson, PseudoId);
                case "inbox.list":
                    return GRushMockInbox.HandleList(paramsJson);
                case "inbox.markRead":
                    return GRushMockInbox.HandleMarkRead(paramsJson);
                case "inbox.remove":
                    return GRushMockInbox.HandleRemove(paramsJson);
                case "inbox.report":
                    return GRushMockInbox.HandleReport(paramsJson);
                case "trade.create":
                    return GRushMockTrades.HandleCreate(paramsJson, PseudoId);
                case "trade.list":
                    return GRushMockTrades.HandleList(paramsJson, PseudoId);
                case "trade.mine":
                    return GRushMockTrades.HandleMine(PseudoId);
                case "trade.accept":
                    return GRushMockTrades.HandleAccept(paramsJson, PseudoId);
                case "trade.cancel":
                    return GRushMockTrades.HandleCancel(paramsJson, PseudoId);
                case "trade.ack":
                    return GRushMockTrades.HandleAck(paramsJson, PseudoId);
                case "trade.report":
                    return GRushMockTrades.HandleReport(paramsJson, PseudoId);
                case "locale.get":
                    return GRushRpcResponse.Success(GRushMockLocale.CurrentJson());
                case "launch.get":
                    return GRushRpcResponse.Success(LaunchJson());
                case "share.getAvailability":
                    return GRushRpcResponse.Success(
                        GRushMock.ShareAvailable ? "{\"available\":true}" : "{\"available\":false}"
                    );
                default:
                    return GRushRpcResponse.Failure(
                        GRushErrorCode.Unsupported,
                        "The mock backend does not implement " + method + "."
                    );
            }
        }

        private string mockPlayerState;
        private int mockPlayerStateRevision;
        private double? mockPlayerStateRating;

        private GRushRpcResponse SetPlayerState(string paramsJson)
        {
            var payload = GRushWire.ExtractPayloadJson(paramsJson);
            var error = GRushMockPlayerStatePayload.Validate(payload);
            if (error != null)
            {
                return GRushRpcResponse.Failure(GRushErrorCode.InvalidParams, error);
            }
            // rating の項目が無ければ今の値のまま、null なら消す。
            var rating = mockPlayerStateRating;
            var members = GRushMockJson.Members(paramsJson);
            string rawRating;
            if (members != null && members.TryGetValue("rating", out rawRating))
            {
                double value;
                if (GRushMockJson.IsNull(rawRating))
                {
                    rating = null;
                }
                else if (GRushMockJson.TryNumber(rawRating, out value) && GRushMockPlayerStates.IsRating(value))
                {
                    rating = value;
                }
                else
                {
                    return GRushRpcResponse.Failure(GRushErrorCode.InvalidParams, "Invalid rating.");
                }
            }
            mockPlayerStateRating = rating;
            mockPlayerStateRevision += 1;
            mockPlayerState =
                "{\"state\":{\"pseudoId\":"
                + GRushWire.Escape(PseudoId)
                + ",\"payload\":"
                + payload
                + ",\"revision\":"
                + mockPlayerStateRevision
                + ",\"rating\":"
                + (rating.HasValue ? GRushWire.Number(rating.Value) : "0")
                + ",\"hasRating\":"
                + (rating.HasValue ? "true" : "false")
                + ",\"updatedAt\":\"\"}}";
            return GRushRpcResponse.Success(mockPlayerState);
        }

        private static GRushRpcResponse Sample(string paramsJson)
        {
            string error;
            var json = GRushMockPlayerStates.SampleJson(paramsJson, PseudoId, out error);
            return error != null
                ? GRushRpcResponse.Failure(GRushErrorCode.InvalidParams, error)
                : GRushRpcResponse.Success(json);
        }

        private GRushRpcResponse SubmitScore(string paramsJson)
        {
            var request = GRushWire.Parse<GRushSubmitParamsWire>(paramsJson);
            if (request == null || string.IsNullOrEmpty(request.key))
            {
                return GRushRpcResponse.Failure(
                    GRushErrorCode.InvalidParams,
                    "A leaderboard key is required."
                );
            }
            return GRushMockLeaderboards.Submit(request.key, request.value, PseudoId);
        }

        private static GRushRpcResponse PageForQuery(string paramsJson, bool aroundMe)
        {
            var query = GRushWire.Parse<GRushLeaderboardQueryWire>(paramsJson);
            if (query == null || string.IsNullOrEmpty(query.key))
            {
                return GRushRpcResponse.Failure(
                    GRushErrorCode.InvalidParams,
                    "A leaderboard key is required."
                );
            }
            return GRushMockLeaderboards.Page(
                query.key,
                aroundMe ? query.range : query.limit,
                query.offset,
                aroundMe
            );
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
                Consented ? GRushMock.AvatarUrl : null,
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

        internal static string PseudoId
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
