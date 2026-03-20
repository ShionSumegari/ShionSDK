using UnityEngine;
using System.Collections.Generic;

public class UILoader : MonoBehaviour
{
    public static UILoader Instance {get; private set;}

    public Transform parentTransform;
    
    [System.Serializable]
    private class UIItem
    {
        public int id;
        public string name;
        public string addressable;
    }

    // Equivalent data migrated from UIManager.lua
    [SerializeField] private List<UIItem> uis = new List<UIItem>
    {
        new UIItem { id = 0, name = "Button", addressable = "ButtonGame" }
    };

    private void Awake()
    {
        if(Instance == null){
            Instance = this;
        }
        else{
            Destroy(this.gameObject);
        }
    }

    private void Start()
    {
        LoadById(0, parentTransform);
    }

    private void LoadById(int id, Transform parent)
    {
        var ui = uis.Find(x => x.id == id);
        if (ui == null)
        {
            Debug.LogWarning($"Cant find ui by id {id}");
            return;
        }

        AddressableBridge.LoadPrefab(ui.addressable, obj =>
        {
            if (obj == null)
            {
                Debug.LogWarning($"Cant load ui {ui.name}");
                return;
            }

            var go = Instantiate(obj, parent);
            go.name = ui.name;
            Debug.Log($"Loaded ui {ui.name}");
        });
    }

    void OnDestroy()
    {
    }
}
