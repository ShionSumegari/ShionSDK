using UnityEngine;
namespace Shion.SDK.Editor
{
    public static class VersionComparisonService
    {
        public static int Compare(string a, string b)
        {
            var va = Normalize(a ?? "");
            var vb = Normalize(b ?? "");
            if (string.IsNullOrEmpty(va) && string.IsNullOrEmpty(vb)) return 0;
            if (string.IsNullOrEmpty(va)) return -1;
            if (string.IsNullOrEmpty(vb)) return 1;
            var pa = va.Split('.');
            var pb = vb.Split('.');
            var max = Mathf.Max(pa.Length, pb.Length);
            for (var i = 0; i < max; i++)
            {
                var na = i < pa.Length && int.TryParse(pa[i], out var ia) ? ia : 0;
                var nb = i < pb.Length && int.TryParse(pb[i], out var ib) ? ib : 0;
                if (na != nb) return na.CompareTo(nb);
            }
            return 0;
        }
        public static string Normalize(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return "";
            return version.Trim().TrimStart('v', 'V').Trim('[', ']');
        }
        public static string NormalizeSupportVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return null;
            var v = version.Trim();
            if (v == "-" || v == "...") return null;
            return v.TrimStart('v', 'V').Trim('[', ']');
        }
        public static bool IsGreater(string a, string b) => Compare(a, b) > 0;
        public static bool IsLower(string a, string b) => Compare(a, b) < 0;
        public static bool IsEqual(string a, string b) => Compare(a, b) == 0;
        public static bool IsUnderOrEqual(string candidate, string reference)
        {
            if (string.IsNullOrEmpty(reference)) return true;
            if (string.IsNullOrEmpty(candidate)) return false;
            return Compare(candidate, reference) <= 0;
        }
        public static int VersionDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0;
            var na = NormalizeSupportVersion(a) ?? "";
            var nb = NormalizeSupportVersion(b) ?? "";
            if (string.IsNullOrEmpty(na) || string.IsNullOrEmpty(nb)) return 0;
            var pa = na.Split('.');
            var pb = nb.Split('.');
            var max = Mathf.Max(pa.Length, pb.Length);
            var distance = 0;
            var weight = 1000;
            for (var i = 0; i < max; i++)
            {
                var va = i < pa.Length && int.TryParse(pa[i], out var ia) ? ia : 0;
                var vb = i < pb.Length && int.TryParse(pb[i], out var ib) ? ib : 0;
                distance += Mathf.Abs(va - vb) * Mathf.Max(1, weight);
                weight /= 10;
            }
            return distance;
        }
        public static void SortDescending(System.Collections.Generic.List<string> versions)
        {
            if (versions == null || versions.Count == 0) return;
            versions.Sort((a, b) => -Compare(a, b));
        }
    }
}