using System;
using System.Diagnostics;
using System.IO;
using Shion.SDK.Core;
using UnityEngine;
namespace Shion.SDK.Editor
{
    public class GitInstaller : IModuleInstaller
    {
        public void Install(Module module)
        {
            try
            {
                if (string.IsNullOrEmpty(module.GitUrl))
                {
                    UnityEngine.Debug.LogError($"[ShionSDK] Invalid Git config: Module '{module.Name}' has empty GitUrl.");
                    return;
                }
                var projectRoot = Directory.GetParent(Application.dataPath).FullName;
                var targetPath = Path.Combine(projectRoot, module.LocalPath);
                if (Directory.Exists(targetPath))
                {
                    UnityEngine.Debug.Log($"[ShionSDK] Module '{module.Name}' already exists at '{targetPath}'. Skipping clone (treated as installed).");
                    return;
                }
                var parentDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                    Directory.CreateDirectory(parentDir);
                UnityEngine.Debug.Log($"[ShionSDK] Start installing module '{module.Name}' via Git. Url = {module.GitUrl}, Target = '{targetPath}'.");
                var psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"clone {module.GitUrl} \"{targetPath}\"",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    WorkingDirectory = projectRoot
                };
                using (var process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        UnityEngine.Debug.LogError("[ShionSDK] Install FAILED: could not start git process.");
                        return;
                    }
                    process.WaitForExit();
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    if (process.ExitCode == 0)
                    {
                        UnityEngine.Debug.Log($"[ShionSDK] Install succeeded for module '{module.Name}' via Git.");
                    }
                    else
                    {
                        UnityEngine.Debug.LogError(
                            $"[ShionSDK] Install FAILED for module '{module.Name}' via Git. ExitCode = {process.ExitCode}. Error = {error}\nOutput = {output}");
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[ShionSDK] Install FAILED for module '{module.Name}' via Git. Exception = {e.Message}");
            }
        }
        public void Uninstall(Module module)
        {
            if (string.IsNullOrEmpty(module.LocalPath)) return;
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var targetPath = Path.Combine(projectRoot, module.LocalPath);
            if (Directory.Exists(targetPath))
                Directory.Delete(targetPath, true);
        }
    }
}