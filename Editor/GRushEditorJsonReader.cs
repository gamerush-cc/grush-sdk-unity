using System.Collections.Generic;

namespace GRushSdk.Editor
{
    internal sealed partial class GRushJson
    {
        public static GRushJson Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }
            var index = 0;
            var value = ReadValue(json, ref index);
            if (value == null)
            {
                return null;
            }
            SkipSpace(json, ref index);
            return index == json.Length ? value : null;
        }

        private static void SkipSpace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
            {
                index++;
            }
        }

        private static bool Matches(string json, int index, string literal)
        {
            return index + literal.Length <= json.Length
                && string.CompareOrdinal(json, index, literal, 0, literal.Length) == 0;
        }

        private static GRushJson ReadValue(string json, ref int index)
        {
            SkipSpace(json, ref index);
            if (index >= json.Length)
            {
                return null;
            }
            var c = json[index];
            if (c == '{')
            {
                return ReadObject(json, ref index);
            }
            if (c == '[')
            {
                return ReadArray(json, ref index);
            }
            if (c == '"')
            {
                var parsed = ReadString(json, ref index);
                return parsed == null
                    ? null
                    : new GRushJson { kind = JsonKind.String, text = parsed };
            }
            if (Matches(json, index, "true"))
            {
                index += 4;
                return new GRushJson { kind = JsonKind.Bool, boolean = true };
            }
            if (Matches(json, index, "false"))
            {
                index += 5;
                return new GRushJson { kind = JsonKind.Bool, boolean = false };
            }
            if (Matches(json, index, "null"))
            {
                index += 4;
                return new GRushJson { kind = JsonKind.Null };
            }
            return ReadNumber(json, ref index);
        }

        private static GRushJson ReadArray(string json, ref int index)
        {
            index++;
            var value = new GRushJson { kind = JsonKind.Array, items = new List<GRushJson>() };
            SkipSpace(json, ref index);
            if (index < json.Length && json[index] == ']')
            {
                index++;
                return value;
            }
            for (; ; )
            {
                var item = ReadValue(json, ref index);
                if (item == null)
                {
                    return null;
                }
                value.items.Add(item);
                SkipSpace(json, ref index);
                if (index >= json.Length)
                {
                    return null;
                }
                if (json[index] == ']')
                {
                    index++;
                    return value;
                }
                if (json[index] != ',')
                {
                    return null;
                }
                index++;
            }
        }

        private static GRushJson ReadObject(string json, ref int index)
        {
            index++;
            var value = new GRushJson
            {
                kind = JsonKind.Object,
                members = new Dictionary<string, GRushJson>(),
            };
            SkipSpace(json, ref index);
            if (index < json.Length && json[index] == '}')
            {
                index++;
                return value;
            }
            for (; ; )
            {
                SkipSpace(json, ref index);
                var key = ReadString(json, ref index);
                if (key == null)
                {
                    return null;
                }
                SkipSpace(json, ref index);
                if (index >= json.Length || json[index] != ':')
                {
                    return null;
                }
                index++;
                var item = ReadValue(json, ref index);
                if (item == null)
                {
                    return null;
                }
                value.members[key] = item;
                SkipSpace(json, ref index);
                if (index >= json.Length)
                {
                    return null;
                }
                if (json[index] == '}')
                {
                    index++;
                    return value;
                }
                if (json[index] != ',')
                {
                    return null;
                }
                index++;
            }
        }
    }
}
