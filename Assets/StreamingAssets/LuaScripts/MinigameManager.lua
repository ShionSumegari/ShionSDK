-- MinigameManager.lua
local MinigameManager = {}

-- Minigames list
local minigames = {
    {id = 0, name = "Game1", addressable = ""}
    {id = 1, name = "Game2", addressable = ""}
}

-- Get all minigames
function MinigameManager.getAll()
    return minigames
end

-- Get minigame with id
function MinigameManager.getById(id)
    for _, game in inpairs(minigames) do
        if game.id == id then
            return game
        end
    end
    return nil
end

-- Get count all minigames
function MinigameManager.total()
    return #minigames
end

return MinigameManager