using Shion.SDK.Core;
using UnityEditor.PackageManager;
using UnityEngine;
namespace Shion.SDK.Editor
{
    public class UpmInstaller : IModuleInstaller
    {
        public void Install(Module module)
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(module.UpmId))
            {
                Debug.LogError($"[ShionSDK] Invalid UPM config: Module '{module.Name}' has no UpmId, cannot install via Package Manager.");
                return;
            }
            var source = string.IsNullOrEmpty(module.UpmSource) ? module.UpmId : module.UpmSource;
            if (!string.IsNullOrEmpty(module.UpmPath) && !source.Contains("?path="))
            {
                var p = module.UpmPath.Trim();
                if (!string.IsNullOrEmpty(p) && !p.StartsWith("/"))
                    p = "/" + p;
                if (!string.IsNullOrEmpty(p))
                    source = source + "?path=" + p;
            }
            if (ModuleVersionSelectionStore.TryGet(module.Id, out var selectedVersion) &&
                !string.IsNullOrEmpty(selectedVersion) &&
                !source.Contains("#"))
            {
                source = source + "#" + selectedVersion;
            }
            if (string.IsNullOrEmpty(source))
            {
                Debug.LogError($"[ShionSDK] Invalid UPM config: Module '{module.Name}' has empty UpmId/UpmSource.");
                return;
            }
            Debug.Log($"[ShionSDK] Start installing module '{module.Name}' via UPM. Source = '{source}'.");
            var request = Client.Add(source);
            UpmAddRequestStore.Register(module, request);
#else
            Debug.LogWarning("[ShionSDK] UPM install is only supported inside the Unity Editor.");
#endif
        }
        public void Uninstall(Module module)
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(module.UpmId))
                return;
            Debug.Log($"[ShionSDK] Start uninstalling module '{module.Name}' via UPM. Package = '{module.UpmId}'.");
            Client.Remove(module.UpmId);
#else
            Debug.LogWarning("[ShionSDK] UPM uninstall is only supported inside the Unity Editor.");
#endif
        }
    }
}