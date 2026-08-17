using System;
using System.Collections;
using System.Net;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GRushSdk.Editor
{
    internal sealed class GRushEditorLogin
    {
        public const double TimeoutSeconds = 300;

        public string Status = "";
        public bool Finished;
        public string Error;
        public GRushCredentials Credentials;

        private readonly string origin;
        private readonly string[] scopes;
        private readonly string clientName;
        private readonly GRushEditorLoopback loopback = new GRushEditorLoopback();
        private volatile bool cancelled;

        public GRushEditorLogin(string origin, string[] scopes, string clientName)
        {
            this.origin = origin;
            this.scopes = scopes;
            this.clientName = clientName;
        }

        public void Cancel()
        {
            cancelled = true;
        }

        public IEnumerator Run()
        {
            try
            {
                loopback.Start(GRushEditorPkce.FreeLoopbackPort());
                var verifier = GRushEditorPkce.CreateVerifier();
                var state = GRushEditorPkce.CreateState();
                Status = "ブラウザで承認を待っています…";
                Application.OpenURL(AuthorizeUrl(GRushEditorPkce.Challenge(verifier), state));

                var deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
                string code = null;
                while (code == null)
                {
                    if (cancelled)
                    {
                        Fail("ログインを中止しました。");
                        yield break;
                    }
                    if (EditorApplication.timeSinceStartup > deadline)
                    {
                        Fail("承認を待つ時間が切れました。もう一度お試しください。");
                        yield break;
                    }
                    var context = loopback.Take();
                    if (context != null)
                    {
                        code = Handle(context, state);
                        if (Error != null)
                        {
                            yield break;
                        }
                        if (code == null)
                        {
                            loopback.Listen();
                        }
                    }
                    yield return null;
                }

                Status = "トークンを受け取っています…";
                yield return Exchange(code, verifier);
            }
            finally
            {
                loopback.Close();
                Finished = true;
            }
        }

        private string AuthorizeUrl(string challenge, string state)
        {
            var builder = new StringBuilder(origin);
            builder.Append("/studio/authorize?redirect_uri=");
            builder.Append(Uri.EscapeDataString(loopback.RedirectUri));
            builder.Append("&code_challenge=").Append(Uri.EscapeDataString(challenge));
            builder.Append("&code_challenge_method=S256");
            builder.Append("&client_name=").Append(Uri.EscapeDataString(clientName));
            builder.Append("&scopes=").Append(Uri.EscapeDataString(string.Join(",", scopes)));
            builder.Append("&state=").Append(Uri.EscapeDataString(state));
            return builder.ToString();
        }

        private string Handle(HttpListenerContext context, string expectedState)
        {
            var query = context.Request.QueryString;
            var code = query["code"];
            var error = query["error"];
            if (code == null && error == null)
            {
                GRushEditorLoopback.Respond(context, 404, "この待ち受けは GameRush の承認専用です。");
                return null;
            }
            if (query["state"] != expectedState)
            {
                GRushEditorLoopback.Respond(
                    context,
                    400,
                    "要求元が一致しません。Unity からやり直してください。"
                );
                Fail("承認の応答が Unity の出した要求と一致しませんでした。");
                return null;
            }
            if (error != null)
            {
                GRushEditorLoopback.Respond(context, 400, "承認されませんでした。Unity へ戻ってください。");
                Fail("承認されませんでした（" + error + "）。");
                return null;
            }
            GRushEditorLoopback.Respond(
                context,
                200,
                "GameRush への接続が完了しました。このタブを閉じて Unity へ戻ってください。"
            );
            return code;
        }

        private IEnumerator Exchange(string code, string verifier)
        {
            var body =
                "{\"code\":"
                + GRushJsonText.Escape(code)
                + ",\"codeVerifier\":"
                + GRushJsonText.Escape(verifier)
                + ",\"redirectUri\":"
                + GRushJsonText.Escape(loopback.RedirectUri)
                + "}";
            var slot = new GRushHttpSlot();
            yield return GRushEditorHttp.Send(
                GRushEditorHttp.Json(
                    origin + "/api/api-token-authorizations/exchange",
                    "POST",
                    body,
                    null
                ),
                slot
            );

            if (!slot.Result.Ok || slot.Result.Json == null)
            {
                Fail("トークンの受け取りに失敗しました: " + slot.Result.Message());
                yield break;
            }
            var token = slot.Result.Json.Get("token");
            var secret = slot.Result.Json.Get("secret").AsString(null);
            if (string.IsNullOrEmpty(secret))
            {
                Fail("トークンの応答を読めませんでした。");
                yield break;
            }

            Credentials = new GRushCredentials
            {
                Origin = origin,
                Token = secret,
                Preview = token.Get("preview").AsString(""),
                ClientName = token.Get("clientName").AsString(clientName),
                ExpiresAt = token.Get("expiresAt").AsString(""),
                Scopes = token.Get("scopes").AsStringArray() ?? new string[0],
                GameIds = token.Get("gameIds").AsStringArray(),
            };
            GRushEditorCredentials.Save(Credentials);
            Status = "ログインしました。";
        }

        private void Fail(string message)
        {
            Error = message;
            Status = message;
        }
    }
}
