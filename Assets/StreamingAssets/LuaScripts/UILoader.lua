-- Addressable bridge
local AddressableBridge = CS.AddressableBridge
-- UIManager
local UIManager = require("UIManager")
-- UILoader
local UILoader = {}

function UILoader.loadById(id, parent)
    local ui = UIManager.getByID(id)
    if ui then
        AddressableBridge.LoadPrefab(ui.addressable, function(obj)
            if obj then
                local go = CS.UnityEngine.Object.Instantiate(obj, parent)
                go.name = ui.name
                print("Loaded ui ", ui.name)
            else
                print("Cant load ui ", ui.name)
            end
        end)
    else
        print("Cant find ui by id", id)
    end
end

return UILoader
