"""Runs Stack Size's mod.lua against a fake Shroudforge asset API: checks the new stack sizes of single, normal and
large items, the setting limits and fallbacks, that one broken item can't stop the others, and the log lines.

Needs lupa (Lua for Python): py -m pip install lupa, or point PYTHONPATH at a folder that has it."""
import pathlib

try:
    import lupa.lua54 as lupa
except ImportError as error:
    raise SystemExit("needs lupa: py -m pip install lupa") from error

MOD = pathlib.Path(__file__).resolve().parents[1] / "src" / "stack-size" / "src" / "mod.lua"
FAKE = r"""
local function item(name, stackSize)
  return {guid = "guid-" .. name, data = {debugName = name, maxStackSize = stackSize}}
end
fakeItems = {
  item("Sword_T1_01", 1),
  item("Wood_Log", 50),
  item("Arrow_T1", 200),
  item("Block_Stone_01", 1000),
  item("Block_Clay_01", 1000),
  item("Block_Wood_01", 5000),
  {guid = "guid-nameless", data = {maxStackSize = 2000}},
  setmetatable({guid = "guid-broken"}, {__index = function(_, key)
    if key == "data" then error("cannot read item data") end
  end}),
}
for index = 1, extraLargeItems or 0 do
  fakeItems[#fakeItems + 1] = item(string.format("Block_Extra_%02d", index), 3000)
end
game = {assets = {get_resources_by_type = function(t)
  assert(t == "keen::ItemInfo", t)
  return fakeItems
end}}
shroudforge = {log = {info = function(m) py_log(m) end}, settings = {get = function(k, d)
  if settings[k] == nil then return d end
  return settings[k]
end}}
loader = {features = {patch = true}}
"""


def run(settings, extra_large_items=0):
    lua = lupa.LuaRuntime(unpack_returned_tuples=True)
    lines = []
    lua.globals().py_log = lambda message: lines.append(str(message))
    lua.globals().extraLargeItems = extra_large_items
    lua.globals().settings = lua.table_from(settings)
    lua.execute(FAKE)
    lua.execute(MOD.read_text(encoding="utf-8"))
    sizes = {}
    for entry in lua.globals().fakeItems.values():
        if entry.guid == "guid-broken":
            continue
        sizes[entry.data.debugName or entry.guid] = entry.data.maxStackSize
    return lines, sizes


BROKEN = "1 items could not be read or changed, first error: "

# Defaults: 9999 for normal stacks, 65535 for items that stack to 1000 or more in the game.
lines, sizes = run({})
assert sizes == {"Sword_T1_01": 1, "Wood_Log": 9999, "Arrow_T1": 9999, "Block_Stone_01": 65535,
                 "Block_Clay_01": 65535, "Block_Wood_01": 65535, "guid-nameless": 65535}, sizes
assert lines[0] == ("6 of 8 items raised: 2 to 9999, 4 large ones (1000 or more in the game) to 65535 "
                    "(1 single, 0 already at or above their new size)"), lines[0]
assert lines[1] == "large, 1000 in the game (2 items): Block_Clay_01, Block_Stone_01", lines[1]
assert lines[2] == "large, 2000 in the game (1 items): guid-nameless", lines[2]
assert lines[3] == "large, 5000 in the game (1 items): Block_Wood_01", lines[3]
assert lines[4].startswith(BROKEN) and "cannot read item data" in lines[4], lines[4]
assert len(lines) == 5, lines

# A higher threshold leaves the 1000 blocks with the normal stacks.
lines, sizes = run({"largeStackThreshold": 2000, "largeStackSize": 30000, "maxStackSize": 500})
assert sizes["Block_Stone_01"] == 1000, "already above Max Stack Size, should keep its size"
assert sizes["Arrow_T1"] == 500 and sizes["Wood_Log"] == 500
assert sizes["guid-nameless"] == 30000 and sizes["Block_Wood_01"] == 30000
assert lines[0] == ("4 of 8 items raised: 2 to 500, 2 large ones (2000 or more in the game) to 30000 "
                    "(1 single, 2 already at or above their new size)"), lines[0]

# Large items that already stack higher than Large Stack Size keep their size.
lines, sizes = run({"largeStackSize": 3000})
assert sizes["Block_Stone_01"] == 3000 and sizes["guid-nameless"] == 3000
assert sizes["Block_Wood_01"] == 5000, "lowered a large item"
assert "(1 single, 1 already at or above their new size)" in lines[0], lines[0]

# Limits: every size is a whole number from 2 to 65535.
lines, sizes = run({"maxStackSize": 1, "largeStackThreshold": 65535})
assert sizes["Wood_Log"] == 50, "lowered an item"
assert lines[0].startswith("0 of 8 items raised: 0 to 2, 0 large ones (65535 or more in the game)"), lines[0]
lines, sizes = run({"largeStackThreshold": 1.5})
assert sizes["Wood_Log"] == 65535, "threshold not raised to 2, which makes every stackable item large"
assert lines[0].startswith("6 of 8 items raised: 0 to 9999, 6 large ones (2 or more in the game)"), lines[0]
lines, sizes = run({"largeStackSize": 99999})
assert sizes["Block_Stone_01"] == 65535, "Large Stack Size not capped at 65535"
lines, sizes = run({"maxStackSize": 12345.9, "largeStackThreshold": 65535})
assert sizes["Block_Wood_01"] == 12345, "threshold 65535 should leave every item normal, size rounded down"
assert " 0 large ones " in lines[0] and len(lines) == 2, lines

# A setting that isn't a number falls back to its default.
lines, sizes = run({"maxStackSize": "lots", "largeStackSize": "lots", "largeStackThreshold": "lots"})
assert sizes["Wood_Log"] == 9999 and sizes["Block_Stone_01"] == 65535

# Long lists are split over several lines, 20 names each.
lines, sizes = run({}, extra_large_items=25)
assert lines[3].startswith("large, 3000 in the game (25 items): Block_Extra_01, "), lines[3]
assert lines[3].endswith("Block_Extra_20"), lines[3]
assert lines[4] == "large, 3000 in the game, continued: " + ", ".join(
    f"Block_Extra_{index:02d}" for index in range(21, 26)), lines[4]
print("OK")
