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

    internal sealed class GRushVersionGate
    {
        private const string FailurePrefix = "最低版数を確認できませんでした: ";

        private readonly GRushEditorGateStatus status = new GRushEditorGateStatus();

        public string MinVersion = "";
        public string Message;
        public string Error;

        public GRushGateState State => status.State;

        public bool AllowsWrites => status.AllowsWrites;

        public bool CanRetry => status.CanRetry;

        public IEnumerator Check(string origin)
        {
            status.Begin();
            Error = null;
            Message = null;
            var slot = new GRushHttpSlot();
            yield return GRushEditorHttp.Send(
                GRushEditorHttp.Json(origin + "/api/dev-client/requirements", "GET", null, null),
                slot
            );
            var reachable = slot.Result.Ok && slot.Result.Json != null;
            string minVersion = null;
            if (reachable)
            {
                var requirements = slot.Result.Json.Get("requirements");
                minVersion = requirements.Get("minVersion").AsString(null);
                Message = requirements.Get("message").AsString(null);
                MinVersion = minVersion ?? "";
            }
            status.Resolve(reachable, minVersion, GRushEditorPackage.Version);
            if (status.State == GRushGateState.Failed)
            {
                Error = FailurePrefix + slot.Result.Message();
            }
        }

        public void FailChecking(string reason)
        {
            if (status.FailChecking())
            {
                Error = FailurePrefix + reason;
            }
        }

        public string RejectedText()
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
    }
}
