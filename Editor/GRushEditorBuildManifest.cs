using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GRushSdk.Editor
{
    internal sealed class GRushBuildFile
    {
        public string Path;
        public string FullPath;
        public long Size;
    }

    internal sealed class GRushBuildManifest
    {
        public List<GRushBuildFile> Files = new List<GRushBuildFile>();
        public long TotalBytes;
        public string Error;

        public bool Ok => Error == null;

        /// <summary>
        /// <c>path</c> と <c>size</c> だけを送る。**<c>encoding</c> は付けない** —
        /// サーバは <c>encoding</c> の付いたファイルの保存キーへ <c>.gz</c> /
        /// <c>.br</c> を足すので、Unity が書き出した <c>Build/x.wasm.br</c> に
        /// <c>encoding</c> を付けると <c>Build/x.wasm.br.br</c> へ入って 404 になる。
        /// 配信側は要求パスの末尾から Content-Encoding を決める。
        /// </summary>
        public string ToRequestBody()
        {
            var builder = new StringBuilder("{\"files\":[");
            for (var index = 0; index < Files.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }
                builder.Append("{\"path\":").Append(GRushJsonText.Escape(Files[index].Path));
                builder.Append(",\"size\":").Append(Files[index].Size).Append('}');
            }
            return builder.Append("]}").ToString();
        }
    }

    internal static class GRushEditorBuildManifest
    {
        public static GRushBuildManifest Collect(string rootDirectory)
        {
            var manifest = new GRushBuildManifest();
            if (string.IsNullOrEmpty(rootDirectory) || !Directory.Exists(rootDirectory))
            {
                manifest.Error = "ビルド出力ディレクトリが見つかりません。";
                return manifest;
            }

            var root = Path.GetFullPath(rootDirectory).TrimEnd('/', '\\');
            var seen = new HashSet<string>();
            var hasEntry = false;
            var hasGeneratedConfig = false;

            foreach (var fullPath in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                var relative = GRushEditorBuildRules.NormalizeUploadPath(
                    Path.GetFullPath(fullPath).Substring(root.Length).TrimStart('/', '\\')
                );
                if (GRushEditorBuildRules.IsIgnoredEntry(relative))
                {
                    continue;
                }
                var pathError = GRushEditorBuildRules.ValidateBuildPath(relative);
                if (pathError != null)
                {
                    manifest.Error = pathError;
                    return manifest;
                }
                if (!seen.Add(relative))
                {
                    manifest.Error = relative + ": 同じパスが2つあります。";
                    return manifest;
                }

                var size = new FileInfo(fullPath).Length;
                if (relative == GRushEditorBuildRules.EntryPath)
                {
                    hasEntry = true;
                }
                if (relative == GRushEditorBuildRules.GeneratedPlayerConfigPath)
                {
                    hasGeneratedConfig = true;
                }
                manifest.TotalBytes += size;
                manifest.Files.Add(
                    new GRushBuildFile
                    {
                        Path = relative,
                        FullPath = fullPath,
                        Size = size,
                    }
                );
            }

            manifest.Error = Validate(manifest, hasEntry, hasGeneratedConfig, root);
            return manifest;
        }

        private static string Validate(
            GRushBuildManifest manifest,
            bool hasEntry,
            bool hasGeneratedConfig,
            string root
        )
        {
            if (manifest.Files.Count == 0)
            {
                return "アップロードできるファイルがありません。";
            }
            if (manifest.Files.Count > GRushEditorBuildRules.MaxBuildFileCount)
            {
                return "ファイルが多すぎます（上限 "
                    + GRushEditorBuildRules.MaxBuildFileCount
                    + " 個、いまは "
                    + manifest.Files.Count
                    + " 個）。";
            }
            if (hasGeneratedConfig && hasEntry)
            {
                return GRushEditorBuildRules.GeneratedPlayerConfigPath
                    + " と "
                    + GRushEditorBuildRules.EntryPath
                    + " は同時に置けません。";
            }
            if (!hasEntry)
            {
                return MissingEntryMessage(manifest, root);
            }
            if (manifest.TotalBytes > GRushEditorBuildRules.MaxBuildBytes)
            {
                return "ビルドが大きすぎます（上限 "
                    + GRushEditorBuildRules.MaxBuildBytes / (1024 * 1024)
                    + " MB、いまは "
                    + manifest.TotalBytes / (1024 * 1024)
                    + " MB）。";
            }
            return null;
        }

        /// <summary>
        /// 1階層下に <c>index.html</c> があるなら、そこを選び直せばよいだけなので
        /// 名指しで返す。パスを黙って書き換えると、送ったものと手元の中身が
        /// 食い違ったまま気づけなくなる。
        /// </summary>
        private static string MissingEntryMessage(GRushBuildManifest manifest, string root)
        {
            foreach (var file in manifest.Files)
            {
                var parts = file.Path.Split('/');
                if (parts.Length == 2 && parts[1] == GRushEditorBuildRules.EntryPath)
                {
                    return GRushEditorBuildRules.EntryPath
                        + " が直下にありません。"
                        + Path.Combine(root, parts[0])
                        + " を選んでください。";
                }
            }
            return GRushEditorBuildRules.EntryPath
                + " が直下にありません。WebGL の書き出し先ディレクトリを選んでください。";
        }
    }
}
