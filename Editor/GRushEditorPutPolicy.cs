using System;

namespace GRushSdk.Editor
{
    internal static class GRushEditorPutPolicy
    {
        public const int BaseSeconds = 30;
        public const long MinBytesPerSecond = 64 * 1024;
        public const int MaxSeconds = 900;
        public const int Attempts = 3;
        public const int RetryBaseSeconds = 2;
        public const int RetryMaxSeconds = 8;
        public const int UrlLifetimeSeconds = 3600;

        public static int TimeoutSeconds(long bytes)
        {
            var size = Math.Max(bytes, 0L);
            var transfer = size / MinBytesPerSecond + (size % MinBytesPerSecond == 0 ? 0 : 1);
            var ceiling = MaxSeconds - BaseSeconds;
            return transfer >= ceiling ? MaxSeconds : BaseSeconds + (int)transfer;
        }

        public static int RetryDelaySeconds(int failedAttempt)
        {
            var shift = Math.Max(0, Math.Min(failedAttempt - 1, 16));
            return Math.Min(RetryMaxSeconds, RetryBaseSeconds << shift);
        }
    }
}
