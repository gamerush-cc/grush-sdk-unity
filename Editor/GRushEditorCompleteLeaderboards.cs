using System.Text;

namespace GRushSdk.Editor
{
    /// <summary>
    /// ビルドの確定（complete）の応答の <c>leaderboards.failed</c> を、作者へ出す文言にする。
    /// grush.json の定義が書けなくてもビルドは確定するので、ここで知らせないと作者は気づけない。
    /// </summary>
    internal static class GRushEditorCompleteLeaderboards
    {
        /// <summary>書けなかった定義が無ければ null を返す。</summary>
        public static string FailedMessage(GRushJson response)
        {
            if (response == null)
            {
                return null;
            }
            var failed = response.Get("leaderboards").Get("failed").Items;
            if (failed.Count == 0)
            {
                return null;
            }
            var builder = new StringBuilder(
                "ビルドは確定しましたが、grush.json のランキングの定義のうち次のものは書けませんでした。"
            );
            foreach (var item in failed)
            {
                builder
                    .Append("\n・")
                    .Append(item.Get("key").AsString("?"))
                    .Append(": ")
                    .Append(Reason(item.Get("code").AsString("")));
            }
            return builder.Append("\nStudio のランキングで今の定義を確認してください。").ToString();
        }

        private static string Reason(string code)
        {
            switch (code)
            {
                case "build.leaderboardManifestSemanticsLocked":
                    return "スコアが投稿済みなので、並び順・値の型・集約・期間は変えられません。新しい key で定義してください";
                case "build.leaderboardManifestLimit":
                    return "1つのゲームのランキングの上限を超えます";
                case "build.leaderboardManifestApplyFailed":
                    return "書き込みに失敗しました。もう一度アップロードしてください";
                default:
                    return code;
            }
        }
    }
}
