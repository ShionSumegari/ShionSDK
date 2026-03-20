-- UIManager.lua
local UIManager = {}

-- UI list
local uis = {
    {id = 0, name = "Button", addressable = "ButtonGame"}
}

-- Get ui width id 
function UIManager.getByID(id)
    for _, ui in ipairs(uis) do
        if ui.id == id then
            return ui
        end
    end
    return nil
end

return UIManager