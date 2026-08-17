using System.Collections.Generic;

namespace GRushSdk.Editor
{
    internal static class GRushEditorUploadTickets
    {
        public static List<GRushUploadTicket> From(GRushJson uploads, GRushBuildManifest manifest)
        {
            var byPath = new Dictionary<string, GRushBuildFile>();
            foreach (var file in manifest.Files)
            {
                byPath[file.Path] = file;
            }
            var tickets = new List<GRushUploadTicket>();
            foreach (var upload in uploads.Items)
            {
                var path = upload.Get("path").AsString(null);
                GRushBuildFile file;
                if (path == null || !byPath.TryGetValue(path, out file))
                {
                    continue;
                }
                tickets.Add(
                    new GRushUploadTicket
                    {
                        Path = path,
                        Url = upload.Get("url").AsString(null),
                        Headers = upload.Get("headers").AsStringMap(),
                        File = file,
                    }
                );
            }
            return tickets;
        }

        /// <summary>
        /// <c>complete</c> が 409 で返す <c>details.missing</c> は届かなかった
        /// パスの一覧。そこだけ送り直せば済むので、全件やり直させない。
        /// </summary>
        public static int Requeue(GRushUploadState state, GRushHttpResult result)
        {
            var missing =
                result.Json == null ? null : result.Json.Get("details").Get("missing").Items;
            if (missing == null || missing.Count == 0)
            {
                return 0;
            }
            var paths = new HashSet<string>();
            foreach (var entry in missing)
            {
                var path = entry.AsString(null);
                if (path != null)
                {
                    paths.Add(path);
                }
            }
            state.Pending.Clear();
            foreach (var ticket in state.Issued)
            {
                if (paths.Contains(ticket.Path))
                {
                    state.Pending.Add(ticket);
                    state.UploadedBytes -= ticket.File.Size;
                }
            }
            if (state.UploadedBytes < 0)
            {
                state.UploadedBytes = 0;
            }
            return state.Pending.Count;
        }
    }
}
