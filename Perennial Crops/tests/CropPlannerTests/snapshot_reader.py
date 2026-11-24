"""Answers boot game-data queries from Kiln's snapshot, as Mod.OnBoot's boot.GameData.Read does, for the planner tests.

Reads <snapshot folder>/templates.* and resources.* (the folder Kiln's launcher writes in <game>/kiln/data/boot-gamedata/<key>).
One query per line on stdin, one JSON line per answer on stdout: {"ok": true, "resourceId": "...", "value": {...}} or {"ok": false}.
Queries: names:templates, template:<name>, resource:<GUID>/<qualified type name>/<part> (templates, items and other resources). Nothing is written.
"""
import json
import sys
from pathlib import Path


def main():
    root = Path(sys.argv[1])
    templates = json.loads((root / "templates.index.json").read_text(encoding="utf-8"))["entries"]
    template_by_guid = {entry[2]: entry for entry in templates.values()}
    items = json.loads((root / "items.index.json").read_text(encoding="utf-8"))["entries"]
    item_by_guid = {entry[2]: entry for entry in items.values()}
    item_data = open(root / "items.data", "rb")
    resource_types = json.loads((root / "resources.index.json").read_text(encoding="utf-8"))["types"]
    template_data = open(root / "templates.data", "rb")
    resource_data = open(root / "resources.data", "rb")

    def read(handle, offset, size):
        handle.seek(offset)
        return json.loads(handle.read(size).decode("utf-8"))

    def answer(query):
        if query == "names:templates":
            return {"ok": True, "resourceId": None, "value": {"names": sorted(templates)}}
        if query.startswith("template:"):
            entry = templates.get(query[len("template:"):])
            return {"ok": True, "resourceId": entry[2], "value": read(template_data, entry[0], entry[1])} if entry else {"ok": False}
        if query.startswith("resource:"):
            guid, type_name, part = query[len("resource:"):].split("/")
            if type_name == "keen::ecs::TemplateResource":
                entry = template_by_guid.get(guid)
                return {"ok": True, "resourceId": guid, "value": read(template_data, entry[0], entry[1])} if entry else {"ok": False}
            if type_name == "keen::ItemInfo":
                entry = item_by_guid.get(guid)
                return {"ok": True, "resourceId": guid, "value": read(item_data, entry[0], entry[1])} if entry else {"ok": False}
            for entry in resource_types.get(type_name, {}).get("entries", []):
                if entry[0] == guid and entry[1] == int(part) and entry[3] >= 0:
                    return {"ok": True, "resourceId": guid, "value": read(resource_data, entry[3], entry[4])}
            return {"ok": False}
        return {"ok": False}

    for line in sys.stdin:
        query = line.strip()
        if query:
            sys.stdout.write(json.dumps(answer(query)) + "\n")
            sys.stdout.flush()


if __name__ == "__main__":
    main()
