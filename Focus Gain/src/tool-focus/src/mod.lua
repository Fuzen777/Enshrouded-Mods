-- Focus Gain (called Tool Focus until 1.0.1, id tool-focus): multiplies the focus that felling axes and
-- pickaxes give per hit by the "Tool Focus Multiplier" setting, and the focus of every other item that gives
-- focus per hit (weapons) by the "Weapon Focus Multiplier" setting.
-- Focus is the game's SpecialAbility attribute. Each weapon and tool carries its focus per hit as an
-- impact config in keen::ItemInfo.impactValues.simple (a Sint32ImpactConfig, configId 1236200858), which the
-- hit programs read through their SpecialAbilityGain::value variable. Felling axes and pickaxes carry 3.
-- Runs in Shroudforge's startup asset pass, so a changed setting applies on the next game start.

local FOCUS_GAIN_CONFIG_ID = 1236200858
local FOCUS_GAIN_CONFIG_TYPE = "keen::impact::Sint32ImpactConfig"
local TOOL_DEFAULT_MULTIPLIER = 3
local WEAPON_DEFAULT_MULTIPLIER = 2
local HIGHEST_MULTIPLIER = 20
local HIGHEST_SINT32 = 2147483647
local EXAMPLES_PER_CHANGE = 3

-- Felling axes are Tool_T0_Axe_Stone_Grassland to Tool_T7_Axe_Celesthium_Ancientlands (category Tools),
-- pickaxes Tool_T0_Pickaxe_... to Tool_T7_Pickaxe_... (category BuildTools).
local TOOL_KINDS = {
    {label = "felling axes", pattern = "^Tool_T%d+_Axe_"},
    {label = "pickaxes", pattern = "^Tool_T%d+_Pickaxe_"},
}

local function log(message)
    if shroudforge and shroudforge.log and shroudforge.log.info then
        shroudforge.log.info(message)
    else
        print("[Focus Gain] " .. message)
    end
end

local function setting(key, fallback)
    if shroudforge and shroudforge.settings and shroudforge.settings.get then
        return shroudforge.settings.get(key, fallback)
    end
    return fallback
end

-- A whole multiplier from 1 to HIGHEST_MULTIPLIER.
local function multiplierSetting(key, fallback)
    local requestedMultiplier = tonumber(setting(key, fallback)) or fallback
    return math.floor(math.max(1, math.min(HIGHEST_MULTIPLIER, requestedMultiplier)))
end

-- A HashKey32 reads as {value = n}; accept a bare number too.
local function hashValue(hashKey)
    if type(hashKey) == "number" then return hashKey end
    local ok, value = pcall(function() return hashKey.value end)
    return ok and tonumber(value) or nil
end

local function toolKindOf(debugName)
    for _, kind in ipairs(TOOL_KINDS) do
        if debugName:match(kind.pattern) then return kind end
    end
    return nil
end

-- Index of the focus gain config in impactValues.simple, or nil (also for an item without impact values).
local function findFocusGainIndex(data)
    local impactValues = data.impactValues
    local simpleConfigs = impactValues and impactValues.simple
    if not simpleConfigs then return nil end
    for index, entry in ipairs(simpleConfigs) do
        if tostring(entry.type) == FOCUS_GAIN_CONFIG_TYPE and hashValue(entry.value.configId) == FOCUS_GAIN_CONFIG_ID then
            return index
        end
    end
    return nil
end

-- Writes the new focus per hit and reads it back.
local function writeFocusGain(resource, data, index, newGain)
    -- Nested values may come back as copies, so every level is written back to its parent.
    local impactValues = data.impactValues
    local entries = {}
    for entryIndex, entry in ipairs(impactValues.simple) do entries[entryIndex] = entry end
    local focusEntry = entries[index]
    local focusConfig = focusEntry.value
    focusConfig.value = newGain
    focusEntry.value = focusConfig
    entries[index] = focusEntry
    impactValues.simple = entries
    data.impactValues = impactValues
    resource.data = data
    local writtenGain = tonumber(resource.data.impactValues.simple[index].value.value)
    if writtenGain ~= newGain then
        error(string.format("focus per hit reads %s after writing %d", tostring(writtenGain), newGain))
    end
end

