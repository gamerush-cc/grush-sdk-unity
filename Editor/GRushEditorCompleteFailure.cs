namespace GRushSdk.Editor
{
    /// <summary>
    /// ビルドの確定（complete）の失敗のうち、同じビルドでは二度と確定できないものを見分ける。
    /// </summary>
    internal static class GRushEditorCompleteFailure
    {
        private const long BadRequest = 400;
        private const long Conflict = 409;
        private const long Unprocessable = 422;

        /// <summary>
        /// 確定の途中でゲームの状態が変わったときの 409。同じビルドの確定をやり直せば通る。
        /// </summary>
        public const string RetryableConflict = "Game state changed during upload. Please retry.";

        /// <summary>登録したファイルの一覧がビルドに無いときの 400。</summary>
        public const string NoFileManifest = "Build has no file manifest.";

        /// <summary>
        /// サーバはアップロード中のビルドを、次の失敗を返す前に failed にする。届いていない
        /// ファイルがある 409（<c>details.missing</c> 付き）、中身の検査に落ちた 422、ファイルの
        /// 一覧の無い 400 がそれに当たり、failed のビルドや片付けられたビルドへの確定は 409 になる。
        /// どれも同じビルドの確定は失敗し続けるので、新しいビルドから送り直すしかない。
        /// 409 のうち、ゲームの状態の競合だけは同じビルドでやり直せる。
        /// </summary>
        public static bool RequiresNewBuild(long status, string error)
        {
            switch (status)
            {
                case Conflict:
                    return error != RetryableConflict;
                case Unprocessable:
                    return true;
                case BadRequest:
                    return error == NoFileManifest;
                default:
                    return false;
            }
        }
    }
}
