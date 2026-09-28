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

        public async Task<bool> IsAvailableAsync()
        {
            if (!GRush.IsShareSupported)
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
            var fields = new List<string>();
            if (text != null)
            {
                fields.Add("\"text\":" + GRushWire.Escape(text));
            }
            if (screen)
            {
                fields.Add("\"image\":\"screen\"");
            }
            return "{" + string.Join(",", fields.ToArray()) + "}";
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

        private static Task<GRushResult<GRushShareStatus>> OpenAsync(string paramsJson, byte[] image)
        {
            var completion = new TaskCompletionSource<GRushResult<GRushShareStatus>>();
            var backend = GRush.Backend as IGRushShareBackend;
            if (!GRush.IsShareSupported || backend == null)
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
