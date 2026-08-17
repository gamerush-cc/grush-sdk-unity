using System.Collections.Generic;
using UnityEngine;

namespace GRushSdk.Editor
{
    internal sealed class GRushUploadTicket
    {
        public string Path;
        public string Url;
        public Dictionary<string, string> Headers;
        public GRushBuildFile File;
    }

    /// <summary>
    /// アップロードの途中経過。**送り終えたチケットは
    /// <see cref="Pending"/> から消えるので、失敗した後は残りだけを送り直せる。**
    /// presigned URL の有効期限は1時間なので、それを超えたら作り直しになる。
    /// </summary>
    internal sealed class GRushUploadState
    {
        public bool Running;
        public bool Cancelled;
        public bool Done;
        public string Phase = "";
        public string Error;
        public string BuildId;
        public string EntryUrl;
        public long TotalBytes;
        public long UploadedBytes;
        public float FileProgress;
        public string CurrentPath = "";
        public List<GRushUploadTicket> Pending = new List<GRushUploadTicket>();
        public List<GRushUploadTicket> Issued = new List<GRushUploadTicket>();
        public bool NeedsComplete;

        public float Progress =>
            TotalBytes <= 0 ? 0f : Mathf.Clamp01((float)UploadedBytes / TotalBytes);

        public bool CanResume => NeedsComplete && !Running;

        public void Reset()
        {
            Running = false;
            Cancelled = false;
            Done = false;
            Phase = "";
            Error = null;
            BuildId = null;
            EntryUrl = null;
            TotalBytes = 0;
            UploadedBytes = 0;
            FileProgress = 0f;
            CurrentPath = "";
            Pending.Clear();
            Issued.Clear();
            NeedsComplete = false;
        }
    }
}
