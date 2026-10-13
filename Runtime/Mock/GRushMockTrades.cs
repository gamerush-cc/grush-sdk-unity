using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GRushSdk
{
    /// <summary>
    /// エディタ用の交換所。GameRush と同じ規則（offer・want・give の形と大きさ・key・期限・
    /// operationId での冪等・受付中の出品は 10 件まで）で弾き、受諾の結果・pending・ack を同じ形で返す。
    /// はじめと <see cref="GRushMock.Reset"/> の後は、モックの相手（<see cref="GRushMock.Rivals"/>）の
    /// 受付中の出品を2件持つ。相手が自分の出品を受ける・相手が出品する・期限を切らすことは、
    /// <see cref="Accept(string, string, string)"/>・<see cref="Offer(string, string, string, GRushTradeOptions)"/>・
    /// <see cref="Expire"/> で起こす。
    /// </summary>
    public static class GRushMockTrades
    {
        private const int MaxOfferBytes = 2048;
        private const int MaxWantBytes = 1024;
        private const int MaxGiveBytes = 2048;
        private const int MaxIds = 50;
        private const int DefaultLimit = 20;
        private const int MaxLimit = 50;
        private const int MaxOpenTrades = 10;
        private const int PendingLimit = 100;
        private const int DefaultExpiresInSec = 3 * 24 * 60 * 60;
        private const int MinExpiresInSec = 10 * 60;
        private const int MaxExpiresInSec = 7 * 24 * 60 * 60;

        private static readonly Regex OperationIdPattern = new Regex("^[A-Za-z0-9._:-]{1,64}$");
        private static readonly Regex UuidPattern = new Regex(
            "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
            RegexOptions.IgnoreCase
        );

        private sealed class Trade
        {
            public string Id;
            public string From;
            public string To;
            public string Taker;
            public string OfferJson;
            public string WantJson;
            public string OfferKey;
            public string WantKey;
            public string GiveJson;
            public string Status;
            public DateTime CreatedAt;
            public DateTime ExpiresAt;
            public DateTime? SettledAt;
            public string OperationId;
            public readonly HashSet<string> AckedBy = new HashSet<string>();
        }

        private static readonly List<Trade> trades = new List<Trade>();

        static GRushMockTrades()
        {
            Seed();
        }

        /// <summary><paramref name="from"/> の公開の出品を作り、その ID を返す。形が縛りに合わなければ null。</summary>
        public static string Offer(string from, string offerJson, string wantJson)
        {
            return Offer(from, offerJson, wantJson, default(GRushTradeOptions));
        }

        /// <summary>
        /// <paramref name="from"/> の出品を作り、その ID を返す。<c>options.To</c> に相手の疑似 ID を
        /// 入れると指名の出品になる。形が縛りに合わなければ null。
        /// </summary>
        public static string Offer(string from, string offerJson, string wantJson, GRushTradeOptions options)
        {
            if (string.IsNullOrEmpty(from) || from == GRushMockBackend.PseudoId)
            {
                return null;
            }
            if (OptionsError(offerJson, wantJson, options.OfferKey, options.WantKey, options.ExpiresInSec) != null)
            {
                return null;
            }
            return Open(from, offerJson, wantJson, options, null).Id;
        }

        /// <summary><paramref name="from"/> から自分への指名の出品を作り、その ID を返す。</summary>
        public static string OfferToSelf(string from, string offerJson, string wantJson)
        {
            return Offer(from, offerJson, wantJson, new GRushTradeOptions { To = GRushMockBackend.PseudoId });
        }

        public static GRushTradeOutcome Accept(string tradeId, string giveJson)
        {
            return Accept(tradeId, giveJson, GRushMock.Rivals[0]);
        }

        /// <summary>自分の出品を <paramref name="by"/> が受ける。結果は受諾の Outcome と同じ。</summary>
        public static GRushTradeOutcome Accept(string tradeId, string giveJson, string by)
        {
            var self = GRushMockBackend.PseudoId;
            if (string.IsNullOrEmpty(by) || by == self)
            {
                return GRushTradeOutcome.NotFound;
            }
            if (GRushMockJson.PayloadError(giveJson, MaxGiveBytes, "Trade give") != null)
            {
                return GRushTradeOutcome.NotFound;
            }
            var trade = trades.Find(entry => entry.Id == tradeId);
            if (trade == null || trade.From != self)
            {
                return GRushTradeOutcome.NotFound;
            }
            Trade settled;
            GRushTradeOutcome outcome;
            GRushWire.TryTradeOutcome(Settle(tradeId, giveJson, by, out settled), out outcome);
            return outcome;
        }

        /// <summary>受付中の出品をいま期限切れにする。</summary>
        public static bool Expire(string tradeId)
        {
            var trade = trades.Find(entry => entry.Id == tradeId && entry.Status == "open");
            if (trade == null)
            {
                return false;
            }
            trade.ExpiresAt = DateTime.UtcNow;
            ExpireDue();
            return true;
        }

        internal static void Reset()
        {
            Seed();
        }

        private static void Seed()
        {
            trades.Clear();
            var rivals = GRushMock.Rivals;
            Open(
                rivals[0],
                "{\"item\":\"sword\",\"count\":1}",
                "{\"item\":\"gem\",\"count\":3}",
                new GRushTradeOptions { OfferKey = "sword", WantKey = "gem" },
                null
            );
            Open(
                rivals[1],
                "{\"item\":\"shield\",\"count\":1}",
                "{\"item\":\"coin\",\"count\":100}",
                new GRushTradeOptions { OfferKey = "shield", WantKey = "coin" },
                null
            );
        }

        private static Trade Open(
            string from,
            string offerJson,
            string wantJson,
            GRushTradeOptions options,
            string operationId
        )
        {
            var now = DateTime.UtcNow;
            var trade = new Trade
            {
                Id = Guid.NewGuid().ToString(),
                From = from,
                To = string.IsNullOrEmpty(options.To) ? null : options.To,
                OfferJson = offerJson,
                WantJson = wantJson,
                OfferKey = string.IsNullOrEmpty(options.OfferKey) ? null : options.OfferKey,
                WantKey = string.IsNullOrEmpty(options.WantKey) ? null : options.WantKey,
                Status = "open",
                CreatedAt = now,
                ExpiresAt = now.AddSeconds(options.ExpiresInSec > 0 ? options.ExpiresInSec : DefaultExpiresInSec),
                OperationId = operationId,
            };
            trades.Add(trade);
            return trade;
        }

        private static string OptionsError(
            string offerJson,
            string wantJson,
            string offerKey,
            string wantKey,
            int expiresInSec
        )
        {
            var error = GRushMockJson.PayloadError(offerJson, MaxOfferBytes, "Trade offer");
            if (error != null)
            {
                return error;
            }
            error = GRushMockJson.PayloadError(wantJson, MaxWantBytes, "Trade want");
            if (error != null)
            {
                return error;
            }
            if (offerKey != null && !GRushMockJson.IsToken(offerKey))
            {
                return "Invalid offerKey.";
            }
            if (wantKey != null && !GRushMockJson.IsToken(wantKey))
            {
                return "Invalid wantKey.";
            }
            if (expiresInSec != 0 && (expiresInSec < MinExpiresInSec || expiresInSec > MaxExpiresInSec))
            {
                return "Invalid expiresInSec.";
            }
            return null;
        }

        private static void ExpireDue()
        {
            var now = DateTime.UtcNow;
            foreach (var trade in trades)
            {
                if (trade.Status == "open" && trade.ExpiresAt <= now)
                {
                    trade.Status = "expired";
                    trade.SettledAt = trade.ExpiresAt;
                }
            }
        }

        private static GRushTradeWire Wire(Trade trade, string self)
        {
            var party = trade.From == self || trade.Taker == self;
            return new GRushTradeWire
            {
                id = trade.Id,
                from = trade.From,
                to = trade.To,
                taker = trade.Taker,
                offerJson = trade.OfferJson,
                wantJson = trade.WantJson,
                offerKey = trade.OfferKey,
                wantKey = trade.WantKey,
                giveJson = party ? trade.GiveJson : null,
                status = trade.Status,
                createdAt = GRushMockInbox.Timestamp(trade.CreatedAt),
                expiresAt = GRushMockInbox.Timestamp(trade.ExpiresAt),
                settledAt = trade.SettledAt.HasValue ? GRushMockInbox.Timestamp(trade.SettledAt.Value) : null,
            };
        }

        private static GRushRpcResponse Invalid(string message)
        {
            return GRushRpcResponse.Failure(GRushErrorCode.InvalidParams, message);
        }

        private static GRushRpcResponse TradeResponse(Trade trade, string self)
        {
            return GRushRpcResponse.Success(JsonUtility.ToJson(new GRushTradeEnvelope { trade = Wire(trade, self) }));
        }

        // 省略と null は既定値。文字列でない値は false。
        private static bool TryOptionalString(Dictionary<string, string> members, string name, out string value)
        {
            value = null;
            string raw;
            if (!members.TryGetValue(name, out raw) || GRushMockJson.IsNull(raw))
            {
                return true;
            }
            return GRushMockJson.TryString(raw, out value);
        }

        internal static bool TryLimit(Dictionary<string, string> members, out int limit)
        {
            limit = DefaultLimit;
            string raw;
            if (!members.TryGetValue("limit", out raw) || GRushMockJson.IsNull(raw))
            {
                return true;
            }
            return GRushMockJson.TryInt(raw, out limit) && limit >= 1 && limit <= MaxLimit;
        }

        // cursor は前のページの最後の ID。その次から読む。
        internal static bool TryCursor(Dictionary<string, string> members, List<string> ids, out int start)
        {
            start = 0;
            string cursor;
            if (!TryOptionalString(members, "cursor", out cursor))
            {
                return false;
            }
            if (cursor == null)
            {
                return true;
            }
            var at = ids.IndexOf(cursor);
            start = at + 1;
            return at >= 0;
        }

        internal static HashSet<string> IdsOf(string paramsJson)
        {
            var wire = GRushWire.Parse<GRushIdsWire>(paramsJson);
            if (wire == null || wire.ids == null || wire.ids.Length == 0 || wire.ids.Length > MaxIds)
            {
                return null;
            }
            var ids = new HashSet<string>();
            foreach (var id in wire.ids)
            {
                if (id == null || !UuidPattern.IsMatch(id))
                {
                    return null;
                }
                ids.Add(id.ToLowerInvariant());
            }
            return ids;
        }

        internal static GRushRpcResponse HandleCreate(string paramsJson, string self)
        {
            var members = GRushMockJson.Members(paramsJson);
            if (members == null)
            {
                return Invalid("Trade params must be a JSON object.");
            }
            string offer;
            string want;
            members.TryGetValue("offer", out offer);
            members.TryGetValue("want", out want);
            string offerKey;
            string wantKey;
            string to;
            string operationId;
            if (!TryOptionalString(members, "offerKey", out offerKey))
            {
                return Invalid("Invalid offerKey.");
            }
            if (!TryOptionalString(members, "wantKey", out wantKey))
            {
                return Invalid("Invalid wantKey.");
            }
            var expiresInSec = 0;
            string rawExpires;
            if (members.TryGetValue("expiresInSec", out rawExpires) && !GRushMockJson.IsNull(rawExpires))
            {
                if (!GRushMockJson.TryInt(rawExpires, out expiresInSec) || expiresInSec == 0)
                {
                    return Invalid("Invalid expiresInSec.");
                }
            }
            var error = OptionsError(offer, want, offerKey, wantKey, expiresInSec);
            if (error != null)
            {
                return Invalid(error);
            }
            if (
                !TryOptionalString(members, "operationId", out operationId)
                || operationId == null
                || !OperationIdPattern.IsMatch(operationId)
            )
            {
                return Invalid("Invalid operationId.");
            }
            if (!TryOptionalString(members, "to", out to) || to == string.Empty)
            {
                return Invalid("Invalid recipient.");
            }
            if (to != null && to == self)
            {
                return Invalid("Cannot offer a trade to yourself.");
            }
            if (to != null && !GRushMockPlayerStates.Exists(to))
            {
                return Invalid("No player with that id.");
            }
            // 同じ operationId の再送には、中身によらず先に作った出品を返す。
            var existing = trades.Find(trade => trade.From == self && trade.OperationId == operationId);
            if (existing != null)
            {
                return TradeResponse(existing, self);
            }
            ExpireDue();
            var open = trades.FindAll(trade => trade.From == self && trade.Status == "open").Count;
            if (open >= MaxOpenTrades)
            {
                // GameRush と同じく、待っても直らない上限なので RateLimited にしない。
                return GRushRpcResponse.Failure(
                    GRushErrorCode.InvalidParams,
                    "Too many open trades. Cancel an open trade first."
                );
            }
            var options = new GRushTradeOptions
            {
                OfferKey = offerKey,
                WantKey = wantKey,
                To = to,
                ExpiresInSec = expiresInSec,
            };
            return TradeResponse(Open(self, offer, want, options, operationId), self);
        }

        internal static GRushRpcResponse HandleList(string paramsJson, string self)
        {
            var members = GRushMockJson.Members(string.IsNullOrEmpty(paramsJson) ? "{}" : paramsJson);
            if (members == null)
            {
                return Invalid("Trade params must be a JSON object.");
            }
            int limit;
            if (!TryLimit(members, out limit))
            {
                return Invalid("Invalid limit.");
            }
            string offerKey;
            string wantKey;
            if (!TryOptionalString(members, "offerKey", out offerKey) || !TryOptionalString(members, "wantKey", out wantKey))
            {
                return Invalid("Invalid key.");
            }
            ExpireDue();
            var visible = new List<Trade>();
            for (var index = trades.Count - 1; index >= 0; index--)
            {
                var trade = trades[index];
                if (
                    trade.Status == "open"
                    && trade.From != self
                    && (trade.To == null || trade.To == self)
                    && !GRushMockInbox.Blocked.Contains(trade.From)
                    && (offerKey == null || trade.OfferKey == offerKey)
                    && (wantKey == null || trade.WantKey == wantKey)
                )
                {
                    visible.Add(trade);
                }
            }
            int start;
            if (!TryCursor(members, visible.ConvertAll(trade => trade.Id), out start))
            {
                return Invalid("Invalid cursor.");
            }
            var count = Math.Min(limit, Math.Max(0, visible.Count - start));
            var page = new GRushTradePageWire
            {
                trades = new GRushTradeWire[count],
                nextCursor = start + limit < visible.Count && count > 0 ? visible[start + count - 1].Id : null,
            };
            for (var index = 0; index < count; index++)
            {
                page.trades[index] = Wire(visible[start + index], self);
            }
            return GRushRpcResponse.Success(JsonUtility.ToJson(page));
        }

        private static GRushTradeWire PendingWire(Trade trade, string self)
        {
            if (trade.Status == "open" || trade.AckedBy.Contains(self))
            {
                return null;
            }
            if (trade.From == self)
            {
                var wire = Wire(trade, self);
                wire.role = "offerer";
                wire.action = trade.Status == "matched" ? "receiveGive" : "returnOffer";
                return wire;
            }
            if (trade.Taker == self)
            {
                var wire = Wire(trade, self);
                wire.role = "taker";
                wire.action = "receiveOffer";
                return wire;
            }
            return null;
        }

        internal static GRushRpcResponse HandleMine(string self)
        {
            ExpireDue();
            var open = new List<GRushTradeWire>();
            for (var index = trades.Count - 1; index >= 0; index--)
            {
                if (trades[index].From == self && trades[index].Status == "open")
                {
                    open.Add(Wire(trades[index], self));
                }
            }
            var finished = trades.FindAll(trade => PendingWire(trade, self) != null);
            finished.Sort((a, b) => a.SettledAt.GetValueOrDefault().CompareTo(b.SettledAt.GetValueOrDefault()));
            var pending = new List<GRushTradeWire>();
            foreach (var trade in finished)
            {
                if (pending.Count >= PendingLimit)
                {
                    break;
                }
                pending.Add(PendingWire(trade, self));
            }
            var mine = new GRushMyTradesWire { open = open.ToArray(), pending = pending.ToArray() };
            return GRushRpcResponse.Success(JsonUtility.ToJson(mine));
        }

        // 受諾の判定。見えない出品は notFound、終わった出品はその状態、受付中なら成立させる。
        private static string Settle(string tradeId, string giveJson, string taker, out Trade settled)
        {
            settled = null;
            ExpireDue();
            var trade = trades.Find(entry => entry.Id == tradeId);
            if (
                trade == null
                || trade.From == taker
                || (trade.To != null && trade.To != taker)
                || GRushMockInbox.Blocked.Contains(trade.From)
            )
            {
                return "notFound";
            }
            if (trade.Status == "matched")
            {
                if (trade.Taker != taker)
                {
                    return "taken";
                }
                settled = trade;
                return "matched";
            }
            if (trade.Status != "open")
            {
                return trade.Status;
            }
            trade.Taker = taker;
            trade.GiveJson = giveJson;
            trade.Status = "matched";
            trade.SettledAt = DateTime.UtcNow;
            settled = trade;
            return "matched";
        }

        internal static GRushRpcResponse HandleAccept(string paramsJson, string self)
        {
            var members = GRushMockJson.Members(paramsJson);
            string tradeId = null;
            string raw;
            if (members == null || !members.TryGetValue("tradeId", out raw) || !GRushMockJson.TryString(raw, out tradeId) || string.IsNullOrEmpty(tradeId))
            {
                return Invalid("Invalid tradeId.");
            }
            string give;
            members.TryGetValue("give", out give);
            var error = GRushMockJson.PayloadError(give, MaxGiveBytes, "Trade give");
            if (error != null)
            {
                return Invalid(error);
            }
            Trade settled;
            var outcome = Settle(tradeId, give, self, out settled);
            var wire = new GRushTradeAcceptWire { outcome = outcome };
            if (settled != null)
            {
                wire.trade = Wire(settled, self);
            }
            return GRushRpcResponse.Success(JsonUtility.ToJson(wire));
        }

        internal static GRushRpcResponse HandleCancel(string paramsJson, string self)
        {
            var members = GRushMockJson.Members(paramsJson);
            string tradeId = null;
            string raw;
            if (members != null && members.TryGetValue("tradeId", out raw))
            {
                GRushMockJson.TryString(raw, out tradeId);
            }
            ExpireDue();
            var trade = trades.Find(entry => entry.Id == tradeId && entry.From == self);
            if (trade == null)
            {
                return Invalid("No trade of yours for that id.");
            }
            if (trade.Status == "open")
            {
                trade.Status = "cancelled";
                trade.SettledAt = DateTime.UtcNow;
            }
            return TradeResponse(trade, self);
        }

        internal static GRushRpcResponse HandleAck(string paramsJson, string self)
        {
            var ids = IdsOf(paramsJson);
            if (ids == null)
            {
                return Invalid("ids must be 1 to 50 trade ids.");
            }
            ExpireDue();
            var acked = 0;
            foreach (var trade in trades)
            {
                if (ids.Contains(trade.Id.ToLowerInvariant()) && PendingWire(trade, self) != null)
                {
                    trade.AckedBy.Add(self);
                    acked++;
                }
            }
            return GRushRpcResponse.Success("{\"acked\":" + acked + "}");
        }

        internal static GRushRpcResponse HandleReport(string paramsJson, string self)
        {
            if (!GRushMock.SignedIn)
            {
                return GRushRpcResponse.Failure(GRushErrorCode.SignInRequired, "Reporting requires sign-in.");
            }
            var members = GRushMockJson.Members(paramsJson);
            string tradeId = null;
            string raw;
            if (members != null && members.TryGetValue("tradeId", out raw))
            {
                GRushMockJson.TryString(raw, out tradeId);
            }
            ExpireDue();
            var trade = trades.Find(entry => entry.Id == tradeId);
            var listed =
                trade != null
                && trade.Status == "open"
                && trade.From != self
                && (trade.To == null || trade.To == self);
            if (trade == null || !(listed || trade.Taker == self))
            {
                return Invalid("No trade for that id.");
            }
            if (!GRushMock.ConfirmTradeReport)
            {
                return GRushRpcResponse.Success("{\"reported\":false}");
            }
            GRushMockInbox.Blocked.Add(trade.From);
            return GRushRpcResponse.Success("{\"reported\":true}");
        }
    }
}