-- How many items went from each original focus per hit to its new one, with a few example names.
local function recordChange(changes, originalGain, newGain, debugName)
    local key = string.format("%d -> %d", originalGain, newGain)
    local change = changes[key]
    if not change then
        change = {text = key, originalGain = originalGain, newGain = newGain, count = 0, names = {}}
        changes[key] = change
    end
    change.count = change.count + 1
    change.names[#change.names + 1] = debugName
end

local function sortedChanges(changes)
    local list = {}
    for _, change in pairs(changes) do list[#list + 1] = change end
    table.sort(list, function(left, right)
        if left.originalGain ~= right.originalGain then return left.originalGain < right.originalGain end
        return left.newGain < right.newGain
    end)
    return list
end

if not loader.features.patch then
    error("Focus Gain needs the asset patch capability (loader.features.patch)")
end

local toolMultiplier = multiplierSetting("focusMultiplier", TOOL_DEFAULT_MULTIPLIER)
local weaponMultiplier = multiplierSetting("weaponFocusMultiplier", WEAPON_DEFAULT_MULTIPLIER)

local toolCounts, toolChanges, withoutFocusGain, failures = {}, {}, {}, {}
local weaponCount, weaponChanges, weaponNamePrefixes = 0, {}, {}

for _, resource in ipairs(game.assets.get_resources_by_type("keen::ItemInfo") or {}) do
    -- One odd item must not fail the whole asset pass (that would roll back every asset mod).
    local ok, err = pcall(function()
        local data = resource.data
        local debugName = tostring(data.debugName or "")
        local kind = toolKindOf(debugName)

        local index = findFocusGainIndex(data)
        if not index then
            if kind then withoutFocusGain[#withoutFocusGain + 1] = debugName end
            return
        end

        local multiplier = kind and toolMultiplier or weaponMultiplier
        local originalGain = tonumber(data.impactValues.simple[index].value.value) or 0
        local newGain = math.min(HIGHEST_SINT32, originalGain * multiplier)
        if newGain ~= originalGain then
            writeFocusGain(resource, data, index, newGain)
        end

        if kind then
            toolCounts[kind.label] = (toolCounts[kind.label] or 0) + 1
            recordChange(toolChanges, originalGain, newGain, debugName)
        else
            weaponCount = weaponCount + 1
            recordChange(weaponChanges, originalGain, newGain, debugName)
            local namePrefix = debugName:match("^([^_]+)") or debugName
            weaponNamePrefixes[namePrefix] = (weaponNamePrefixes[namePrefix] or 0) + 1
        end
    end)
    if not ok then failures[#failures + 1] = tostring(err) end
end

-- Tools: "tool focus x3: 8 felling axes and 8 pickaxes (3 -> 9)"
local toolCountTexts = {}
for _, kind in ipairs(TOOL_KINDS) do
    toolCountTexts[#toolCountTexts + 1] = string.format("%d %s", toolCounts[kind.label] or 0, kind.label)
end
local toolChangeTexts = {}
for _, change in ipairs(sortedChanges(toolChanges)) do toolChangeTexts[#toolChangeTexts + 1] = change.text end
log(string.format("tool focus x%d: %s (%s)", toolMultiplier, table.concat(toolCountTexts, " and "),
    #toolChangeTexts > 0 and table.concat(toolChangeTexts, ", ") or "nothing changed"))

-- Every other item: "weapon focus x2: 140 items (3 -> 6 on 120, e.g. A, B, C; ...); name prefixes: Weapon 140"
local weaponChangeTexts = {}
for _, change in ipairs(sortedChanges(weaponChanges)) do
    table.sort(change.names)
    local examples = {}
    for exampleIndex = 1, math.min(EXAMPLES_PER_CHANGE, #change.names) do examples[exampleIndex] = change.names[exampleIndex] end
    weaponChangeTexts[#weaponChangeTexts + 1] = string.format("%s on %d, e.g. %s", change.text, change.count,
        table.concat(examples, ", "))
end
local prefixList = {}
for namePrefix, count in pairs(weaponNamePrefixes) do prefixList[#prefixList + 1] = {name = namePrefix, count = count} end
table.sort(prefixList, function(left, right)
    if left.count ~= right.count then return left.count > right.count end
    return left.name < right.name
end)
local prefixTexts = {}
for _, prefix in ipairs(prefixList) do prefixTexts[#prefixTexts + 1] = string.format("%s %d", prefix.name, prefix.count) end
log(string.format("weapon focus x%d: %d items (%s)%s", weaponMultiplier, weaponCount,
    #weaponChangeTexts > 0 and table.concat(weaponChangeTexts, "; ") or "nothing changed",
    #prefixTexts > 0 and ("; name prefixes: " .. table.concat(prefixTexts, ", ")) or ""))

if #withoutFocusGain > 0 then
    log(string.format("%d tools have no focus per hit value, left as they are: %s", #withoutFocusGain,
        table.concat(withoutFocusGain, ", ")))
end
if #failures > 0 then
    log(string.format("%d items could not be read or changed, first error: %s", #failures, failures[1]))
end
