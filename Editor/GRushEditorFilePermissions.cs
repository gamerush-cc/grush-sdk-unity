using System;
using System.Diagnostics;
using UnityEngine;

namespace GRushSdk.Editor
{
    /// <summary>
    /// 平文の bearer トークンを置く前に、本人だけが読めることを**確かめる**。
    /// 絞れなければ例外を投げるので、呼び出し側は書いたものを消すこと
    /// （絞れたつもりで置いたままにするのが一番まずい）。
    ///
    /// <c>System.Security.AccessControl</c> は Unity の API 互換レベル次第で
    /// 参照が解決しないため、どの環境でも同じ経路になる外部プロセスで扱う。
    /// </summary>
    internal static class GRushEditorFilePermissions
    {
        private const int TimeoutMs = 15000;

        public static void RestrictToOwner(string path, bool isDirectory)
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                RestrictWithIcacls(path, isDirectory);
                return;
            }
            RestrictWithChmod(path, isDirectory);
        }

        private static void RestrictWithIcacls(string path, bool isDirectory)
        {
            var user = Environment.UserDomainName + "\\" + Environment.UserName;
            var rights = isDirectory ? "(OI)(CI)(F)" : "(F)";
            var applied = Run(
                "icacls",
                Quote(path) + " /inheritance:r /grant:r " + Quote(user + ":" + rights)
            );
            if (applied == null)
            {
                throw Failure(path, "icacls で継承を切れませんでした");
            }
            var granted = Run("icacls", Quote(path));
            if (granted == null || !OnlyGrantedTo(granted, path, user))
            {
                throw Failure(path, "icacls の適用後も本人以外の権限が残っています");
            }
        }

        /// <summary>
        /// <c>icacls &lt;path&gt;</c> は1行目に「パス + 先頭の ACE」、以降1行1 ACE を
        /// 出し、空行で終わる。本人あての ACE が1つだけ残っていることを見る。
        /// </summary>
        private static bool OnlyGrantedTo(string output, string path, string user)
        {
            var entries = 0;
            foreach (var raw in output.Replace("\r", "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0)
                {
                    break;
                }
                if (line.StartsWith(path, StringComparison.OrdinalIgnoreCase))
                {
                    line = line.Substring(path.Length).Trim();
                }
                if (line.Length == 0)
                {
                    continue;
                }
                var separator = line.LastIndexOf(":(", StringComparison.Ordinal);
                if (separator < 0)
                {
                    return false;
                }
                var principal = line.Substring(0, separator).Trim();
                if (!string.Equals(principal, user, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                entries++;
            }
            return entries == 1;
        }

        private static void RestrictWithChmod(string path, bool isDirectory)
        {
            var mode = isDirectory ? "700" : "600";
            if (Run("/bin/chmod", mode + " " + Quote(path)) == null)
            {
                throw Failure(path, "chmod " + mode + " に失敗しました");
            }
            var listed = Run("/bin/ls", "-ld " + Quote(path));
            if (listed == null || !IsOwnerOnly(listed, isDirectory))
            {
                throw Failure(path, "chmod の適用後も本人以外の権限が残っています");
            }
        }

        /// <summary>
        /// <c>ls -ld</c> の先頭10文字が権限。末尾の <c>+</c> は拡張 ACL が
        /// 別に効いていることを意味するので、その時点で絞れていない。
        /// </summary>
        private static bool IsOwnerOnly(string output, bool isDirectory)
        {
            var line = output.Replace("\r", "").Split('\n')[0];
            if (line.Length < 11)
            {
                return false;
            }
            if (line[10] == '+')
            {
                return false;
            }
            var expected = isDirectory ? "drwx------" : "-rw-------";
            return string.CompareOrdinal(line, 0, expected, 0, expected.Length) == 0;
        }

        private static string Quote(string value)
        {
            return "\"" + value + "\"";
        }

        private static Exception Failure(string path, string reason)
        {
            return new InvalidOperationException(
                "トークンの保存先（"
                    + path
                    + "）を本人だけが読める状態にできませんでした（"
                    + reason
                    + "）。トークンは保存していません。"
            );
        }

        private static string Run(string fileName, string arguments)
        {
            var startInfo = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            try
            {
                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return null;
                    }
                    var output = process.StandardOutput.ReadToEnd();
                    process.StandardError.ReadToEnd();
                    if (!process.WaitForExit(TimeoutMs))
                    {
                        return null;
                    }
                    return process.ExitCode == 0 ? output : null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
