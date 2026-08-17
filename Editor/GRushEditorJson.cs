using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GRushSdk.Editor
{
    /// <summary>
    /// Editor から叩く API の応答を読むための最小の JSON 値。presigned PUT の
    /// <c>headers</c> のようにキーが決まらないオブジェクトがあるため、
    /// Runtime 側の <c>JsonUtility</c> ではなくこちらを使う。
    /// </summary>
    internal sealed partial class GRushJson
    {
        private enum JsonKind
        {
            Null,
            Bool,
            Number,
            String,
            Array,
            Object,
        }

        private static readonly List<GRushJson> NoItems = new List<GRushJson>();
        private static readonly GRushJson Missing = new GRushJson { kind = JsonKind.Null };

        private JsonKind kind;
        private bool boolean;
        private double number;
        private string text;
        private List<GRushJson> items;
        private Dictionary<string, GRushJson> members;

        public bool IsNull => kind == JsonKind.Null;

        public List<GRushJson> Items => items ?? NoItems;

        public GRushJson this[string key]
        {
            get
            {
                GRushJson found;
                return members != null && members.TryGetValue(key, out found) ? found : null;
            }
        }

        /// <summary>
        /// 無いキーでも <c>null</c> を返さないので、応答の形が想定と違っても
        /// 参照で落ちずに fallback へ落ちる。
        /// </summary>
        public GRushJson Get(string key) => this[key] ?? Missing;

        public string AsString(string fallback) => kind == JsonKind.String ? text : fallback;

        public long AsLong(long fallback) => kind == JsonKind.Number ? (long)number : fallback;

        public bool AsBool(bool fallback) => kind == JsonKind.Bool ? boolean : fallback;

        public string[] AsStringArray()
        {
            if (kind != JsonKind.Array)
            {
                return null;
            }
            var result = new string[items.Count];
            for (var index = 0; index < items.Count; index++)
            {
                result[index] = items[index].AsString(null);
            }
            return result;
        }

        public Dictionary<string, string> AsStringMap()
        {
            var result = new Dictionary<string, string>();
            if (members == null)
            {
                return result;
            }
            foreach (var pair in members)
            {
                var value = pair.Value.AsString(null);
                if (value != null)
                {
                    result[pair.Key] = value;
                }
            }
            return result;
        }
    }

    internal static class GRushJsonText
    {
        public static string Escape(string value)
        {
            var builder = new StringBuilder((value == null ? 0 : value.Length) + 2);
            builder.Append('"');
            foreach (var c in value ?? string.Empty)
            {
                if (c == '"' || c == '\\')
                {
                    builder.Append('\\').Append(c);
                }
                else if (c < ' ')
                {
                    builder.Append("\\u").Append(((int)c).ToString("x4"));
                }
                else
                {
                    builder.Append(c);
                }
            }
            return builder.Append('"').ToString();
        }

        public static string Number(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
