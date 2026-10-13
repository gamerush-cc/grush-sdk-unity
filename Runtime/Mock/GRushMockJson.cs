using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GRushSdk
{
    /// <summary>
    /// モックが params の JSON を読むための小さな読み手。JsonUtility は項目の有無と null を
    /// 区別できず、任意の形の payload も読めないので、最上位の項目を生の JSON のまま取り出す。
    /// あわせて、受信箱と交換所の payload の形をサーバと同じ規則で確かめる。
    /// </summary>
    internal static class GRushMockJson
    {
        public const int MaxDepth = 4;
        public const int MaxScalars = 64;

        /// <summary>最上位のオブジェクトの項目を、値の生の JSON で返す。オブジェクトでなければ null。</summary>
        public static Dictionary<string, string> Members(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }
            var reader = new Reader(json);
            reader.Space();
            if (reader.Peek() != '{')
            {
                return null;
            }
            reader.Position++;
            var members = new Dictionary<string, string>();
            reader.Space();
            if (reader.Peek() == '}')
            {
                reader.Position++;
                return reader.RestIsSpace() ? members : null;
            }
            while (true)
            {
                reader.Space();
                string key;
                if (!reader.ReadString(out key))
                {
                    return null;
                }
                reader.Space();
                if (reader.Peek() != ':')
                {
                    return null;
                }
                reader.Position++;
                reader.Space();
                var start = reader.Position;
                if (!reader.SkipValue())
                {
                    return null;
                }
                members[key] = json.Substring(start, reader.Position - start);
                reader.Space();
                var next = reader.Peek();
                reader.Position++;
                if (next == '}')
                {
                    return reader.RestIsSpace() ? members : null;
                }
                if (next != ',')
                {
                    return null;
                }
            }
        }

        public static bool IsNull(string raw)
        {
            return raw == null || raw == "null";
        }

        public static bool TryString(string raw, out string value)
        {
            value = null;
            if (raw == null)
            {
                return false;
            }
            var reader = new Reader(raw);
            return reader.ReadString(out value) && reader.RestIsSpace();
        }

        public static bool TryNumber(string raw, out double value)
        {
            value = 0;
            if (raw == null || raw.Length == 0 || raw[0] == '"' || raw == "null")
            {
                return false;
            }
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !double.IsNaN(value)
                && !double.IsInfinity(value);
        }

        public static bool TryInt(string raw, out int value)
        {
            value = 0;
            double number;
            if (!TryNumber(raw, out number) || number != Math.Floor(number))
            {
                return false;
            }
            if (number < int.MinValue || number > int.MaxValue)
            {
                return false;
            }
            value = (int)number;
            return true;
        }

        public static bool IsToken(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 32)
            {
                return false;
            }
            foreach (var c in value)
            {
                var ok =
                    (c >= 'A' && c <= 'Z')
                    || (c >= 'a' && c <= 'z')
                    || (c >= '0' && c <= '9')
                    || c == '_'
                    || c == '.'
                    || c == ':'
                    || c == '-';
                if (!ok)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// GameRush と同じ規則で payload を確かめる。通るなら null、弾くならエラーの文言。
        /// オブジェクトであること、文字列の値とキーが識別子であること、数値が有限であること、
        /// 入れ子が 4 段まで、値が合計 64 個まで、詰めた JSON が <paramref name="maxBytes"/> まで。
        /// </summary>
        public static string PayloadError(string json, int maxBytes, string label)
        {
            if (string.IsNullOrEmpty(json))
            {
                return label + " must be a JSON object.";
            }
            var validator = new Validator(json, label);
            validator.Reader.Space();
            if (validator.Reader.Peek() != '{')
            {
                return label + " must be a JSON object.";
            }
            if (!validator.Value(1))
            {
                return validator.Error;
            }
            if (!validator.Reader.RestIsSpace())
            {
                return label + " must be JSON.";
            }
            if (validator.Bytes > maxBytes)
            {
                return label + " must be " + maxBytes + " bytes or smaller.";
            }
            return null;
        }

        private sealed class Validator
        {
            public readonly Reader Reader;
            public long Bytes;
            public string Error;
            private readonly string label;
            private int scalars;

            public Validator(string json, string label)
            {
                Reader = new Reader(json);
                this.label = label;
            }

            private bool Fail(string message)
            {
                Error = label + " " + message;
                return false;
            }

            // depth は value を入れている入れ物の段。最上位のオブジェクトが 1 段目。上限より深い入れ物は
            // 降りずに弾くので、再帰は MaxDepth を超えない。
            public bool Value(int depth)
            {
                Reader.Space();
                var c = Reader.Peek();
                if (c == '{' || c == '[')
                {
                    if (depth > MaxDepth)
                    {
                        return Fail("must not nest deeper than " + MaxDepth + " levels.");
                    }
                    return c == '{' ? ObjectValue(depth) : ArrayValue(depth);
                }
                scalars++;
                if (scalars > MaxScalars)
                {
                    return Fail("must hold at most " + MaxScalars + " values.");
                }
                if (c == '"')
                {
                    string text;
                    if (!Reader.ReadString(out text))
                    {
                        return Fail("must be JSON.");
                    }
                    if (!IsToken(text))
                    {
                        return Fail("strings must be 1-32 letters, digits, or _ . : - (no free text).");
                    }
                    Bytes += text.Length + 2;
                    return true;
                }
                foreach (var word in new[] { "true", "false", "null" })
                {
                    if (Reader.TakeWord(word))
                    {
                        Bytes += word.Length;
                        return true;
                    }
                }
                var literal = Reader.ReadNumberLiteral();
                double number;
                if (
                    literal == null
                    || !double.TryParse(
                        literal,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out number
                    )
                )
                {
                    return Fail("must be JSON.");
                }
                if (double.IsInfinity(number) || double.IsNaN(number))
                {
                    return Fail("numbers must be finite.");
                }
                Bytes += GRushMockPlayerStatePayload.NumberLength(literal);
                return true;
            }

            private bool ObjectValue(int depth)
            {
                Reader.Position++;
                Bytes += 2;
                Reader.Space();
                if (Reader.Peek() == '}')
                {
                    Reader.Position++;
                    return true;
                }
                while (true)
                {
                    Reader.Space();
                    string key;
                    if (!Reader.ReadString(out key))
                    {
                        return Fail("must be JSON.");
                    }
                    if (!IsToken(key))
                    {
                        return Fail("keys must be 1-32 letters, digits, or _ . : -");
                    }
                    Bytes += key.Length + 3;
                    Reader.Space();
                    if (Reader.Peek() != ':')
                    {
                        return Fail("must be JSON.");
                    }
                    Reader.Position++;
                    if (!Value(depth + 1))
                    {
                        return false;
                    }
                    Reader.Space();
                    var next = Reader.Peek();
                    Reader.Position++;
                    if (next == '}')
                    {
                        return true;
                    }
                    if (next != ',')
                    {
                        return Fail("must be JSON.");
                    }
                    Bytes += 1;
                }
            }

            private bool ArrayValue(int depth)
            {
                Reader.Position++;
                Bytes += 2;
                Reader.Space();
                if (Reader.Peek() == ']')
                {
                    Reader.Position++;
                    return true;
                }
                while (true)
                {
                    if (!Value(depth + 1))
                    {
                        return false;
                    }
                    Reader.Space();
                    var next = Reader.Peek();
                    Reader.Position++;
                    if (next == ']')
                    {
                        return true;
                    }
                    if (next != ',')
                    {
                        return Fail("must be JSON.");
                    }
                    Bytes += 1;
                }
            }
        }

        private sealed class Reader
        {
            private readonly string text;
            public int Position;

            public Reader(string text)
            {
                this.text = text;
            }

            public char Peek()
            {
                return Position < text.Length ? text[Position] : '\0';
            }

            public void Space()
            {
                while (Position < text.Length && " \t\r\n".IndexOf(text[Position]) >= 0)
                {
                    Position++;
                }
            }

            public bool RestIsSpace()
            {
                Space();
                return Position >= text.Length;
            }

            public bool TakeWord(string word)
            {
                if (string.CompareOrdinal(text, Position, word, 0, word.Length) != 0)
                {
                    return false;
                }
                Position += word.Length;
                return true;
            }

            public string ReadNumberLiteral()
            {
                var start = Position;
                while (Position < text.Length && "+-.eE0123456789".IndexOf(text[Position]) >= 0)
                {
                    Position++;
                }
                return Position > start ? text.Substring(start, Position - start) : null;
            }

            public bool ReadString(out string value)
            {
                value = null;
                if (Peek() != '"')
                {
                    return false;
                }
                Position++;
                var builder = new StringBuilder();
                while (Position < text.Length)
                {
                    var c = text[Position++];
                    if (c == '"')
                    {
                        value = builder.ToString();
                        return true;
                    }
                    if (c < ' ')
                    {
                        return false;
                    }
                    if (c != '\\')
                    {
                        builder.Append(c);
                        continue;
                    }
                    if (Position >= text.Length)
                    {
                        return false;
                    }
                    var escape = text[Position++];
                    switch (escape)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            builder.Append(escape);
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'u':
                            int code;
                            if (
                                Position + 4 > text.Length
                                || !int.TryParse(
                                    text.Substring(Position, 4),
                                    NumberStyles.HexNumber,
                                    CultureInfo.InvariantCulture,
                                    out code
                                )
                            )
                            {
                                return false;
                            }
                            builder.Append((char)code);
                            Position += 4;
                            break;
                        default:
                            return false;
                    }
                }
                return false;
            }

            // 値を1つ読み飛ばす。入れ子の中は文字列だけを正しく飛ばし、形は確かめない。
            public bool SkipValue()
            {
                var c = Peek();
                if (c == '"')
                {
                    string ignored;
                    return ReadString(out ignored);
                }
                if (c == '{' || c == '[')
                {
                    var depth = 0;
                    while (Position < text.Length)
                    {
                        var ch = text[Position];
                        if (ch == '"')
                        {
                            string ignored;
                            if (!ReadString(out ignored))
                            {
                                return false;
                            }
                            continue;
                        }
                        if (ch == '{' || ch == '[')
                        {
                            depth++;
                        }
                        else if (ch == '}' || ch == ']')
                        {
                            depth--;
                            if (depth == 0)
                            {
                                Position++;
                                return true;
                            }
                        }
                        Position++;
                    }
                    return false;
                }
                var start = Position;
                while (Position < text.Length && ",}] \t\r\n".IndexOf(text[Position]) < 0)
                {
                    Position++;
                }
                return Position > start;
            }
        }
    }
}
