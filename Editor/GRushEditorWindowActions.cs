using System.Collections;
using UnityEngine;

namespace GRushSdk.Editor
{
    public sealed partial class GRushEditorWindow
    {
        private IEnumerator RefreshGames()
        {
            if (credentials == null || !credentials.HasScope("games:read"))
            {
                yield break;
            }
            yield return GRushEditorApi.ListGames(
                Client(),
                loaded =>
                {
                    games = loaded;
                    selectedGame = Mathf.Clamp(selectedGame, 0, Mathf.Max(0, games.Count - 1));
                },
                message => error = message
            );
        }

        private IEnumerator CreateGameRoutine()
        {
            notice = null;
            GRushGameSummary created = null;
            yield return GRushEditorApi.CreateGame(
                Client(),
                newGameTitle.Trim(),
                newGameDescription.Trim(),
                VisibilityValues[newGameVisibility],
                game => created = game,
                message => error = message
            );
            if (created == null)
            {
                yield break;
            }
            newGameTitle = "";
            newGameDescription = "";
            notice = created.Title + " を作りました。ビルドを上げてください。";
            yield return RefreshGames();
            SelectGame(created.Id);
        }

        private IEnumerator DeclareLeaderboardRoutine(string gameId)
        {
            notice = null;
            yield return GRushEditorApi.DeclareLeaderboard(
                Client(),
                gameId,
                leaderboard,
                () => notice = leaderboard.Key + " を適用しました。",
                message => error = message
            );
        }

        private void SelectGame(string gameId)
        {
            if (games == null)
            {
                return;
            }
            for (var index = 0; index < games.Count; index++)
            {
                if (games[index].Id == gameId)
                {
                    selectedGame = index;
                    return;
                }
            }
        }

        private GRushGameSummary SelectedGame()
        {
            if (games == null || games.Count == 0)
            {
                return null;
            }
            return games[Mathf.Clamp(selectedGame, 0, games.Count - 1)];
        }

        private void Rescan()
        {
            manifest = GRushEditorBuildManifest.Collect(buildDirectory);
            upload.Reset();
        }
    }
}
