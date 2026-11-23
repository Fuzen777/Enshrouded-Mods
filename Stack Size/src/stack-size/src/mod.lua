-- Stack Size: raises the max stack size of every stackable item (keen::ItemInfo.maxStackSize above 1)
-- to the "Max Stack Size" setting. Items that stack to 1 (weapons, armour ...) stay single, and items
-- that already stack as high as the setting or higher keep their size.
-- Runs in Shroudforge's startup asset pass, so a changed setting applies on the next game start.
-- One pass over the items only: Auto Loot Plus's many passes over every template ran Shroudforge out
-- of Lua references and made the whole asset pass fail.

local TAG = "[Stack Size]"
local DEFAULT_MAX_STACK_SIZE = 9999
local HIGHEST_STACK_SIZE = 65535 -- maxStackSize is a 16-bit value

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

if not loader.features.patch then
    error(TAG .. " needs the asset patch capability (loader.features.patch)")
end

local requestedSize = tonumber(setting("maxStackSize", DEFAULT_MAX_STACK_SIZE)) or DEFAULT_MAX_STACK_SIZE
local targetSize = math.floor(math.max(2, math.min(HIGHEST_STACK_SIZE, requestedSize)))

local items = game.assets.get_resources_by_type("keen::ItemInfo") or {}
local raisedCount, singleCount, alreadyHighCount, failedCount = 0, 0, 0, 0
local firstFailure

for _, resource in ipairs(items) do
    -- One odd item must not fail the whole asset pass (that would roll back every asset mod).
    local ok, result = pcall(function()
        local data = resource.data
        local currentSize = tonumber(data and data.maxStackSize) or 0
        if currentSize <= 1 then return "single" end
        if currentSize >= targetSize then return "alreadyHigh" end
        data.maxStackSize = targetSize
        resource.data = data
        return "raised"
    end)
    if not ok then
        failedCount = failedCount + 1
        firstFailure = firstFailure or tostring(result)
    elseif result == "raised" then
        raisedCount = raisedCount + 1
    elseif result == "single" then
        singleCount = singleCount + 1
    else
        alreadyHighCount = alreadyHighCount + 1
    end
end

log(string.format("%d of %d items raised to %d (%d single, %d already at or above it)",
    raisedCount, #items, targetSize, singleCount, alreadyHighCount))
if failedCount > 0 then
    log(string.format("%d items could not be read or changed, first error: %s", failedCount, firstFailure))
end
