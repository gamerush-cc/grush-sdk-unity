using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GRushSdk
{
    public enum GRushShareStatus
    {
        Cancelled,
        Opened,
    }

    public interface IGRushShareBackend
    {
        void Share(string paramsJson, byte[] image, Action<GRushRpcResponse> onDone);
    }

    [Serializable]
    internal class GRushShareResultWire
    {
        public string status;
    }

    [Serializable]
    internal class GRushShareAvailabilityWire
    {
        public bool available;
    }

    public sealed class GRushShareApi
    {
        private const string AvailabilityMethod = "share.getAvailability";

        public Task<GRushResult<GRushShareStatus>> ShareAsync(string text)
        {
            return ShareAsync(text, null);
        }

        public Task<GRushResult<GRushShareStatus>> ShareAsync(string text, byte[] image)
        {
            return OpenAsync(OpenParams(text, false), image);
        }

        public Task<GRushResult<GRushShareStatus>> ShareScreenAsync(string text)
        {
            return OpenAsync(OpenParams(text, true), null);
        }

        /// <summary>
        /// 起動パラメータを添えて共有する。受け取った人がリンクを開くと、ゲームの <see cref="GRush.Launch"/> に
        /// 渡る（<c>stage</c> = <c>3</c> ならリンクに <c>?gp.stage=3</c> が付く）。
        /// </summary>
        /// <remarks>
        /// キーは英数字と <c>_-</c> で 32 文字まで（<c>__proto__</c> は使えない）、値は英数字と <c>._~:,-</c> で
        /// 128 文字まで（空も可）、8 個まで。1つでも合わなければ共有を開かずに <see cref="GRushErrorCode.InvalidParams"/>。
        /// null と空の辞書は渡さないときと同じ。
        /// <see cref="GRush.ShareLaunchProtocolVersion"/> 未満の GameRush では、空でなければ共有を開かずに
        /// <see cref="GRushErrorCode.Unsupported"/> を返す。launchParams を外して呼び直せば共有できる。
        /// リンクは誰にでも見え、そのまま転送されるので、プレイヤーを識別できる値（招待コードなど）を入れない。
        /// 受け取る側のゲームは値を信用せず、確かめてから使う。
        /// </remarks>
        /// <param name="text">共有の本文。</param>
        /// <param name="image">添える画像（PNG などのバイト列）。null なら画像なし。</param>
        /// <param name="launchParams">共有リンクに添える起動パラメータ。</param>
        public Task<GRushResult<GRushShareStatus>> ShareAsync(
            string text,
            byte[] image,
            IReadOnlyDictionary<string, string> launchParams
        )
        {
            return OpenWithLaunchParamsAsync(OpenParams(text, false, launchParams), image, launchParams);
        }

        /// <summary>
        /// ゲームの canvas のスクショと起動パラメータを添えて共有する。起動パラメータの規則と、古い GameRush での
        /// 扱いは <see cref="ShareAsync(string, byte[], IReadOnlyDictionary{string, string})"/> と同じ。
        /// </summary>
        /// <remarks>
        /// リンクは誰にでも見え、そのまま転送されるので、プレイヤーを識別できる値（招待コードなど）を入れない。
        /// </remarks>
        /// <param name="text">共有の本文。</param>
        /// <param name="launchParams">共有リンクに添える起動パラメータ。</param>
        public Task<GRushResult<GRushShareStatus>> ShareScreenAsync(
            string text,
            IReadOnlyDictionary<string, string> launchParams
        )
        {
            return OpenWithLaunchParamsAsync(OpenParams(text, true, launchParams), null, launchParams);
        }

        public async Task<bool> IsAvailableAsync()
        {
            if (!GRush.IsShareAvailable)
            {
                return false;
            }
            var response = await GRush.CallAsync(AvailabilityMethod, null);
            if (!response.Ok)
            {
                return false;
            }
            var wire = GRushWire.Parse<GRushShareAvailabilityWire>(response.Value);
            return wire != null && wire.available;
        }

        internal static string OpenParams(string text, bool screen)
        {
            return OpenParams(text, screen, null);
        }

        // キーと値の形は GameRush が確かめる。null の値も JSON の null のまま送り、invalidParams を受け取る。
        internal static string OpenParams(
            string text,
            bool screen,
            IReadOnlyDictionary<string, string> launchParams
        )
        {
            var fields = new List<string>();
            if (text != null)
            {
                fields.Add("\"text\":" + GRushWire.Escape(text));
            }
            if (screen)
            {
                fields.Add("\"image\":\"screen\"");
            }
            if (HasLaunchParams(launchParams))
            {
                var entries = new List<string>();
                foreach (var pair in launchParams)
                {
                    entries.Add(
                        GRushWire.Escape(pair.Key ?? "")
                            + ":"
                            + (pair.Value == null ? "null" : GRushWire.Escape(pair.Value))
                    );
                }
                fields.Add("\"launchParams\":{" + string.Join(",", entries.ToArray()) + "}");
            }
            return "{" + string.Join(",", fields.ToArray()) + "}";
        }

        private static bool HasLaunchParams(IReadOnlyDictionary<string, string> launchParams)
        {
            return launchParams != null && launchParams.Count > 0;
        }

        internal static GRushResult<GRushShareStatus> ToStatus(GRushRpcResponse response)
        {
            if (!response.Ok)
            {
                return GRushResult<GRushShareStatus>.Failure(response.Code, response.Message);
            }
            var wire = GRushWire.Parse<GRushShareResultWire>(response.ResultJson);
            if (wire == null)
            {
                return GRushResult<GRushShareStatus>.Failure(
                    GRushErrorCode.Internal,
                    "GameRush returned an unreadable share result."
                );
            }
            return GRushResult<GRushShareStatus>.Success(
                wire.status == "opened" ? GRushShareStatus.Opened : GRushShareStatus.Cancelled
            );
        }

        // 古いランタイムは launchParams を読まずに共有を開くので、添えるときはそれを読む版数を求める。
        private static Task<GRushResult<GRushShareStatus>> OpenWithLaunchParamsAsync(
            string paramsJson,
            byte[] image,
            IReadOnlyDictionary<string, string> launchParams
        )
        {
            if (HasLaunchParams(launchParams) && GRush.ProtocolVersion < GRush.ShareLaunchProtocolVersion)
            {
                return Task.FromResult(GRushResult<GRushShareStatus>.Unsupported());
            }
            return OpenAsync(paramsJson, image);
        }

        private static Task<GRushResult<GRushShareStatus>> OpenAsync(string paramsJson, byte[] image)
        {
            var completion = new TaskCompletionSource<GRushResult<GRushShareStatus>>();
            var backend = GRush.Backend as IGRushShareBackend;
            if (!GRush.IsShareAvailable || backend == null)
            {
                completion.SetResult(GRushResult<GRushShareStatus>.Unsupported());
                return completion.Task;
            }
            backend.Share(
                paramsJson,
                image != null && image.Length > 0 ? image : null,
                response => completion.TrySetResult(ToStatus(response))
            );
            return completion.Task;
        }
    }
}
