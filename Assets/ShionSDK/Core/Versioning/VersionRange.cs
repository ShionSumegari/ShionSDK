namespace Shion.SDK.Core
{
    public class VersionRange
    {
        public SemanticVersion Min;
        public SemanticVersion Max;
        public bool HasMin;
        public bool HasMax;
        public VersionRange(string range)
        {
            if(string.IsNullOrEmpty(range))
                return;
            if (range.StartsWith(">="))
            {
                HasMin = true;
                Min = SemanticVersion.Parse(range.Substring(2));
            }
            else if (range.StartsWith("<="))
            {
                HasMax = true;
                Max = SemanticVersion.Parse(range.Substring(2));
            }
            else
            {
                HasMin = true;
                HasMax = true;
                Min = SemanticVersion.Parse(range);
                Max = SemanticVersion.Parse(range);
            }
        }
        public bool IsSatisfiedBy(SemanticVersion version)
        {
            if (HasMin && VersionComparer.Lower(version, Min))
                return false;
            if(HasMax && VersionComparer.Greater(version, Max))
                return false;
            return true;
        }
    }
}