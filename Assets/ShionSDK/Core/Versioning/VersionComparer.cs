namespace Shion.SDK.Core
{
    public static class VersionComparer
    {
        public static int Compare(SemanticVersion a, SemanticVersion b)
        {
            if (a.Major != b.Major) return a.Major.CompareTo(b.Major);
            if (a.Minor != b.Minor) return a.Minor.CompareTo(b.Minor);
            if (a.Patch != b.Patch) return a.Patch.CompareTo(b.Patch);
            return a.Build.CompareTo(b.Build);
        }
        public static bool Greater(SemanticVersion a, SemanticVersion b) => Compare(a, b) > 0;
        public static bool Lower(SemanticVersion a, SemanticVersion b) => Compare(a, b) < 0;
        public static bool Equal(SemanticVersion a, SemanticVersion b) => Compare(a, b) == 0;
    }
}