using System.Collections;
using System.Collections.Generic;
using UnityEditor;

namespace GRushSdk.Editor
{
    internal static class GRushEditorUploader
    {
        // ビルドの presigned URL は一度きりの書き込みなので、
        // 412 は前の試行が届いていた印として成功に扱う。
        private const long ObjectAlreadyWritten = 412;
        private const long SignatureRejected = 403;

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
                    FailCancelled(state);
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
            for (var attempt = 1; attempt <= GRushEditorPutPolicy.Attempts; attempt++)
            {
                if (state.Cancelled)
                {
                    FailCancelled(state);
                    yield break;
                }
                using (
                    var request = GRushEditorHttp.PutFile(
                        ticket.Url,
                        ticket.File.FullPath,
                        ticket.File.Size,
                        ticket.Headers
                    )
                )
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
                        FailCancelled(state);
                        yield break;
                    }
                    if (result.Status == SignatureRejected)
                    {
                        state.NeedsComplete = false;
                        Fail(
                            state,
                            ticket.Path
                                + ": アップロード用の URL が期限切れです（有効期限1時間）。"
                                + "『アップロードする』をもう一度押すと新しいビルドで送り直します。"
                                + "途中のビルドは24時間後に自動で片付きます。"
                        );
                        yield break;
                    }
                    if (attempt == GRushEditorPutPolicy.Attempts)
                    {
                        Fail(state, ticket.Path + ": 送信に失敗しました: " + result.Message());
                        yield break;
                    }
                }
                var until =
                    EditorApplication.timeSinceStartup
                    + GRushEditorPutPolicy.RetryDelaySeconds(attempt);
                while (!state.Cancelled && EditorApplication.timeSinceStartup < until)
                {
                    yield return null;
                }
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

        private static void FailCancelled(GRushUploadState state)
        {
            Fail(state, "アップロードを中止しました。残り " + state.Pending.Count + " ファイルです。");
        }

        private static void Fail(GRushUploadState state, string message)
        {
            state.Error = message;
            state.Phase = message;
            state.Running = false;
        }
    }
}
