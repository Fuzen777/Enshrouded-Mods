"""Runs Focus Gain's mod.lua against a fake Shroudforge asset API: checks the raised values for tools and
weapons, that other configs stay untouched, the multiplier limits, and the log lines.

Needs lupa (Lua for Python): py -m pip install lupa, or point PYTHONPATH at a folder that has it."""
import pathlib

try:
    import lupa.lua54 as lupa
except ImportError as error:
    raise SystemExit("needs lupa: py -m pip install lupa") from error

MOD = pathlib.Path(__file__).resolve().parents[1] / "src" / "tool-focus" / "src" / "mod.lua"
FAKE = r"""
local function focusConfig(v)
  return {type = "keen::impact::Sint32ImpactConfig", value = {configId = {value = 1236200858}, value = v}}
end
local function otherConfig(id, v)
  return {type = "keen::impact::Sint32ImpactConfig", value = {configId = {value = id}, value = v}}
end
local function item(name, configs) return {guid = name, data = {debugName = name, impactValues = {simple = configs, scaled = {}}}} end
fakeItems = {
  item("Tool_T0_Axe_Stone_Grassland", {otherConfig(1920827256, 3), focusConfig(3)}),
  item("Tool_T7_Axe_Celesthium_Ancientlands", {focusConfig(3), otherConfig(387218275, 45)}),
  item("Tool_T4_Pickaxe_Iron_Desert", {focusConfig(3)}),
  item("Tool_T5_Pickaxe_Steel_Coldheights", {otherConfig(387218275, 30)}),
  item("Weapon_T4_1H_Axe_01", {otherConfig(387218275, 20), focusConfig(5)}),
  item("Weapon_T2_Staff_01", {focusConfig(3)}),
  item("Bow_T1_01", {focusConfig(3)}),
  item("Tool_Glider_T1", {}),
  {guid = "food", data = {debugName = "Food_Berry"}},
  {guid = "broken", data = {debugName = "Weapon_T1_Broken", impactValues = {simple = {{type = "keen::impact::Sint32ImpactConfig"}}}}},
}
game = {assets = {get_resources_by_type = function(t) assert(t == "keen::ItemInfo") return fakeItems end}}
shroudforge = {log = {info = function(m) py_log(m) end}, settings = {get = function(k, d) return settings[k] or d end}}
loader = {features = {patch = true}}
"""


def run(settings):
    lua = lupa.LuaRuntime(unpack_returned_tuples=True)
    lines = []
    lua.globals().py_log = lambda message: lines.append(str(message))
    lua.execute(FAKE)
    lua.globals().settings = lua.table_from(settings)
    lua.execute(MOD.read_text(encoding="utf-8"))
    gains = {}
    for entry in lua.globals().fakeItems.values():
        name = entry.data.debugName
        if entry.data.impactValues is None:
            continue
        for config in entry.data.impactValues.simple.values():
            if config.value is not None:
                gains[(name, config.value.configId.value)] = config.value.value
    return lines, gains


FOCUS = 1236200858

# Defaults: tools x3, weapons x2.
lines, gains = run({})
print("\n".join(lines))
assert gains[("Tool_T0_Axe_Stone_Grassland", FOCUS)] == 9
assert gains[("Tool_T0_Axe_Stone_Grassland", 1920827256)] == 3, "other config changed"
assert gains[("Tool_T7_Axe_Celesthium_Ancientlands", FOCUS)] == 9
assert gains[("Tool_T7_Axe_Celesthium_Ancientlands", 387218275)] == 45
assert gains[("Tool_T4_Pickaxe_Iron_Desert", FOCUS)] == 9
assert gains[("Weapon_T4_1H_Axe_01", FOCUS)] == 10
assert gains[("Weapon_T4_1H_Axe_01", 387218275)] == 20, "other config changed"
assert gains[("Weapon_T2_Staff_01", FOCUS)] == 6
assert gains[("Bow_T1_01", FOCUS)] == 6
assert lines[0] == "tool focus x3: 2 felling axes and 1 pickaxes (3 -> 9)", lines[0]
assert lines[1] == ("weapon focus x2: 3 items (3 -> 6 on 2, e.g. Bow_T1_01, Weapon_T2_Staff_01; "
                    "5 -> 10 on 1, e.g. Weapon_T4_1H_Axe_01); name prefixes: Weapon 2, Bow 1"), lines[1]
assert lines[2] == "1 tools have no focus per hit value, left as they are: Tool_T5_Pickaxe_Steel_Coldheights", lines[2]
assert lines[3].startswith("1 items could not be read or changed"), lines[3]
assert len(lines) == 4, lines

# Weapons x1 keeps the game's values, tools keep their own setting.
lines, gains = run({"focusMultiplier": 5, "weaponFocusMultiplier": 1})
assert gains[("Tool_T4_Pickaxe_Iron_Desert", FOCUS)] == 15
assert gains[("Weapon_T4_1H_Axe_01", FOCUS)] == 5, "weapon changed at x1"
assert gains[("Bow_T1_01", FOCUS)] == 3, "weapon changed at x1"
assert lines[0] == "tool focus x5: 2 felling axes and 1 pickaxes (3 -> 15)", lines[0]
assert lines[1].startswith("weapon focus x1: 3 items (3 -> 3 on 2, e.g. Bow_T1_01, Weapon_T2_Staff_01; 5 -> 5 on 1"), lines[1]

# Limits: both settings are whole numbers from 1 to 20.
lines, gains = run({"focusMultiplier": 50, "weaponFocusMultiplier": 0})
assert gains[("Tool_T4_Pickaxe_Iron_Desert", FOCUS)] == 60, "tool multiplier not capped at 20"
assert gains[("Weapon_T4_1H_Axe_01", FOCUS)] == 5, "weapon multiplier not raised to 1"
lines, gains = run({"focusMultiplier": 1, "weaponFocusMultiplier": 2.7})
assert gains[("Tool_T4_Pickaxe_Iron_Desert", FOCUS)] == 3
assert gains[("Weapon_T4_1H_Axe_01", FOCUS)] == 10, "weapon multiplier not rounded down"
assert lines[0] == "tool focus x1: 2 felling axes and 1 pickaxes (3 -> 3)", lines[0]
print("OK")
