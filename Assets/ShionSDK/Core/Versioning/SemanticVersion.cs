using System;
namespace Shion.SDK.Core
{
    public readonly struct SemanticVersion : IComparable<SemanticVersion>
    {
        public readonly int Major;
        public readonly int Minor;
        public readonly int Patch;
        public readonly int Build;
        public SemanticVersion(int major, int minor, int patch, int build = -1)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Build = build;
        }
        public static SemanticVersion Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return default;
            var p = value.Trim().Split('.');
            if (p.Length < 3)
                return default;
            if (!int.TryParse(p[0], out var ma) || !int.TryParse(p[1], out var mi) || !int.TryParse(p[2], out var pa))
                return default;
            var build = p.Length >= 4 && int.TryParse(p[3], out var b) ? b : -1;
            return new SemanticVersion(ma, mi, pa, build);
        }
        public int CompareTo(SemanticVersion other)
        {
            if (Major != other.Major) return Major.CompareTo(other.Major);
            if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
            if (Patch != other.Patch) return Patch.CompareTo(other.Patch);
            return Build.CompareTo(other.Build);
        }
        public override string ToString() =>
            Build >= 0 ? $"{Major}.{Minor}.{Patch}.{Build}" : $"{Major}.{Minor}.{Patch}";
    }
}