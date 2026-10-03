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
    }
}
