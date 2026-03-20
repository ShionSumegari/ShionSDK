using UnityEngine;
using System;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class AddressableBridge
{
    public static void Load(string address, Action<UnityEngine.Object> onComplete)
    {
        Addressables.LoadAssetAsync<UnityEngine.Object>(address).Completed += handle =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                var asset = handle.Result;
                onComplete?.Invoke(asset);
            }
            else
            {
                Debug.LogError($"Failed to load addressable: {address}");
                onComplete?.Invoke(null);
            }
        };
    }

    public static void LoadPrefab(string address, Action<UnityEngine.Object> onComplete){
        var obj = Resources.Load(address);
        if(obj){
            onComplete?.Invoke(obj);
        }else{
            Debug.LogError($"Failed to load addressable: {address}");
                onComplete?.Invoke(null);
        }
    }

}
