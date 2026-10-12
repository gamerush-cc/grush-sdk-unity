using System.Collections.Generic;
using System.Threading.Tasks;

namespace GRushSdk
{
    /// <summary>
    /// 受信箱。同じゲームの疑似 ID 宛てに、小さい構造化メッセージ（非同期対戦の結果の通知など）を
    /// 送り、受け取る。送信も受信もゲストができる。
    ///
    /// **payload で文章や画像は送れない。** payload は JSON オブジェクトで、文字列の値とキーは
    /// 識別子（<c>^[A-Za-z0-9_.:-]{1,32}$</c>）だけ、ほかは数値・真偽値・null。入れ子は最上位を
    /// 1 段目として 4 段まで、値は合計 64 個まで、詰めた JSON で 1KB まで。合わなければ
    /// InvalidParams。利用者どうしの文章や画像のやりとりには使わないこと。
    ///
    /// <see cref="GRush.IsInboxAvailable"/> が false の古いランタイムでは、どの呼び出しも Unsupported。
    /// </summary>
    public sealed class GRushInboxApi
    {
        /// <summary>
        /// <paramref name="to"/>（同じゲームの疑似 ID）へ送る。<paramref name="kind"/> は
        /// <c>^[A-Za-z0-9._:-]{1,32}$</c>、<paramref name="payloadJson"/> は上の形の JSON。
        /// 送れたら true。
        /// </summary>
        public async Task<GRushResult<bool>> SendAsync(string to, string kind, string payloadJson)
        {
            if (!GRush.IsInboxAvailable)
            {
                return GRushResult<bool>.Unsupported();
            }
            var response = await GRush.CallAsync(
                "inbox.send",
                GRushWire.InboxSendParams(to, kind, payloadJson)
            );
            if (!response.Ok)
            {
                return GRushResult<bool>.Failure(response.Code, response.Message);
            }
            var wire = GRushWire.Parse<GRushSentWire>(response.Value);
            return GRushResult<bool>.Success(wire != null && wire.sent);
        }

        public Task<GRushResult<GRushInboxPage>> ListAsync()
        {
            return ListAsync(false, 0, null);
        }

        public Task<GRushResult<GRushInboxPage>> ListAsync(bool unreadOnly)
        {
            return ListAsync(unreadOnly, 0, null);
        }

        /// <summary>
        /// 受け取ったメッセージを新しい順に返す。<paramref name="limit"/> は 1〜50（0 なら 20）。
        /// 続きは前の結果の <c>NextCursor</c> を <paramref name="cursor"/> に渡して読む。
        /// </summary>
        public async Task<GRushResult<GRushInboxPage>> ListAsync(
            bool unreadOnly,
            int limit,
            string cursor
        )
        {
            if (!GRush.IsInboxAvailable)
            {
                return GRushResult<GRushInboxPage>.Unsupported();
            }
            var response = await GRush.CallAsync(
                "inbox.list",
                GRushWire.InboxListParams(unreadOnly, limit, cursor)
            );
            if (!response.Ok)
            {
                return GRushResult<GRushInboxPage>.Failure(response.Code, response.Message);
            }
            var wire = GRushWire.Parse<GRushInboxPageWire>(response.Value);
            if (wire == null || wire.messages == null)
            {
                return GRushResult<GRushInboxPage>.Failure(
                    GRushErrorCode.Internal,
                    "GameRush returned an unreadable inbox."
                );
            }
            var messages = new List<GRushInboxMessage>(wire.messages.Length);
            foreach (var message in wire.messages)
            {
                if (message != null && !string.IsNullOrEmpty(message.id))
                {
                    messages.Add(GRushWire.ToInboxMessage(message));
                }
            }
            return GRushResult<GRushInboxPage>.Success(
                new GRushInboxPage
                {
                    Messages = messages.ToArray(),
                    UnreadCount = wire.unreadCount,
                    NextCursor = string.IsNullOrEmpty(wire.nextCursor) ? null : wire.nextCursor,
                }
            );
        }

        /// <summary>既読にし、更新した件数を返す（1 回 50 件まで）。</summary>
        public async Task<GRushResult<int>> MarkReadAsync(string[] ids)
        {
            if (!GRush.IsInboxAvailable)
            {
                return GRushResult<int>.Unsupported();
            }
            var response = await GRush.CallAsync("inbox.markRead", GRushWire.IdsParams(ids));
            return ToUpdated(response);
        }

        /// <summary>消し、消した件数を返す（1 回 50 件まで）。</summary>
        public async Task<GRushResult<int>> RemoveAsync(string[] ids)
        {
            if (!GRush.IsInboxAvailable)
            {
                return GRushResult<int>.Unsupported();
            }
            var response = await GRush.CallAsync("inbox.remove", GRushWire.IdsParams(ids));
            return ToUpdated(response);
        }

        /// <summary>
        /// メッセージを通報する。**ゲームは通報 UI を描けない。** 確認は GameRush が出し、
        /// プレイヤーが承諾したら true。通報すると送り手をブロックし、その相手のメッセージと出品は
        /// 見えなくなる。ゲストは SignInRequired。
        /// </summary>
        public async Task<GRushResult<bool>> ReportAsync(string messageId)
        {
            if (!GRush.IsInboxAvailable)
            {
                return GRushResult<bool>.Unsupported();
            }
            var response = await GRush.CallAsync(
                "inbox.report",
                GRushWire.StringParams("messageId", messageId)
            );
            if (!response.Ok)
            {
                return GRushResult<bool>.Failure(response.Code, response.Message);
            }
            var wire = GRushWire.Parse<GRushReportResultWire>(response.Value);
            return GRushResult<bool>.Success(wire != null && wire.reported);
        }

        private static GRushResult<int> ToUpdated(GRushResult<string> response)
        {
            if (!response.Ok)
            {
                return GRushResult<int>.Failure(response.Code, response.Message);
            }
            var wire = GRushWire.Parse<GRushUpdatedWire>(response.Value);
            if (wire == null)
            {
                return GRushResult<int>.Failure(
                    GRushErrorCode.Internal,
                    "GameRush returned an unreadable count."
                );
            }
            return GRushResult<int>.Success(wire.updated);
        }
    }
}
