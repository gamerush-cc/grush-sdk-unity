using System.Collections.Generic;
using System.Text;

namespace GRushSdk
{
    /// <summary>
    /// エディタ用の「他プレイヤーの公開状態」。実サーバへ繋がずに、他人の進捗を
    /// 読む側のコードを試せるようにする。
    ///
    /// hidden の扱いも再現する。**運営が伏せた状態は get から返らない**ので、
    /// ゲームが「要求した数だけ返る」前提で書いていないかを確かめられる唯一の場所。
    /// </summary>
    public static class GRushMockPlayerStates
    {
        private sealed class Entry
        {
            public string PayloadJson;
            public bool Hidden;
            public int Revision;
        }

        private static readonly Dictionary<string, Entry> states = new Dictionary<string, Entry>();

        public static void Define(string pseudoId, string payloadJson)
        {
            Define(pseudoId, payloadJson, false);
        }

        public static void Define(string pseudoId, string payloadJson, bool hidden)
        {
            states[pseudoId] = new Entry
            {
                PayloadJson = payloadJson,
                Hidden = hidden,
                Revision = 1,
            };
        }

        public static void Reset()
        {
            states.Clear();
        }

        internal static string GetJson(string paramsJson)
        {
            var wire = GRushWire.Parse<GRushPlayerStateQueryWire>(paramsJson);
            var builder = new StringBuilder("{\"states\":[");
            var first = true;
            if (wire != null && wire.pseudoIds != null)
            {
                foreach (var pseudoId in wire.pseudoIds)
                {
                    Entry entry;
                    if (pseudoId == null || !states.TryGetValue(pseudoId, out entry))
                    {
                        continue;
                    }
                    // hidden は返さない（実サーバと同じ）。
                    if (entry.Hidden)
                    {
                        continue;
                    }
                    if (!first)
                    {
                        builder.Append(',');
                    }
                    first = false;
                    builder.Append('{');
                    builder.Append("\"pseudoId\":").Append(GRushWire.Escape(pseudoId));
                    builder.Append(",\"revision\":").Append(entry.Revision);
                    builder.Append(",\"updatedAt\":\"\"");
                    builder.Append(",\"payload\":").Append(entry.PayloadJson ?? "null");
                    builder.Append('}');
                }
            }
            return builder.Append("]}").ToString();
        }
    }
}
