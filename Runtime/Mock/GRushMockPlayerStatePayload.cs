using System.Collections.Generic;
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
        /// <paramref name="json"/> が1つの JSON オブジェクトなら、サーバが payload を
        /// JSON.stringify し直したときの UTF-8 のバイト数を見積もって返す。作者が整形した
        /// JSON の空白は数えず、数値は JavaScript の数値の文字列化と同じ書き方に直して数える。
        /// 文字列のエスケープは書いたまま数えるため、<c>\u3042</c> のような書き方では
        /// サーバより多く数える（多く数える側の差は、実サーバで通る payload をモックで弾くだけ）。
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
            if (scanner.Peek() != '{' || !scanner.Document())
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

        /// <summary>
        /// 数値の字句（構文は確かめ済み）を、JavaScript の Number::toString が書く文字数に直す。
        /// 有効数字が 15 桁までなら倍精度で正確に表せて、最短の書き方は字句の有効数字そのものになる。
        /// 16 桁以上は丸めで桁数も指数も変わりうるので、17 桁で、指数が繰り上がった場合も含めた
        /// 長いほうを返す（多く数える側に倒す）。
        /// </summary>
        internal static int NumberLength(string literal)
        {
            var index = 0;
            var negative = literal[0] == '-';
            if (negative)
            {
                index++;
            }
            var digits = new StringBuilder();
            var point = 0;
            while (index < literal.Length && literal[index] >= '0' && literal[index] <= '9')
            {
                digits.Append(literal[index]);
                point++;
                index++;
            }
            if (index < literal.Length && literal[index] == '.')
            {
                index++;
                while (index < literal.Length && literal[index] >= '0' && literal[index] <= '9')
                {
                    digits.Append(literal[index]);
                    index++;
                }
            }
            long exponent = 0;
            if (index < literal.Length && (literal[index] == 'e' || literal[index] == 'E'))
            {
                index++;
                var exponentNegative = false;
                if (literal[index] == '+' || literal[index] == '-')
                {
                    exponentNegative = literal[index] == '-';
                    index++;
                }
                while (index < literal.Length)
                {
                    // 倍精度の範囲を大きく外れた指数は、どこで切っても桁数の見積もりに足りる。
                    if (exponent < 1000000)
                    {
                        exponent = exponent * 10 + (literal[index] - '0');
                    }
                    index++;
                }
                if (exponentNegative)
                {
                    exponent = -exponent;
                }
            }

            var text = digits.ToString();
            var first = 0;
            while (first < text.Length && text[first] == '0')
            {
                first++;
            }
            if (first == text.Length)
            {
                // 0 と -0 はどちらも "0" になる。
                return 1;
            }
            var last = text.Length;
            while (text[last - 1] == '0')
            {
                last--;
            }
            var k = last - first;
            // 値は 0.d1d2…dk × 10^n。
            var n = point - first + exponent;
            var length = k <= 15
                ? FormattedLength(k, n)
                : System.Math.Max(FormattedLength(17, n), FormattedLength(17, n + 1));
            return (negative ? 1 : 0) + length;
        }

        // ECMAScript の Number::toString(10) の書き方で、有効数字 k 桁・値 0.d1…dk × 10^n を書いた文字数。
        private static int FormattedLength(int k, long n)
        {
            if (k <= n && n <= 21)
            {
                return (int)n;
            }
            if (0 < n && n <= 21)
            {
                return k + 1;
            }
            if (-6 < n && n <= 0)
            {
                return 2 + (int)-n + k;
            }
            var e = System.Math.Abs(n - 1);
            return (k == 1 ? 1 : k + 1) + 2 + e.ToString().Length;
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

            // 入れ子は再帰ではなく閉じ括弧の積み上げで追う。サーバは深さを制限しないので
            // 深さでは弾かず、深い入れ子でも再帰がスタックを使い切って Editor ごと落ちない。
            public bool Document()
            {
                var closers = new List<char>();
                while (true)
                {
                    SkipWhitespace();
                    var c = Peek();
                    if (c == '{' || c == '[')
                    {
                        var close = c == '{' ? '}' : ']';
                        Take(1);
                        SkipWhitespace();
                        if (Peek() == close)
                        {
                            Take(1);
                        }
                        else
                        {
                            closers.Add(close);
                            if (close == '}' && !Key())
                            {
                                return false;
                            }
                            continue;
                        }
                    }
                    else if (!Scalar(c))
                    {
                        return false;
                    }

                    // 値を1つ読み終えた。閉じられる入れ子を閉じ、次の要素へ進む。
                    while (true)
                    {
                        if (closers.Count == 0)
                        {
                            return true;
                        }
                        SkipWhitespace();
                        var top = closers[closers.Count - 1];
                        var next = Peek();
                        if (next == top)
                        {
                            Take(1);
                            closers.RemoveAt(closers.Count - 1);
                            continue;
                        }
                        if (next != ',')
                        {
                            return false;
                        }
                        Take(1);
                        if (top == '}' && !Key())
                        {
                            return false;
                        }
                        break;
                    }
                }
            }

            private bool Key()
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
                return true;
            }

            private bool Scalar(char c)
            {
                switch (c)
                {
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
                Bytes += NumberLength(text.Substring(start, position - start));
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
