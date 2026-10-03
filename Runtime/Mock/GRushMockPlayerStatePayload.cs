using System.Text;

namespace GRushSdk
{
    /// <summary>
    /// モックの <c>playerState.setMine</c> が payload を検査する。サーバと同じく、
    /// JSON オブジェクトであることと、空白を除いた JSON の UTF-8 のバイト数が 4KB 以下で
    /// あることを求める。
    /// </summary>
    internal static class GRushMockPlayerStatePayload
    {
        public const int MaxBytes = 4 * 1024;

        public const string NotObjectMessage = "Player state payload must be a JSON object.";
        public const string TooLargeMessage = "Player state payload is too large.";

        // 深い入れ子で再帰がスタックを使い切ると Editor ごと落ちるので、手前で JSON ではないものとして弾く。
        private const int MaxDepth = 512;

        /// <summary>通るなら null、弾くならエラーの文言を返す。</summary>
        public static string Validate(string payloadJson)
        {
            long bytes;
            if (!TryMeasureObject(payloadJson, out bytes))
            {
                return NotObjectMessage;
            }
            return bytes > MaxBytes ? TooLargeMessage : null;
        }

        /// <summary>
        /// <paramref name="json"/> が1つの JSON オブジェクトなら、値の外の空白を除いた
        /// UTF-8 のバイト数を返す。サーバは payload を JSON.stringify し直して数えるので、
        /// 作者が整形した JSON の空白は数えない。文字列のエスケープと数値の書き方は
        /// そのまま数えるため、<c>あ</c> のような書き方ではサーバより多く数える。
        /// </summary>
        public static bool TryMeasureObject(string json, out long bytes)
        {
            bytes = 0;
            if (json == null)
            {
                return false;
            }
            var scanner = new Scanner(json);
            scanner.SkipWhitespace();
            if (scanner.Peek() != '{' || !scanner.Value(0))
            {
                return false;
            }
            scanner.SkipWhitespace();
            if (!scanner.AtEnd)
            {
                return false;
            }
            bytes = scanner.Bytes;
            return true;
        }

        private sealed class Scanner
        {
            private readonly string text;
            private int position;

            public long Bytes;

            public Scanner(string text)
            {
                this.text = text;
            }

            public bool AtEnd
            {
                get { return position >= text.Length; }
            }

            public char Peek()
            {
                return AtEnd ? '\0' : text[position];
            }

            public void SkipWhitespace()
            {
                while (!AtEnd)
                {
                    var c = text[position];
                    if (c != ' ' && c != '\t' && c != '\n' && c != '\r')
                    {
                        return;
                    }
                    position++;
                }
            }

            public bool Value(int depth)
            {
                if (depth > MaxDepth)
                {
                    return false;
                }
                SkipWhitespace();
                switch (Peek())
                {
                    case '{':
                        return Container('}', depth, true);
                    case '[':
                        return Container(']', depth, false);
                    case '"':
                        return String();
                    case 't':
                        return Literal("true");
                    case 'f':
                        return Literal("false");
                    case 'n':
                        return Literal("null");
                    default:
                        return Number();
                }
            }

            private bool Container(char close, int depth, bool isObject)
            {
                Take(1);
                SkipWhitespace();
                if (Peek() == close)
                {
                    Take(1);
                    return true;
                }
                while (true)
                {
                    if (isObject)
                    {
                        SkipWhitespace();
                        if (Peek() != '"' || !String())
                        {
                            return false;
                        }
                        SkipWhitespace();
                        if (Peek() != ':')
                        {
                            return false;
                        }
                        Take(1);
                    }
                    if (!Value(depth + 1))
                    {
                        return false;
                    }
                    SkipWhitespace();
                    var next = Peek();
                    if (next == close)
                    {
                        Take(1);
                        return true;
                    }
                    if (next != ',')
                    {
                        return false;
                    }
                    Take(1);
                }
            }

            private bool String()
            {
                var start = position;
                position++;
                while (!AtEnd)
                {
                    var c = text[position];
                    if (c == '"')
                    {
                        position++;
                        Bytes += Encoding.UTF8.GetByteCount(text.Substring(start, position - start));
                        return true;
                    }
                    if (c < 0x20)
                    {
                        return false;
                    }
                    if (c == '\\')
                    {
                        if (!Escape())
                        {
                            return false;
                        }
                        continue;
                    }
                    position++;
                }
                return false;
            }

            private bool Escape()
            {
                position++;
                if (AtEnd)
                {
                    return false;
                }
                var c = text[position];
                position++;
                if ("\"\\/bfnrt".IndexOf(c) >= 0)
                {
                    return true;
                }
                if (c != 'u' || position + 4 > text.Length)
                {
                    return false;
                }
                for (var i = 0; i < 4; i++)
                {
                    if (!IsHex(text[position + i]))
                    {
                        return false;
                    }
                }
                position += 4;
                return true;
            }

            private bool Literal(string word)
            {
                if (string.CompareOrdinal(text, position, word, 0, word.Length) != 0)
                {
                    return false;
                }
                Take(word.Length);
                return true;
            }

            private bool Number()
            {
                var start = position;
                if (Peek() == '-')
                {
                    position++;
                }
                if (Peek() == '0')
                {
                    position++;
                }
                else if (!Digits())
                {
                    return false;
                }
                if (Peek() == '.')
                {
                    position++;
                    if (!Digits())
                    {
                        return false;
                    }
                }
                if (Peek() == 'e' || Peek() == 'E')
                {
                    position++;
                    if (Peek() == '+' || Peek() == '-')
                    {
                        position++;
                    }
                    if (!Digits())
                    {
                        return false;
                    }
                }
                Bytes += position - start;
                return true;
            }

            private bool Digits()
            {
                var start = position;
                while (!AtEnd && text[position] >= '0' && text[position] <= '9')
                {
                    position++;
                }
                return position > start;
            }

            private void Take(int length)
            {
                position += length;
                Bytes += length;
            }

            private static bool IsHex(char c)
            {
                return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            }
        }
    }
}
