using System.Globalization;
using System.Text;

namespace GRushSdk.Editor
{
    internal sealed partial class GRushJson
    {
        private static GRushJson ReadNumber(string json, ref int index)
        {
            var start = index;
            while (index < json.Length && "+-.eE0123456789".IndexOf(json[index]) >= 0)
            {
                index++;
            }
            double parsed;
            if (
                start == index
                || !double.TryParse(
                    json.Substring(start, index - start),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out parsed
                )
            )
            {
                return null;
            }
            return new GRushJson { kind = JsonKind.Number, number = parsed };
        }

        private static string ReadString(string json, ref int index)
        {
            if (index >= json.Length || json[index] != '"')
            {
                return null;
            }
            index++;
            var builder = new StringBuilder();
            while (index < json.Length)
            {
                var c = json[index++];
                if (c == '"')
                {
                    return builder.ToString();
                }
                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }
                if (index >= json.Length)
                {
                    return null;
                }
                var escape = json[index++];
                if (escape != 'u')
                {
                    builder.Append(Unescape(escape));
                    continue;
                }
                int code;
                if (
                    index + 4 > json.Length
                    || !int.TryParse(
                        json.Substring(index, 4),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out code
                    )
                )
                {
                    return null;
                }
                builder.Append((char)code);
                index += 4;
            }
            return null;
        }

        private static char Unescape(char escape)
        {
            switch (escape)
            {
                case 'n':
                    return '\n';
                case 't':
                    return '\t';
                case 'r':
                    return '\r';
                case 'b':
                    return '\b';
                case 'f':
                    return '\f';
                default:
                    return escape;
            }
        }
    }
}
