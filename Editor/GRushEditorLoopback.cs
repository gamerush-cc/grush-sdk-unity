using System;
using System.Net;
using System.Text;

namespace GRushSdk.Editor
{
    /// <summary>
    /// 承認後のリダイレクトを受ける手元の待ち受け。<c>redirect_uri</c> は
    /// クエリもフラグメントも付けられないので、末尾スラッシュ無しの
    /// <c>http://&lt;host&gt;:&lt;port&gt;</c> をそのまま交換にも使う。
    /// </summary>
    internal sealed class GRushEditorLoopback
    {
        public string RedirectUri { get; private set; }

        private HttpListener listener;
        private volatile HttpListenerContext received;

        /// <summary>
        /// RFC 8252 が勧める <c>127.0.0.1</c> を先に試し、Windows の HTTP.SYS が
        /// 管理者権限を要求して弾いたときだけ <c>localhost</c> へ落ちる。
        /// サーバはどちらの表記も受け付ける。
        /// </summary>
        public void Start(int port)
        {
            foreach (var host in new[] { "127.0.0.1", "localhost" })
            {
                var redirectUri = "http://" + host + ":" + port;
                var candidate = new HttpListener();
                candidate.Prefixes.Add(redirectUri + "/");
                try
                {
                    candidate.Start();
                }
                catch (HttpListenerException)
                {
                    candidate.Close();
                    continue;
                }
                listener = candidate;
                RedirectUri = redirectUri;
                Listen();
                return;
            }
            throw new InvalidOperationException(
                "手元の待ち受け（http://127.0.0.1:" + port + "）を開けませんでした。"
            );
        }

        public void Listen()
        {
            listener.BeginGetContext(OnContext, null);
        }

        public HttpListenerContext Take()
        {
            var context = received;
            received = null;
            return context;
        }

        private void OnContext(IAsyncResult result)
        {
            try
            {
                received = listener.EndGetContext(result);
            }
            catch (Exception)
            {
                received = null;
            }
        }

        public static void Respond(HttpListenerContext context, int status, string message)
        {
            var bytes = Encoding.UTF8.GetBytes(
                "<!doctype html><meta charset=\"utf-8\"><title>GameRush</title>"
                    + "<body style=\"font-family:sans-serif;padding:40px\"><p>"
                    + message
                    + "</p></body>"
            );
            context.Response.StatusCode = status;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.Close();
        }

        public void Close()
        {
            if (listener == null)
            {
                return;
            }
            try
            {
                listener.Close();
            }
            finally
            {
                listener = null;
            }
        }
    }
}
