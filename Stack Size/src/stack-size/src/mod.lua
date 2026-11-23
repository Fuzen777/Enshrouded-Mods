-- Stack Size: raises the max stack size of every stackable item (keen::ItemInfo.maxStackSize above 1).
-- Items that already stack to the "Large Stack Threshold" or more in the game (building blocks ...) go to the
-- "Large Stack Size" setting, every other stackable item to the "Max Stack Size" setting. Items that stack to 1
-- (weapons, armour ...) stay single, and items that already stack as high as their new size or higher keep
-- their size.
-- Runs in Shroudforge's startup asset pass, so a changed setting applies on the next game start.
-- One pass over the items only: Auto Loot Plus's many passes over every template ran Shroudforge out
-- of Lua references and made the whole asset pass fail.

local TAG = "[Stack Size]"
local DEFAULT_MAX_STACK_SIZE = 9999
local DEFAULT_LARGE_STACK_SIZE = 65535
local DEFAULT_LARGE_STACK_THRESHOLD = 1000
local HIGHEST_STACK_SIZE = 65535 -- maxStackSize is a 16-bit value, so the game can't hold more
local NAMES_PER_LOG_LINE = 20

local function log(message)
    if shroudforge and shroudforge.log and shroudforge.log.info then
        shroudforge.log.info(message)
    else
        print(TAG .. " " .. message)
    end
end

local function setting(key, fallback)
    if shroudforge and shroudforge.settings and shroudforge.settings.get then
        return shroudforge.settings.get(key, fallback)
    end
    return fallback
end

-- A stack size setting as a whole number from 2 to 65535 (the fallback when it isn't a number).
local function stackSizeSetting(key, fallback)
    local requestedSize = tonumber(setting(key, fallback)) or fallback
    return math.floor(math.max(2, math.min(HIGHEST_STACK_SIZE, requestedSize)))
end

if not loader.features.patch then
    error(TAG .. " needs the asset patch capability (loader.features.patch)")
end

local maxStackSize = stackSizeSetting("maxStackSize", DEFAULT_MAX_STACK_SIZE)
local largeStackSize = stackSizeSetting("largeStackSize", DEFAULT_LARGE_STACK_SIZE)
local largeStackThreshold = stackSizeSetting("largeStackThreshold", DEFAULT_LARGE_STACK_THRESHOLD)

local items = game.assets.get_resources_by_type("keen::ItemInfo") or {}
local raisedCount, largeRaisedCount, singleCount, alreadyHighCount, failedCount = 0, 0, 0, 0, 0
local firstFailure
local largeNamesByGameSize = {} -- the game's own stack size -> names of the large items raised from it

for _, resource in ipairs(items) do
    -- One odd item must not fail the whole asset pass (that would roll back every asset mod).
    local ok, result, gameSize, itemName = pcall(function()
        local data = resource.data
        local currentSize = tonumber(data and data.maxStackSize) or 0
        if currentSize <= 1 then return "single" end
        local isLarge = currentSize >= largeStackThreshold
        local targetSize = isLarge and largeStackSize or maxStackSize
        if currentSize >= targetSize then return "alreadyHigh" end
        local name = tostring(data.debugName or resource.guid)
        data.maxStackSize = targetSize
        resource.data = data
        if isLarge then return "raisedLarge", currentSize, name end
        return "raised"
    end)
    if not ok then
        failedCount = failedCount + 1
        firstFailure = firstFailure or tostring(result)
    elseif result == "raised" then
        raisedCount = raisedCount + 1
    elseif result == "raisedLarge" then
        largeRaisedCount = largeRaisedCount + 1
        local names = largeNamesByGameSize[gameSize]
        if not names then
            names = {}
            largeNamesByGameSize[gameSize] = names
        end
        names[#names + 1] = itemName
    elseif result == "single" then
        singleCount = singleCount + 1
    else
        alreadyHighCount = alreadyHighCount + 1
    end
end

log(string.format("%d of %d items raised: %d to %d, %d large ones (%d or more in the game) to %d "
    .. "(%d single, %d already at or above their new size)",
    raisedCount + largeRaisedCount, #items, raisedCount, maxStackSize, largeRaisedCount, largeStackThreshold,
    largeStackSize, singleCount, alreadyHighCount))

-- Which items count as large, by the stack size they have in the game, a few names per line.
local gameSizes = {}
for size in pairs(largeNamesByGameSize) do gameSizes[#gameSizes + 1] = size end
table.sort(gameSizes)
for _, size in ipairs(gameSizes) do
    local names = largeNamesByGameSize[size]
    table.sort(names)
    for first = 1, #names, NAMES_PER_LOG_LINE do
        local lineNames = table.concat(names, ", ", first, math.min(#names, first + NAMES_PER_LOG_LINE - 1))
        if first == 1 then
            log(string.format("large, %d in the game (%d items): %s", size, #names, lineNames))
        else
            log(string.format("large, %d in the game, continued: %s", size, lineNames))
        end
    end
end

if failedCount > 0 then
    log(string.format("%d items could not be read or changed, first error: %s", failedCount, firstFailure))
end
