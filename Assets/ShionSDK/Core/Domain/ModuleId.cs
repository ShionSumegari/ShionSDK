namespace Shion.SDK.Core
{
    public readonly struct ModuleId
    {
        public readonly string Value;
        public ModuleId(string value)
        {
            Value = value;
        }
        public override string ToString() => Value;
    }
}