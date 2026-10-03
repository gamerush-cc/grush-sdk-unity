namespace GRushSdk.Editor
{
    /// <summary>
    /// ビルドの確定（complete）の失敗のうち、同じビルドでは二度と確定できないものを見分ける。
    /// </summary>
    internal static class GRushEditorCompleteFailure
    {
        private const long Conflict = 409;

        /// <summary>
        /// 確定の途中でゲームの状態が変わったときの 409。同じビルドの確定をやり直せば通る。
        /// </summary>
        public const string RetryableConflict = "Game state changed during upload. Please retry.";

        /// <summary>
        /// 届いていないファイルの一覧（<c>details.missing</c>）を持たない 409 は、ビルドが
        /// failed になったか片付けられた印で、同じビルドの確定は同じ 409 を返し続ける。
        /// そのときは新しいビルドから送り直すしかない。
        /// </summary>
        public static bool RequiresNewBuild(long status, string error, int missing)
        {
            return status == Conflict && missing == 0 && error != RetryableConflict;
        }
    }
}
