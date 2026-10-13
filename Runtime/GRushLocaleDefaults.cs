using System.Collections.Generic;

namespace GRushSdk
{
    // 表示言語の値の既定を JavaScript の SDK とそろえる。
    // UnityEngine に依存させないので、テストからこのファイルだけを取り出してコンパイルできる。
    internal static class GRushLocaleDefaults
    {
        public const string DeviceSource = "device";

        public static string Source(string source)
        {
            return string.IsNullOrEmpty(source) ? DeviceSource : source;
        }

        // null と空文字を捨てる。
        public static string[] Clean(string[] languages)
        {
            if (languages == null)
            {
                return new string[0];
            }
            var kept = new List<string>(languages.Length);
            foreach (var language in languages)
            {
                if (!string.IsNullOrEmpty(language))
                {
                    kept.Add(language);
                }
            }
            return kept.ToArray();
        }

        // 空になれば表示言語だけの並びにする。
        public static string[] Languages(string locale, string[] languages)
        {
            var kept = Clean(languages);
            return kept.Length > 0 ? kept : new[] { locale };
        }
    }
}
