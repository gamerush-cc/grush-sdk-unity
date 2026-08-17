using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace GRushSdk.Editor
{
    internal static class GRushEditorUploader
    {
        private const int PutAttempts = 3;
        private const long ObjectAlreadyWritten = 412;
        private const long SignatureRejected = 403;

        /// <summary>
        /// **<c>POST /api/games/:id/builds</c> は機械的に再試行しない。**
        /// サーバに冪等キーが無く、再試行はもう1つのビルドを作る。失敗したら
        /// 理由を出して止め、作者に判断させる。個別ファイルの PUT は同じ URL
        /// への上書きなので再試行してよい。
        /// </summary>
        public static IEnumerator Run(
            GRushEditorClient client,
            string gameId,
            GRushBuildManifest manifest,
            GRushUploadState state
        )
        {
            state.Reset();
            state.Running = true;
            state.TotalBytes = manifest.TotalBytes;
            state.Phase = "ビルドを作成しています…";

            var slot = new GRushHttpSlot();
            yield return client.Post("/api/games/" + gameId + "/builds", manifest.ToRequestBody(), slot);
            if (!slot.Result.Ok || slot.Result.Json == null)
            {
                Fail(
                    state,
                    "ビルドの作成に失敗しました: "
                        + slot.Result.Message()
                        + "（自動では再試行しません。原因を直してからやり直してください）"
                );
                yield break;
            }

            state.BuildId = slot.Result.Json.Get("build").Get("id").AsString(null);
            state.Issued = GRushEditorUploadTickets.From(
                slot.Result.Json.Get("uploadUrls"),
                manifest
            );
            state.Pending = new List<GRushUploadTicket>(state.Issued);
            state.NeedsComplete = true;
            if (string.IsNullOrEmpty(state.BuildId) || state.Pending.Count != manifest.Files.Count)
            {
                Fail(state, "アップロード先の応答を読めませんでした。Studio でビルドの状態を確認してください。");
                yield break;
            }
            yield return Resume(client, state);
        }

        public static IEnumerator Resume(GRushEditorClient client, GRushUploadState state)
        {
            state.Running = true;
            state.Cancelled = false;
            state.Error = null;
            state.Phase = "ファイルを送っています…";

            while (state.Pending.Count > 0)
            {
                if (state.Cancelled)
                {
                    Fail(state, "アップロードを中止しました。残り " + state.Pending.Count + " ファイルです。");
                    yield break;
                }
                var ticket = state.Pending[0];
                state.CurrentPath = ticket.Path;
                state.FileProgress = 0f;
                var outcome = new GRushHttpSlot();
                yield return PutFile(ticket, state, outcome);
                if (outcome.Result == null)
                {
                    yield break;
                }
                state.Pending.RemoveAt(0);
                state.UploadedBytes += ticket.File.Size;
            }

            state.CurrentPath = "";
            yield return Complete(client, state);
        }

        private static IEnumerator PutFile(
            GRushUploadTicket ticket,
            GRushUploadState state,
            GRushHttpSlot outcome
        )
        {
            var body = File.ReadAllBytes(ticket.File.FullPath);
            for (var attempt = 1; attempt <= PutAttempts; attempt++)
            {
                using (var request = GRushEditorHttp.PutBytes(ticket.Url, body, ticket.Headers))
                {
                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        if (state.Cancelled)
                        {
                            request.Abort();
                        }
                        state.FileProgress = request.uploadProgress;
                        yield return null;
                    }
                    var result = GRushHttpResult.From(request);
                    if (result.Ok || result.Status == ObjectAlreadyWritten)
                    {
                        outcome.Result = result;
                        yield break;
                    }
                    if (state.Cancelled)
                    {
                        Fail(state, "アップロードを中止しました。残り " + state.Pending.Count + " ファイルです。");
                        yield break;
                    }
                    if (result.Status == SignatureRejected)
                    {
                        Fail(
                            state,
                            ticket.Path
                                + ": アップロード用の URL が期限切れです（有効期限1時間）。ビルドを作り直してください。"
                        );
                        yield break;
                    }
                    if (attempt == PutAttempts)
                    {
                        Fail(state, ticket.Path + ": 送信に失敗しました: " + result.Message());
                        yield break;
                    }
                }
                yield return null;
            }
        }

        private static IEnumerator Complete(GRushEditorClient client, GRushUploadState state)
        {
            state.Phase = "ビルドを確定しています…";
            var slot = new GRushHttpSlot();
            yield return client.Post("/api/builds/" + state.BuildId + "/complete", "{}", slot);
            if (slot.Result.Ok)
            {
                state.EntryUrl = slot.Result.Json == null
                    ? null
                    : slot.Result.Json.Get("entryUrl").AsString(null);
                state.NeedsComplete = false;
                state.Running = false;
                state.Done = true;
                state.Phase = "アップロードが終わりました。";
                yield break;
            }
            var missing = GRushEditorUploadTickets.Requeue(state, slot.Result);
            Fail(
                state,
                missing > 0
                    ? "R2 に届いていないファイルが " + missing + " 件ありました。再送してください。"
                    : "ビルドの確定に失敗しました: " + slot.Result.Message()
            );
        }

        private static void Fail(GRushUploadState state, string message)
        {
            state.Error = message;
            state.Phase = message;
            state.Running = false;
        }
    }
}
