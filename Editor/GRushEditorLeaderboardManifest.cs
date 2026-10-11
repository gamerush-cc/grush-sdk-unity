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
    /// そこへは出さない。プロジェクトの直下に無いのに出力先にあれば、警告だけを出す。
    /// エディタのモックも同じファイルを読む。
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
            var target = Path.Combine(report.summary.outputPath, FileName);
            if (!File.Exists(source))
            {
                // 出力先のファイルは、前回写したものかもしれず、作者が手で置いたものや WebGL
                // テンプレートから出たものかもしれない。見分けられないので消さない。残ったまま
                // アップロードすると、その中身で Studio の定義を書き換える。
                if (File.Exists(target))
                {
                    Debug.LogWarning(
                        "[grush-build] " + FileName + " がプロジェクトの直下に無いのに、ビルドの出力先にあります。"
                            + "このままアップロードすると、その中身でランキングの定義を書き換えます。"
                            + "要らなければ消してください: " + target
                    );
                }
                return;
            }
            File.Copy(source, target, true);
            Debug.Log("[grush-build] " + FileName + " をビルドの最上位へ写しました: " + target);
        }
    }
}
