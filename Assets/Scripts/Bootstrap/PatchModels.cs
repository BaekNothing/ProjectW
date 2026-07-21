using System;

namespace ProjectW.Bootstrap
{
    [Serializable]
    internal sealed class PatchChannel
    {
        public int schemaVersion;
        public string manifestUrl;
    }

    [Serializable]
    internal sealed class PatchManifest
    {
        public int schemaVersion;
        public string patchVersion;
        public int minBaseVersion;
        public string entryAssembly;
        public string entryType;
        public PatchFile[] files;
    }

    [Serializable]
    internal sealed class PatchFile
    {
        public string name;
        public string role;
        public string url;
        public long size;
        public string sha256;
    }
}
