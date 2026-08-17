using UnityEditor;
using UnityEngine;

namespace GRushSdk.Editor
{
    public sealed partial class GRushEditorWindow
    {
        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawGate();
            if (!gate.Blocked)
            {
                DrawAccount();
                if (credentials != null)
                {
                    DrawGames();
                    DrawUpload();
                    DrawLeaderboard();
                }
            }
            DrawMessages();
            EditorGUILayout.EndScrollView();
        }

        private static void Section(string title)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private void DrawGate()
        {
            if (gate.Blocked)
            {
                EditorGUILayout.HelpBox(gate.BlockedText(), MessageType.Error);
                if (GUILayout.Button("もう一度確認する"))
                {
                    Run(gate.Check(GRushEditorClient.DefaultOrigin()));
                }
                return;
            }
            if (gate.Error != null)
            {
                EditorGUILayout.HelpBox(gate.Error, MessageType.Warning);
            }
        }

        private void DrawAccount()
        {
            Section("アカウント");
            if (credentials == null)
            {
                if (login != null)
                {
                    EditorGUILayout.LabelField(login.Status);
                    if (GUILayout.Button("キャンセル"))
                    {
                        login.Cancel();
                    }
                    return;
                }
                requestGamesCreate = EditorGUILayout.ToggleLeft(
                    "新しいゲームを作れるようにする（games:create）",
                    requestGamesCreate
                );
                EditorGUILayout.HelpBox(
                    "games:create を含めたトークンは、対象ゲームを絞れません。"
                        + "既存のゲームへ上げるだけならチェックを外したままにしてください。",
                    MessageType.None
                );
                using (new EditorGUI.DisabledScope(busy))
                {
                    if (GUILayout.Button("ブラウザでログイン"))
                    {
                        StartLogin();
                    }
                }
                return;
            }

            EditorGUILayout.LabelField("接続先", credentials.Origin);
            EditorGUILayout.LabelField("トークン", credentials.Preview + "…");
            EditorGUILayout.LabelField("権限", string.Join(", ", credentials.Scopes));
            EditorGUILayout.LabelField("有効期限", credentials.ExpiresAt);
            EditorGUILayout.LabelField("保存先", GRushEditorCredentials.FilePath);
            if (credentials.IsExpired())
            {
                EditorGUILayout.HelpBox(
                    "トークンの有効期限が切れています。ログインし直してください。",
                    MessageType.Warning
                );
            }
            if (GUILayout.Button("ログアウト"))
            {
                GRushEditorCredentials.Clear();
                credentials = null;
                games = null;
            }
        }

        private void DrawGames()
        {
            Section("ゲーム");
            using (new EditorGUI.DisabledScope(busy))
            {
                if (GUILayout.Button("一覧を取り直す"))
                {
                    Run(RefreshGames());
                }
            }
            if (games != null && games.Count > 0)
            {
                var labels = new string[games.Count];
                for (var index = 0; index < games.Count; index++)
                {
                    labels[index] = games[index].Title + "（" + games[index].Visibility + "）";
                }
                selectedGame = EditorGUILayout.Popup("対象", selectedGame, labels);
                EditorGUILayout.HelpBox(
                    "既存のゲームの公開設定は Editor から変えられません。Studio で操作してください。",
                    MessageType.None
                );
            }
            else if (games != null)
            {
                EditorGUILayout.LabelField("まだゲームがありません。");
            }
            DrawNewGame();
        }

        private void DrawNewGame()
        {
            if (!credentials.HasScope("games:create"))
            {
                EditorGUILayout.HelpBox(
                    "新しいゲームを作るには games:create が要ります。"
                        + "ログアウトしてから「新しいゲームを作れるようにする」を入れてログインし直してください。",
                    MessageType.None
                );
                return;
            }

            Section("新しいゲームを作る");
            newGameTitle = EditorGUILayout.TextField("タイトル", newGameTitle);
            newGameDescription = EditorGUILayout.TextField("説明", newGameDescription);
            newGameVisibility = EditorGUILayout.Popup(
                "公開設定",
                newGameVisibility,
                VisibilityLabels
            );
            EditorGUILayout.HelpBox(
                "限定公開（unlisted）も審査の対象です。作った直後は遊べる状態になりません。"
                    + "一般公開（public）への申請は Studio から行います。",
                MessageType.Info
            );
            using (new EditorGUI.DisabledScope(busy || newGameTitle.Trim().Length == 0))
            {
                if (GUILayout.Button("作成する（再試行はしません）"))
                {
                    Run(CreateGameRoutine());
                }
            }
        }

        private void DrawMessages()
        {
            EditorGUILayout.Space(8);
            if (error != null)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
            if (notice != null)
            {
                EditorGUILayout.HelpBox(notice, MessageType.Info);
            }
        }
    }
}
