using System.Collections.Generic;
namespace Shion.SDK.Editor
{
    public interface IStringListStore
    {
        IReadOnlyCollection<string> GetAll();
        void Add(string id);
        void Remove(string id);
    }
}