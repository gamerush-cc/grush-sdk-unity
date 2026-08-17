using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace GRushSdk.Editor
{
    internal sealed class GRushCredentials
    {
        public string Origin;
        public string Token;
        public string Preview;
        public string ClientName;
        public string ExpiresAt;
        public string[] Scopes = new string[0];
        public string[] GameIds;

        public bool HasScope(string scope)
        {
            foreach (var granted in Scopes)
            {
                if (granted == scope)
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsExpired()
        {
            DateTime expires;
            if (
                !DateTime.TryParse(
                    ExpiresAt ?? string.Empty,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out expires
                )
            )
            {
                return false;
            }
            return expires <= DateTime.UtcNow;
        }
    }

    /// <summary>
    /// トークンは**プロジェクトの外**（ユーザープロファイル配下）に置く。
    /// <c>Assets/</c> はリポジトリへ、<c>EditorPrefs</c> はプロジェクト設定へ
    /// 漏れるため、どちらも使わない。
    /// </summary>
    internal static class GRushEditorCredentials
    {
        private const string FileName = "unity-editor-token.json";

        public static string FilePath => Path.Combine(DirectoryPath(), FileName);

        private static string DirectoryPath()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                var appData = Environment.GetEnvironmentVariable("APPDATA");
                if (string.IsNullOrEmpty(appData))
                {
                    appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                }
                return Path.Combine(appData, "GameRush");
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                return Path.Combine(home, "Library", "Application Support", "GameRush");
            }

            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrEmpty(configHome))
            {
                configHome = Path.Combine(home, ".config");
            }
            return Path.Combine(configHome, "gamerush");
        }

        public static GRushCredentials Load()
        {
            var path = FilePath;
            if (!File.Exists(path))
            {
                return null;
            }
            var json = GRushJson.Parse(File.ReadAllText(path, Encoding.UTF8));
            if (json == null)
            {
                return null;
            }
            var token = json.Get("token").AsString(null);
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }
            return new GRushCredentials
            {
                Origin = json.Get("origin").AsString(null),
                Token = token,
                Preview = json.Get("preview").AsString(""),
                ClientName = json.Get("clientName").AsString(""),
                ExpiresAt = json.Get("expiresAt").AsString(""),
                Scopes = json.Get("scopes").AsStringArray() ?? new string[0],
                GameIds = json.Get("gameIds").AsStringArray(),
            };
        }

        /// <summary>
        /// 権限を絞れなければ保存しない。書いてから絞れないと分かった場合も
        /// 消して例外を投げる — 誰でも読める場所に平文の bearer トークンを
        /// 置いたまま「保存できた」と返すのが一番まずい。
        /// </summary>
        public static void Save(GRushCredentials credentials)
        {
            var directory = DirectoryPath();
            Directory.CreateDirectory(directory);
            GRushEditorFilePermissions.RestrictToOwner(directory, true);

            var path = FilePath;
            File.WriteAllText(path, Serialize(credentials), new UTF8Encoding(false));
            try
            {
                GRushEditorFilePermissions.RestrictToOwner(path, false);
            }
            catch (Exception)
            {
                if (!TryDelete(path))
                {
                    throw new InvalidOperationException(
                        "権限を絞れなかったトークンを消せませんでした。"
                            + path
                            + " を手で消し、Studio でこのトークンを失効させてください。"
                    );
                }
                throw;
            }
        }

        private static bool TryDelete(string path)
        {
            try
            {
                File.Delete(path);
                return !File.Exists(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string Serialize(GRushCredentials credentials)
        {
            var builder = new StringBuilder("{");
            builder.Append("\"origin\":").Append(GRushJsonText.Escape(credentials.Origin));
            builder.Append(",\"token\":").Append(GRushJsonText.Escape(credentials.Token));
            builder.Append(",\"preview\":").Append(GRushJsonText.Escape(credentials.Preview));
            builder.Append(",\"clientName\":").Append(GRushJsonText.Escape(credentials.ClientName));
            builder.Append(",\"expiresAt\":").Append(GRushJsonText.Escape(credentials.ExpiresAt));
            builder.Append(",\"scopes\":").Append(Array(credentials.Scopes));
            builder.Append(",\"gameIds\":").Append(Array(credentials.GameIds));
            return builder.Append('}').ToString();
        }

        private static string Array(string[] values)
        {
            if (values == null)
            {
                return "null";
            }
            var builder = new StringBuilder("[");
            for (var index = 0; index < values.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }
                builder.Append(GRushJsonText.Escape(values[index]));
            }
            return builder.Append(']').ToString();
        }

        public static void Clear()
        {
            var path = FilePath;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
