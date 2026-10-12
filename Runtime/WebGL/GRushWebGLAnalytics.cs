#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace GRushSdk
{
    // 分析イベントは同期で bool を返すので、コールバックを持たない1本の関数で渡す。kind が null のときは、
    // GameRush の中で動いているか（ランタイムの isAvailable）を返す。
    internal static class GRushWebGLAnalytics
    {
        [DllImport("__Internal")]
        private static extern int GRushJsAnalytics(string kind, string paramsJson);

        public static bool IsAvailable()
        {
            return GRushJsAnalytics(null, null) == 1;
        }

        public static bool Log(GRushAnalyticsEvent analyticsEvent)
        {
            return GRushJsAnalytics(analyticsEvent.Kind, analyticsEvent.ParamsJson()) == 1;
        }
    }
}
#endif
