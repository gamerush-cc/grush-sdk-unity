using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GRushSdk.Editor
{
    public sealed partial class GRushEditorWindow
    {
        private const string BuildOutputPrefsPrefix = "GRush.BuildOutput:";

        private string buildOutput;
        private GRushBuildOutcome lastBuild;

        [MenuItem("GameRush/推奨設定を適用", false, 40)]
        public static void MenuApplyRecommended()
        {
            if (RefuseWhileUploading())
            {
                return;
            }
            var changes = ConfirmAndApply();
            if (changes == null)
            {
                return;
            }
            foreach (var window in Resources.FindObjectsOfTypeAll<GRushEditorWindow>())
            {
                window.notice = AppliedNotice(changes);
                window.Repaint();
            }
        }

        [MenuItem("GameRush/推奨設定でビルド", false, 41)]
        public static void MenuBuildRecommended()
        {
            var window = GetWindow<GRushEditorWindow>("GameRush");
            window.Show();
            window.ConfirmAndBuild();
        }

        private static string BuildOutputPrefsKey()
        {
            return BuildOutputPrefsPrefix + GRushEditorBuildOutput.ProjectRoot();
        }

        private static bool RefuseWhileUploading()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<GRushEditorWindow>())
            {
                if (window.busy || window.upload.Running)
                {
                    EditorUtility.DisplayDialog("GameRush", "アップロード中はビルドできません。", "閉じる");
                    return true;
                }
            }
            return false;
        }

        private string BuildOutput()
        {
            if (buildOutput == null)
            {
                buildOutput = EditorPrefs.GetString(BuildOutputPrefsKey(), GRushBuild.DefaultOutput());
            }
            return buildOutput;
        }

        private void SetBuildOutput(string value)
        {
            buildOutput = value ?? "";
            EditorPrefs.SetString(BuildOutputPrefsKey(), buildOutput);
        }

        private void DrawRecommendedBuild()
        {
            Section("推奨設定でビルド");
            DrawRuleRow("項目", "いまの設定", "推奨", "", EditorStyles.miniBoldLabel);
            foreach (var rule in GRushEditorRecommendedSettings.All())
            {
                DrawRuleRow(
                    rule.Label,
                    rule.Current(),
                    rule.Recommended,
                    rule.IsOk() ? "✓" : "✗",
                    EditorStyles.miniLabel
                );
            }
            EditorGUILayout.LabelField(
                "Color Space（参考・変更しません）",
                GRushEditorRecommendedSettings.ColorSpaceLabel()
            );

            EditorGUILayout.BeginHorizontal();
            var edited = EditorGUILayout.TextField("出力先", BuildOutput());
            if (edited != BuildOutput())
            {
                SetBuildOutput(edited);
            }
            if (GUILayout.Button("選ぶ…", GUILayout.Width(60)))
            {
                var picked = EditorUtility.OpenFolderPanel("ビルドの出力先を選ぶ", BuildOutput(), "");
                if (!string.IsNullOrEmpty(picked))
                {
                    SetBuildOutput(picked);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(
                "出力先のフォルダはビルドのたびに消して作り直します。変えた設定は元に戻しません。",
                MessageType.None
            );

            EditorGUILayout.BeginHorizontal();
            using (
                new EditorGUI.DisabledScope(
                    busy || GRushEditorRecommendedSettings.Pending().Count == 0
                )
            )
            {
                if (GUILayout.Button("推奨設定を適用"))
                {
                    EditorApplication.delayCall += () => MenuApplyRecommended();
                }
            }
            using (new EditorGUI.DisabledScope(busy || BuildOutput().Trim().Length == 0))
            {
                if (GUILayout.Button("推奨設定でビルド"))
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (this != null)
                        {
                            ConfirmAndBuild();
                        }
                    };
                }
            }
            EditorGUILayout.EndHorizontal();
            DrawLastBuild();
        }

        private static void DrawRuleRow(
            string label,
            string current,
            string recommended,
            string mark,
            GUIStyle style
        )
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, style, GUILayout.Width(170));
            EditorGUILayout.LabelField(current, style);
            EditorGUILayout.LabelField(recommended, style);
            EditorGUILayout.LabelField(mark, style, GUILayout.Width(20));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLastBuild()
        {
            if (lastBuild == null)
            {
                return;
            }
            var type = !lastBuild.Ok
                ? MessageType.Error
                : lastBuild.OverGuideline
                    ? MessageType.Warning
                    : MessageType.Info;
            var text = new StringBuilder(lastBuild.Message);
            if (lastBuild.TotalBytes > 0)
            {
                text.Append("\n合計 ")
                    .Append(GRushBuild.Megabytes(lastBuild.TotalBytes))
                    .Append("（目安 ")
                    .Append(GRushBuild.Megabytes(GRushEditorRecommendedSettings.GuidelineBytes))
                    .Append(" 以下。")
                    .Append(GRushBuild.Megabytes(GRushEditorBuildRules.MaxBuildBytes))
                    .Append(" を超えるとアップロードできません）");
            }
            if (lastBuild.OverGuideline)
            {
                text.Append("\n目安を超えています。フィードで最初の読み込みが遅くなります。");
            }
            EditorGUILayout.HelpBox(text.ToString(), type);
        }

        private void ConfirmAndBuild()
        {
            if (RefuseWhileUploading())
            {
                return;
            }
            var output = BuildOutput();
            var message = new StringBuilder();
            message.Append("次の出力先を消して WebGL でビルドします。\n").Append(output).Append("\n\n");
            AppendPending(message, "ビルド前に次の設定を変えます（元に戻しません）:");
            if (!EditorUtility.DisplayDialog("GameRush", message.ToString(), "ビルドする", "やめる"))
            {
                return;
            }
            var outcome = GRushBuild.Build(output);
            lastBuild = outcome;
            if (outcome.Ok && outcome.OutputPath != null)
            {
                buildDirectory = outcome.OutputPath;
                Rescan();
                notice = "ビルドしました。ログインしていれば、そのまま「ビルドをアップロード」から上げられます。";
            }
            Repaint();
        }

        private static List<string> ConfirmAndApply()
        {
            var message = new StringBuilder();
            if (!AppendPending(message, "次の設定を変えます（元に戻しません）:"))
            {
                EditorUtility.DisplayDialog("GameRush", "すでに推奨設定になっています。", "閉じる");
                return null;
            }
            if (!EditorUtility.DisplayDialog("GameRush", message.ToString(), "適用する", "やめる"))
            {
                return null;
            }
            return GRushEditorRecommendedSettings.ApplyAll();
        }

        private static bool AppendPending(StringBuilder message, string heading)
        {
            var pending = GRushEditorRecommendedSettings.Pending();
            if (pending.Count == 0)
            {
                return false;
            }
            message.Append(heading).Append('\n');
            foreach (var rule in pending)
            {
                message
                    .Append("・")
                    .Append(rule.Label)
                    .Append(": ")
                    .Append(rule.Current())
                    .Append(" -> ")
                    .Append(rule.Recommended)
                    .Append('\n');
            }
            return true;
        }

        private static string AppliedNotice(List<string> changes)
        {
            return "推奨設定を適用しました。\n" + string.Join("\n", changes);
        }
    }
}
