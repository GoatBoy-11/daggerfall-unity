#!/usr/bin/env python3
"""Checks Schemas~/*.schema.json against the parsers (spec tools-quality §A2).

1. Every schema is valid JSON.
2. Each object's property names match the keys the C# parser knows (read from the source), so the schemas
   cannot fall behind the parsers.
3. When the `jsonschema` module is installed, every example npc.json, sprites.json and dialogue file validates.

Exit 0 only when every check passed. Run: python Tools~/check-schemas.py
"""
import glob
import json
import os
import re
import sys

MOD = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCHEMAS = os.path.join(MOD, "Schemas~")
CORE = os.path.join(MOD, "Scripts", "Core")
EXAMPLES = os.path.join(MOD, "Examples", "ANPCs")
problems = []


def source(name):
    with open(os.path.join(CORE, name), encoding="utf-8") as f:
        return f.read()


def array(text, name):
    """The string literals of `static readonly string[] <name> = { ... }` (or Join(new[] { ... }, ...))."""
    m = re.search(r"\b" + name + r"\s*=\s*(?:Join\(new\[\]\s*)?\{(.*?)\}", text, re.S)
    if not m:
        problems.append("could not find " + name)
        return set()
    return set(re.findall(r'"([^"]*)"', m.group(1)))


def load(name):
    path = os.path.join(SCHEMAS, name)
    try:
        with open(path, encoding="utf-8") as f:
            return json.load(f)
    except Exception as e:
        problems.append(name + ": not valid JSON (" + str(e) + ")")
        return None


def strip_comments(text):
    """Drops // and /* */ comments outside strings, as the mod's own reader does."""
    return re.sub(r'("(?:\\.|[^"\\])*")|//[^\n]*|/\*.*?\*/', lambda m: m.group(1) or "", text, flags=re.S)


def same(where, schema_keys, parser_keys):
    schema_keys = set(schema_keys) - {"$schema"}
    missing = parser_keys - schema_keys
    extra = schema_keys - parser_keys
    if missing:
        problems.append(where + ": schema lacks " + ", ".join(sorted(missing)))
    if extra:
        problems.append(where + ": schema has keys the parser does not know: " + ", ".join(sorted(extra)))


npc = load("npc.schema.json")
sprites = load("sprites.schema.json")
dialogue = load("dialogue.schema.json")
package = load("package.schema.json")
spot = load("spot.schema.json")

definition = source("DefinitionParser.cs")
if npc:
    p = npc["properties"]
    same("npc.json", p, array(definition, "SharedKeys") | array(definition, "UniqueKeys") | array(definition, "GenericKeys"))
    same("npc.json spawn", p["spawn"]["properties"], array(definition, "SpawnKeys"))
    same("npc.json spawn.places[]", p["spawn"]["properties"]["places"]["items"]["properties"], array(definition, "PlaceKeys"))
    outside = source("OutsideSpawn.cs")
    same("npc.json spawn.dungeons", p["spawn"]["properties"]["dungeons"]["properties"], array(outside, "DungeonKeys"))
    same("npc.json spawn.interiors", p["spawn"]["properties"]["interiors"]["properties"], array(outside, "InteriorKeys"))
    same("npc.json spawn.wilderness", p["spawn"]["properties"]["wilderness"]["properties"], array(outside, "WildernessKeys"))
    same("npc.json magic", p["magic"]["properties"], array(source("NpcMagicDefinition.cs"), "Keys"))
    same("npc.json magic.healing", p["magic"]["properties"]["healing"]["properties"], array(source("NpcMagicDefinition.cs"), "HealingKeys"))
    same("npc.json combat", p["combat"]["properties"], array(source("CombatRules.cs"), "Keys"))
    same("npc.json sounds", p["sounds"]["properties"], array(source("SoundRules.cs"), "Keys"))

if package:
    package_source = source("AiPackage.cs")
    for branch, key in zip(package["oneOf"], ["StandKeys", "WanderKeys", "TravelKeys", "PatrolKeys", "AnimationKeys", "GuardKeys", "FollowKeys", "WorkKeys", "SequenceKeys", "CivilianKeys", "ShelterKeys", "EatKeys", "ChoreKeys", "HawkKeys", "SocialKeys"]):
        same("package", branch["properties"], array(package_source, "Keys"))
        same("package " + key, branch["properties"]["parameters"]["properties"], array(package_source, key))
if spot:
    same("spot",spot["properties"],array(source("NamedSpots.cs"),"Keys"))
    same("spot location",spot["properties"]["location"]["properties"],array(source("NamedSpots.cs"),"LocationKeys"))
if npc:
    same("npc schedule", npc["properties"]["schedule"]["items"]["properties"], array(source("AiSchedule.cs"), "Keys"))
    same("npc schedule.when", npc["definitions"]["scheduleWhen"]["properties"], array(source("Condition.cs"), "KnownKeys") - {"tone"})

if sprites:
    sprite_source = source("SpriteSet.cs")
    top = set(re.findall(r'(?:FieldReader\.\w+|WholeAbove)\(o, "(\w+)"', sprite_source)) | {"directions"}     # directions: informational
    same("sprites.json", sprites["properties"], top)
    anim = set(re.findall(r'(?:FieldReader\.\w+|WholeAbove)\(a, "(\w+)"', sprite_source))
    same("sprites.json animations.*", sprites["properties"]["animations"]["additionalProperties"]["properties"], anim)

if dialogue:
    d = source("DialogueParser.cs")
    actions = array(d, "ActionKeys")
    defs = dialogue["definitions"]
    same("dialogue", dialogue["properties"], array(d, "FileKeys"))
    same("dialogue topic", defs["topic"]["properties"], array(d, "TopicKeys") | actions)
    same("dialogue answer", defs["answer"]["anyOf"][1]["properties"], array(d, "AnswerKeys") | actions)
    same("dialogue reply", defs["reply"]["properties"], array(d, "ReplyKeys") | actions)
    same("dialogue when", defs["when"]["properties"], array(source("Condition.cs"), "KnownKeys"))

try:
    import jsonschema
except ImportError:
    jsonschema = None
    print("note: the jsonschema module is not installed; example files were not validated (pip install jsonschema)")

if jsonschema and npc and sprites and dialogue:
    def validate(schema, pattern):
        n = 0
        for path in sorted(glob.glob(os.path.join(EXAMPLES, pattern))):
            with open(path, encoding="utf-8") as f:
                data = json.loads(strip_comments(f.read()))
            for error in jsonschema.Draft7Validator(schema).iter_errors(data):
                where = "/".join(str(x) for x in error.absolute_path)
                problems.append(os.path.relpath(path, MOD) + ": " + (where + ": " if where else "") + error.message)
            n += 1
        return n
    count = validate(npc, "*/npc.json") + validate(sprites, "*/sprites*/sprites.json")
    count += validate(npc, "_Examples/*/npc.json") + validate(sprites, "_Examples/*/sprites*/sprites.json")
    count += validate(dialogue, "_Dialogue/*.json") + validate(dialogue, "*/dialogue.json")
    if package:
        count += validate(package, "_Packages/*.json")
    if spot:
        count += validate(spot,"_Spots/*.json")
    print("validated " + str(count) + " example file(s)")

for p in problems:
    print("FAIL " + p)
print("check-schemas: " + ("OK" if not problems else str(len(problems)) + " problem(s)"))
sys.exit(1 if problems else 0)
