using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace GRushSdk
{
    /// <summary>
    /// エディタ用の受信箱。GameRush と同じ規則（payload の形・1KB・kind の形・自分宛ての禁止）で弾く。
    /// モックの相手（<see cref="GRushMock.Rivals"/>）と <see cref="GRushMockPlayerStates"/> で
    /// 置いた相手は、届いたメッセージを同じ kind と payload で送り返すので、送ってから一覧で受け取れる。
    /// 通報を承諾すると送り手をブロックし、その相手のメッセージと出品は見えなくなる。
    /// 中身は <see cref="GRushMock.Reset"/> で、はじめの1通だけに戻る。
    /// </summary>
    public static class GRushMockInbox
    {
        private const int MaxPayloadBytes = 1024;

        private sealed class Message
        {
            public string Id;
            public string From;
            public string Kind;
            public string PayloadJson;
            public DateTime CreatedAt;
            public string ReadAt;
            public bool Removed;
        }

        private static readonly List<Message> messages = new List<Message>();

        // 受信箱と交換所で共有するゲーム内のブロック。
        internal static readonly HashSet<string> Blocked = new HashSet<string>();

        static GRushMockInbox()
        {
            Seed();
        }

        /// <summary>
        /// <paramref name="from"/> から自分の受信箱へメッセージを届ける。形が受信箱の縛りに合わないか、
        /// 送り手をブロックしていれば false。
        /// </summary>
        public static bool Receive(string from, string kind, string payloadJson)
        {
            if (string.IsNullOrEmpty(from) || from == GRushMockBackend.PseudoId)
            {
                return false;
            }
            if (!GRushMockJson.IsToken(kind) || Blocked.Contains(from))
            {
                return false;
            }
            if (GRushMockJson.PayloadError(payloadJson, MaxPayloadBytes, "Inbox payload") != null)
            {
                return false;
            }
            Deliver(from, kind, payloadJson);
            return true;
        }

        internal static void Reset()
        {
            Seed();
        }

        internal static string Timestamp(DateTime at)
        {
            return at.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        }

        private static void Seed()
        {
            messages.Clear();
            Blocked.Clear();
            Deliver(GRushMock.Rivals[0], "challenge", "{\"stage\":3,\"score\":1200}");
        }

        private static void Deliver(string from, string kind, string payloadJson)
        {
            if (Blocked.Contains(from))
            {
                return;
            }
            messages.Add(
                new Message
                {
                    Id = Guid.NewGuid().ToString(),
                    From = from,
                    Kind = kind,
                    PayloadJson = payloadJson,
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        // 新しい順。消したものと、ブロックした送り手のものを除く。
        private static List<Message> Visible()
        {
            var visible = new List<Message>();
            for (var index = messages.Count - 1; index >= 0; index--)
            {
                var message = messages[index];
                if (!message.Removed && !Blocked.Contains(message.From))
                {
                    visible.Add(message);
                }
            }
            return visible;
        }

        private static GRushRpcResponse Invalid(string message)
        {
            return GRushRpcResponse.Failure(GRushErrorCode.InvalidParams, message);
        }

        internal static GRushRpcResponse HandleSend(string paramsJson, string self)
        {
            var members = GRushMockJson.Members(paramsJson);
            string to;
            string kind;
            string payload;
            if (
                members == null
                || !members.TryGetValue("to", out var rawTo)
                || !GRushMockJson.TryString(rawTo, out to)
                || string.IsNullOrEmpty(to)
            )
            {
                return Invalid("Invalid recipient.");
            }
            if (to == self)
            {
                return Invalid("Cannot send a message to yourself.");
            }
            if (!GRushMockPlayerStates.Exists(to))
            {
                return Invalid("No player with that id.");
            }
            if (
                !members.TryGetValue("kind", out var rawKind)
                || !GRushMockJson.TryString(rawKind, out kind)
                || !GRushMockJson.IsToken(kind)
            )
            {
                return Invalid("Invalid kind.");
            }
            members.TryGetValue("payload", out payload);
            var error = GRushMockJson.PayloadError(payload, MaxPayloadBytes, "Inbox payload");
            if (error != null)
            {
                return Invalid(error);
            }
            // モックの相手は、届いたメッセージを同じ kind と payload で送り返す。
            Deliver(to, kind, payload);
            return GRushRpcResponse.Success("{\"sent\":true}");
        }

        internal static GRushRpcResponse HandleList(string paramsJson)
        {
            var members = GRushMockJson.Members(string.IsNullOrEmpty(paramsJson) ? "{}" : paramsJson);
            if (members == null)
            {
                return Invalid("Inbox params must be a JSON object.");
            }
            int limit;
            if (!GRushMockTrades.TryLimit(members, out limit))
            {
                return Invalid("Invalid limit.");
            }
            var unreadOnly = members.TryGetValue("unreadOnly", out var rawUnread) && rawUnread == "true";
            var visible = Visible();
            var unreadCount = 0;
            var filtered = new List<Message>();
            foreach (var message in visible)
            {
                if (message.ReadAt == null)
                {
                    unreadCount++;
                }
                if (!unreadOnly || message.ReadAt == null)
                {
                    filtered.Add(message);
                }
            }
            int start;
            if (!GRushMockTrades.TryCursor(members, filtered.ConvertAll(message => message.Id), out start))
            {
                return Invalid("Invalid cursor.");
            }
            var count = Math.Min(limit, Math.Max(0, filtered.Count - start));
            var page = new GRushInboxPageWire
            {
                messages = new GRushInboxMessageWire[count],
                unreadCount = unreadCount,
                nextCursor = start + limit < filtered.Count && count > 0 ? filtered[start + count - 1].Id : null,
            };
            for (var index = 0; index < count; index++)
            {
                var message = filtered[start + index];
                page.messages[index] = new GRushInboxMessageWire
                {
                    id = message.Id,
                    from = message.From,
                    kind = message.Kind,
                    payloadJson = message.PayloadJson,
                    createdAt = Timestamp(message.CreatedAt),
                    readAt = message.ReadAt,
                };
            }
            return GRushRpcResponse.Success(JsonUtility.ToJson(page));
        }

        internal static GRushRpcResponse HandleMarkRead(string paramsJson)
        {
            return Update(paramsJson, message =>
            {
                if (message.ReadAt != null)
                {
                    return false;
                }
                message.ReadAt = Timestamp(DateTime.UtcNow);
                return true;
            });
        }

        internal static GRushRpcResponse HandleRemove(string paramsJson)
        {
            return Update(paramsJson, message =>
            {
                message.Removed = true;
                return true;
            });
        }

        private static GRushRpcResponse Update(string paramsJson, Func<Message, bool> apply)
        {
            var ids = GRushMockTrades.IdsOf(paramsJson);
            if (ids == null)
            {
                return Invalid("ids must be 1 to 50 message ids.");
            }
            var updated = 0;
            foreach (var message in Visible())
            {
                if (ids.Contains(message.Id.ToLowerInvariant()) && apply(message))
                {
                    updated++;
                }
            }
            return GRushRpcResponse.Success("{\"updated\":" + updated + "}");
        }

        internal static GRushRpcResponse HandleReport(string paramsJson)
        {
            if (!GRushMock.SignedIn)
            {
                return GRushRpcResponse.Failure(GRushErrorCode.SignInRequired, "Reporting requires sign-in.");
            }
            var members = GRushMockJson.Members(paramsJson);
            string messageId = null;
            if (members != null && members.TryGetValue("messageId", out var raw))
            {
                GRushMockJson.TryString(raw, out messageId);
            }
            var target = Visible().Find(message => message.Id == messageId);
            if (target == null)
            {
                return Invalid("No message for that id.");
            }
            if (!GRushMock.ConfirmInboxReport)
            {
                return GRushRpcResponse.Success("{\"reported\":false}");
            }
            Blocked.Add(target.From);
            return GRushRpcResponse.Success("{\"reported\":true}");
        }
    }
}
