using System;
using UnityEngine;

namespace GRushSdk
{
    internal static class GRushMockLocale
    {
        private static string locale;
        private static string source;
        private static string[] languages;

        public static event Action<string> Changed;

        public static void Set(string newLocale, string newSource, string[] newLanguages)
        {
            locale = string.IsNullOrEmpty(newLocale) ? null : newLocale;
            source = string.IsNullOrEmpty(newSource) ? "user" : newSource;
            languages = newLanguages;
            var handler = Changed;
            if (handler != null)
            {
                handler(CurrentJson());
            }
        }

        public static void Reset()
        {
            locale = null;
            source = null;
            languages = null;
        }

        public static string CurrentJson()
        {
            var tag = locale ?? FromSystemLanguage(Application.systemLanguage);
            var origin = locale == null ? "device" : source;
            var list = languages != null && languages.Length > 0 ? languages : new[] { tag };
            var builder = new System.Text.StringBuilder();
            builder.Append("{\"locale\":").Append(GRushWire.Escape(tag));
            builder.Append(",\"source\":").Append(GRushWire.Escape(origin));
            builder.Append(",\"languages\":[");
            for (var index = 0; index < list.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }
                builder.Append(GRushWire.Escape(list[index]));
            }
            return builder.Append("]}").ToString();
        }

        internal static string FromSystemLanguage(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.Japanese:
                    return "ja";
                case SystemLanguage.Korean:
                    return "ko";
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                    return "zh-Hans";
                case SystemLanguage.ChineseTraditional:
                    return "zh-Hant";
                case SystemLanguage.French:
                    return "fr";
                case SystemLanguage.German:
                    return "de";
                case SystemLanguage.Spanish:
                    return "es";
                case SystemLanguage.Portuguese:
                    return "pt";
                case SystemLanguage.Italian:
                    return "it";
                case SystemLanguage.Russian:
                    return "ru";
                case SystemLanguage.Indonesian:
                    return "id";
                case SystemLanguage.Thai:
                    return "th";
                case SystemLanguage.Vietnamese:
                    return "vi";
                case SystemLanguage.Turkish:
                    return "tr";
                case SystemLanguage.Arabic:
                    return "ar";
                default:
                    return "en";
            }
        }
    }
}
