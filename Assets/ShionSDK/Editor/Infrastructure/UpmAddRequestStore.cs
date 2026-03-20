using System.Collections.Generic;
using Shion.SDK.Core;
using UnityEditor.PackageManager.Requests;
namespace Shion.SDK.Editor
{
    internal static class UpmAddRequestStore
    {
        private static readonly Dictionary<string, AddRequest> Requests = new();
        public static void Register(Module module, AddRequest request)
        {
            if (module == null || request == null)
                return;
            Requests[module.Id.Value] = request;
        }
        public static bool TryGet(ModuleId id, out AddRequest request)
        {
            request = null;
            if (string.IsNullOrEmpty(id.Value))
                return false;
            return Requests.TryGetValue(id.Value, out request);
        }
        public static void Clear(ModuleId id)
        {
            if (string.IsNullOrEmpty(id.Value))
                return;
            Requests.Remove(id.Value);
        }
        public static void ClearAll()
        {
            Requests.Clear();
        }
    }
}