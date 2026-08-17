using System.Collections;
using System.IO;
using UnityEditor.PackageManager;

namespace GRushSdk.Editor
{
    internal static class GRushEditorPackage
    {
        public const string Name = "cc.gamerush.sdk";

        private static string cached;

        public static string Version
        {
            get
            {
                if (cached == null)
                {
                    cached = Resolve() ?? "";
                }
                return cached;
            }
        }

        private static string Resolve()
        {
            var info = PackageInfo.FindForAssembly(typeof(GRushEditorClient).Assembly);
            if (info == null)
            {
                return null;
            }
            if (!string.IsNullOrEmpty(info.version))
            {
                return info.version;
            }
            var manifest = Path.Combine(info.resolvedPath ?? "", "package.json");
            if (!File.Exists(manifest))
            {
                return null;
            }
            var json = GRushJson.Parse(File.ReadAllText(manifest));
            return json == null ? null : json.Get("version").AsString(null);
        }
    }

    /// <summary>
    /// サーバが最低版数を配る（<c>GET /api/dev-client/requirements</c>）。
    /// 判定は <c>packages/shared/src/contracts/dev-client.ts</c> の
    /// <c>isSupportedDevClientVersion</c> の写し。
    /// </summary>
    internal sealed class GRushVersionGate
    {
        public bool Checked;
        public bool Blocked;
        public string MinVersion = "";
        public string Message;
        public string Error;

        public IEnumerator Check(string origin)
        {
            Error = null;
            var slot = new GRushHttpSlot();
            yield return GRushEditorHttp.Send(
                GRushEditorHttp.Json(origin + "/api/dev-client/requirements", "GET", null, null),
                slot
            );
            Checked = true;
            if (!slot.Result.Ok || slot.Result.Json == null)
            {
                Error = "最低版数を確認できませんでした: " + slot.Result.Message();
                yield break;
            }
            var requirements = slot.Result.Json.Get("requirements");
            MinVersion = requirements.Get("minVersion").AsString("0.0.0");
            Message = requirements.Get("message").AsString(null);
            Blocked = !IsSupported(GRushEditorPackage.Version, MinVersion);
        }

        public string BlockedText()
        {
            var text =
                "この Editor 拡張は古いため使えません（いま "
                + (string.IsNullOrEmpty(GRushEditorPackage.Version)
                    ? "不明"
                    : GRushEditorPackage.Version)
                + " / 必要 "
                + MinVersion
                + " 以上）。SDK を更新してください。";
            return string.IsNullOrEmpty(Message) ? text : text + "\n" + Message;
        }

        public static bool IsSupported(string version, string minVersion)
        {
            var current = Parse(version);
            var minimum = Parse(minVersion);
            if (current == null || minimum == null)
            {
                return false;
            }
            for (var index = 0; index < 3; index++)
            {
                if (current[index] != minimum[index])
                {
                    return current[index] > minimum[index];
                }
            }
            return true;
        }

        private static int[] Parse(string value)
        {
            var parts = (value ?? "").Split('.');
            if (parts.Length != 3)
            {
                return null;
            }
            var numbers = new int[3];
            for (var index = 0; index < 3; index++)
            {
                if (!IsDigits(parts[index]) || !int.TryParse(parts[index], out numbers[index]))
                {
                    return null;
                }
            }
            return numbers;
        }

        private static bool IsDigits(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            foreach (var c in value)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }
            return true;
        }
    }
}
