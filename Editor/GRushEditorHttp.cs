using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;

namespace GRushSdk.Editor
{
    internal sealed class GRushHttpResult
    {
        public long Status;
        public string Body;
        public string Transport;
        public GRushJson Json;

        public bool Ok => Status >= 200 && Status < 300;

        public static GRushHttpResult From(UnityWebRequest request)
        {
            var body = request.downloadHandler == null ? null : request.downloadHandler.text;
            return new GRushHttpResult
            {
                Status = request.responseCode,
                Body = body,
                Transport = request.result == UnityWebRequest.Result.Success ? null : request.error,
                Json = GRushJson.Parse(body),
            };
        }

        /// <summary>
        /// 作者へ出す1行。API は文字列の <c>error</c> か、翻訳前提の
        /// <c>code</c> のどちらかを返すので、両方を拾って素通しする。
        /// </summary>
        public string Message()
        {
            if (Json != null)
            {
                var error = Json["error"];
                if (error != null)
                {
                    return error.AsString(null) ?? "";
                }
                var code = Json["code"];
                if (code != null)
                {
                    return code.AsString(null) ?? "";
                }
            }
            if (Status > 0)
            {
                return "HTTP " + Status;
            }
            return string.IsNullOrEmpty(Transport) ? "通信に失敗しました。" : Transport;
        }
    }

    internal static class GRushEditorHttp
    {
        public const int JsonTimeoutSeconds = 30;

        public static UnityWebRequest Json(string url, string method, string body, string bearer)
        {
            var request = new UnityWebRequest(url, method);
            request.downloadHandler = new DownloadHandlerBuffer();
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.SetRequestHeader("Accept", "application/json");
            if (!string.IsNullOrEmpty(bearer))
            {
                request.SetRequestHeader("Authorization", "Bearer " + bearer);
            }
            request.timeout = JsonTimeoutSeconds;
            return request;
        }

        /// <summary>
        /// ビルドのファイルは**ディスクから流す**。サーバの上限は総量 300MB で
        /// ファイル単位の上限が無いため、1本の大きな <c>.data</c> を丸ごと
        /// メモリへ読むとエディタごと落ちる。再送のたびに読み直しになるが、
        /// 同じ URL への PUT は上書きなので問題ない。
        /// </summary>
        public static UnityWebRequest PutFile(
            string url,
            string filePath,
            Dictionary<string, string> headers
        )
        {
            var request = new UnityWebRequest(url, "PUT");
            var upload = new UploadHandlerFile(filePath);
            upload.contentType = "application/octet-stream";
            request.uploadHandler = upload;
            request.disposeUploadHandlerOnDispose = true;
            request.downloadHandler = new DownloadHandlerBuffer();
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.SetRequestHeader(header.Key, header.Value);
                }
            }
            return request;
        }

        public static IEnumerator Send(UnityWebRequest request, GRushHttpSlot slot)
        {
            using (request)
            {
                yield return request.SendWebRequest();
                slot.Result = GRushHttpResult.From(request);
            }
        }
    }

    internal sealed class GRushHttpSlot
    {
        public GRushHttpResult Result;
    }
}
