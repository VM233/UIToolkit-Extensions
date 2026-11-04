using System;
using System.IO;

namespace VM233.UIElements
{
    public readonly struct UiTransitionSite
    {
        public string File { get; }
        public string Member { get; }
        public int Line { get; }
        public string TargetExpression { get; }
        public string Identity => File + ":" + Line + "/" + TargetExpression;

        public UiTransitionSite(string file, string member, int line, string targetExpression)
        {
            string normalized = file.Replace('\\', '/');
            int root = normalized.IndexOf("/Assets/", StringComparison.Ordinal);
            File = root >= 0 ? normalized.Substring(root + 1) : Path.GetFileName(normalized);
            Member = member;
            Line = line;
            TargetExpression = targetExpression;
        }
    }
}
