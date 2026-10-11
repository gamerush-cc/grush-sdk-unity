using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GRushSdk.Editor
{
    /// <summary>
    /// プロジェクトの直下（Assets の隣）の grush.json を、WebGL ビルドの最上位
    /// （index.html の隣）へ写す。GameRush はビルドの最上位の grush.json だけを
    /// ランキングの定義として読むが、Unity は Assets の外のファイルも StreamingAssets も
    /// そこへは出さない。エディタのモックも同じファイルを読む。
    /// </summary>
    internal sealed class GRushEditorLeaderboardManifest : IPostprocessBuildWithReport
    {
        public const string FileName = "grush.json";

        public int callbackOrder
        {
            get { return 0; }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL)
            {
                return;
            }
            var source = Path.Combine(GRushEditorBuildOutput.ProjectRoot(), FileName);
            if (!File.Exists(source))
            {
                return;
            }
            var target = Path.Combine(report.summary.outputPath, FileName);
            File.Copy(source, target, true);
            Debug.Log("[grush-build] " + FileName + " をビルドの最上位へ写しました: " + target);
        }
    }
}
