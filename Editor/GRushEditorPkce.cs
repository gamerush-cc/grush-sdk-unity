using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace GRushSdk.Editor
{
    /// <summary>
    /// RFC 8252 + PKCE の材料。<c>code_challenge_method</c> はサーバが
    /// <c>S256</c> しか受け付けず、<c>code_verifier</c> は 43〜128 文字。
    /// 32 バイトの base64url は 43 文字ちょうどで、使う文字も許容集合に収まる。
    /// </summary>
    internal static class GRushEditorPkce
    {
        public static string CreateVerifier()
        {
            return Base64Url(RandomBytes(32));
        }

        public static string Challenge(string verifier)
        {
            using (var sha256 = SHA256.Create())
            {
                return Base64Url(sha256.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
            }
        }

        public static string CreateState()
        {
            return Base64Url(RandomBytes(16));
        }

        /// <summary>
        /// 空きポートを OS に選ばせてから離す。選び直しの隙間に他所へ取られる
        /// 可能性は残るが、その場合は <see cref="HttpListener"/> の起動が
        /// 例外で落ちるので、黙って別の待ち受けへ繋がることはない。
        /// </summary>
        public static int FreeLoopbackPort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            try
            {
                return ((IPEndPoint)probe.LocalEndpoint).Port;
            }
            finally
            {
                probe.Stop();
            }
        }

        private static byte[] RandomBytes(int length)
        {
            var bytes = new byte[length];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }
            return bytes;
        }

        private static string Base64Url(byte[] bytes)
        {
            return Convert
                .ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
