"""Builds the Nexus zips: the Advanced NPCs framework and one zip per submod (Tools~/release-packs.json).

Usage: python package-release.py [--dry-run] [--include-held] [--only NAME ...] [--self-test]
  Run Tools~/build-mod.sh --release first: the framework zip takes Builds/AdvancedNPCs-release/.../advancednpcs.dfmod.
  Output: <project>/Builds/Release/<zip>-<ModVersion>.zip, laid out like a DFU install
  (DaggerfallUnity_Data/StreamingAssets/Mods + .../ANPCs), so players extract it into their game folder.

Framework: dfmod, README/MANUAL/schemas (ANPCs/_Docs), the repo's shared folders and example folders.
Packs: ANPC folders from the manifest's "source" plus the portraits and dialogue types they use that the framework
lacks; each pack gets ANPCs/_Credits/<pack>.txt pointing at its folders' credit files. Submods never ship root files,
so installing several never overwrites another's.
"""
import fnmatch
import io
import json
import os
import re
import sys
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
MOD = os.path.dirname(HERE)
PROJECT = os.path.abspath(os.path.join(MOD, "../../../.."))
REPO_ANPCS = os.path.join(MOD, "Examples", "ANPCs")
DFMOD = os.path.join(PROJECT, "Builds", "AdvancedNPCs-release", "StandaloneWindows", "advancednpcs.dfmod")
OUT = os.path.join(PROJECT, "Builds", "Release")
ANPCS_IN_ZIP = "DaggerfallUnity_Data/StreamingAssets/ANPCs/"
MODS_IN_ZIP = "DaggerfallUnity_Data/StreamingAssets/Mods/"
STORED = (".png", ".ogg", ".wav", ".mp3", ".dfmod")  # already compressed
CREDIT_FILE = re.compile(r"(CREDITS|README)[^/]*\.(txt|md)$", re.I)

# shortcut: regexes on the raw npc.json text (comments allowed there), so a commented-out reference still counts;
# that only adds a file or a warning. Upgrade to a comment-aware parser if that ever matters.
PORTRAIT = re.compile(r'"portrait"\s*:\s*"([^"]+)"')
LIST_OR_STRING = r'\s*:\s*(\[[^\]]*\]|"[^"]*")'
DIALOGUE = re.compile(r'"dialogue"' + LIST_OR_STRING)
VOICE = re.compile(r'"voice"' + LIST_OR_STRING)


def names(match):
    return re.findall(r'"([^"]+)"', match) if match else []


def references(text):
    """(portraits, dialogue types, voice packs) an npc.json names."""
    return ([m for m in PORTRAIT.findall(text)],
            [n for m in DIALOGUE.findall(text) for n in names(m)],
            [n for m in VOICE.findall(text) for n in names(m)])


def self_test():
    p, d, v = references('{ "portrait": "wench", // "a//b"\n "dialogue": ["gossip", "tavern"], "sounds": { "voice": "va_x" } }')
    assert p == ["wench"] and d == ["gossip", "tavern"] and v == ["va_x"], (p, d, v)
    assert references('{ "dialogue": "one" }')[1] == ["one"]
    assert expand(["a*", "b"], ["a1", "a2", "b", "c"]) == ["a1", "a2", "b"]
    print("self-test OK")


def expand(patterns, available):
    out = []
    for p in patterns:
        hits = [a for a in available if fnmatch.fnmatchcase(a, p)]
        if not hits:
            raise SystemExit("ERROR: no folder matches " + p)
        out += [h for h in hits if h not in out]
    return out


def excluded(rel, patterns):
    return any(fnmatch.fnmatchcase(part, p) for part in rel.replace("\\", "/").split("/") for p in patterns)


def files_under(root, rel, patterns):
    """(disk path, path relative to root) for a file or folder rel under root."""
    path = os.path.join(root, rel)
    if os.path.isfile(path):
        return [(path, rel)]
    found = []
    for d, dirs, files in os.walk(path):
        dirs[:] = [x for x in dirs if not excluded(x, patterns)]
        for f in files:
            r = os.path.relpath(os.path.join(d, f), root).replace("\\", "/")
            if not excluded(r, patterns):
                found.append((os.path.join(d, f), r))
    return sorted(found, key=lambda x: x[1])


def read(path):
    with io.open(path, encoding="utf-8-sig") as f:
        return f.read()


def write_zip(path, entries, texts, dry):
    """entries: (disk path, name in zip); texts: (name in zip, text)."""
    size = sum(os.path.getsize(p) for p, _ in entries)
    print("  %s: %d files, %.0f MB" % (os.path.basename(path), len(entries) + len(texts), size / 1048576.0))
    if dry:
        return
    tmp = path + ".part"
    with zipfile.ZipFile(tmp, "w") as z:
        for disk, name in entries:
            z.write(disk, name, zipfile.ZIP_STORED if name.lower().endswith(STORED) else zipfile.ZIP_DEFLATED)
        for name, text in texts:
            z.writestr(name, text, zipfile.ZIP_DEFLATED)
    os.replace(tmp, path)


def main(argv):
    if "--self-test" in argv:
        self_test()
        return
    dry = "--dry-run" in argv
    held = "--include-held" in argv
    only = argv[argv.index("--only") + 1:] if "--only" in argv else None
    manifest = json.loads(read(os.path.join(HERE, "release-packs.json")))
    version = json.loads(read(os.path.join(MOD, "AdvancedNPCs.dfmod.json")))["ModVersion"]
    source = manifest["source"]
    skip = manifest["exclude"]
    never = manifest["never"]
    repo_dirs = sorted(d for d in os.listdir(REPO_ANPCS) if os.path.isdir(os.path.join(REPO_ANPCS, d)))
    src_dirs = sorted(d for d in os.listdir(source) if os.path.isdir(os.path.join(source, d)) and not d.startswith("_"))
    os.makedirs(OUT, exist_ok=True)
    print("Advanced NPCs %s -> %s%s" % (version, OUT, " (dry run)" if dry else ""))

    # Framework: everything from the repo.
    fw = manifest["framework"]
    fw_folders = expand(fw["folders"], repo_dirs)
    fw_dialogue = {os.path.splitext(f)[0] for f in os.listdir(os.path.join(REPO_ANPCS, "_Dialogue")) if f.endswith(".json")}
    fw_voices = {d for d in os.listdir(os.path.join(REPO_ANPCS, "_Sounds")) if d.startswith("va_")}
    if only is None or fw["zip"] in only:
        if not os.path.isfile(DFMOD) and not dry:
            raise SystemExit("ERROR: no release bundle at %s; run Tools~/build-mod.sh --release first" % DFMOD)
        entries = [(DFMOD, MODS_IN_ZIP + "advancednpcs.dfmod")] if os.path.isfile(DFMOD) else []
        for doc in ("README.md", "MANUAL.md"):
            entries.append((os.path.join(MOD, doc), ANPCS_IN_ZIP + "_Docs/" + doc))
        for disk, rel in files_under(os.path.join(MOD, "Schemas~"), "", skip):
            entries.append((disk, ANPCS_IN_ZIP + "_Docs/Schemas/" + rel))
        portraits = set()
        for rel in fw["shared"] + fw_folders:
            for disk, r in files_under(REPO_ANPCS, rel, skip):
                entries.append((disk, ANPCS_IN_ZIP + r))
        for f in fw_folders:
            portraits.update(references(read(os.path.join(REPO_ANPCS, f, "npc.json")))[0])
        for p in sorted(portraits):
            disk = os.path.join(REPO_ANPCS, "_Portraits", p + ".png")
            if os.path.isfile(disk):
                entries.append((disk, ANPCS_IN_ZIP + "_Portraits/" + p + ".png"))
        write_zip(os.path.join(OUT, "%s-%s.zip" % (fw["zip"], version)), entries, [], dry)

    # Submods: folders from the install.
    owner = {f: fw["zip"] for f in fw_folders}
    for name, pack in manifest["packs"].items():
        folders = expand(pack["folders"], src_dirs)
        for f in folders:
            if f in owner:
                raise SystemExit("ERROR: %s is in both %s and %s" % (f, owner[f], name))
            owner[f] = name
        if (only is not None and name not in only) or (pack.get("hold") and not held):
            if pack.get("hold") and (only is None or name in only):
                print("  %s: held (%s)" % (name, pack["hold"]))
            continue
        entries, credits = [], []
        extra_portraits, extra_dialogue = set(), set()
        for f in folders:
            npc = os.path.join(source, f, "npc.json")
            if not os.path.isfile(npc):
                raise SystemExit("ERROR: %s has no npc.json" % f)
            ps, ds, vs = references(read(npc))
            extra_portraits.update(ps)
            extra_dialogue.update(d for d in ds if d not in fw_dialogue)
            for v in vs:
                if v not in fw_voices:
                    print("  WARNING: %s uses voice pack %s, which the framework does not ship" % (f, v))
            for disk, r in files_under(source, f, skip):
                entries.append((disk, ANPCS_IN_ZIP + r))
                if CREDIT_FILE.search(r):
                    credits.append(r)
        for p in sorted(extra_portraits):
            disk = os.path.join(source, "_Portraits", p + ".png")
            if os.path.isfile(disk):
                entries.append((disk, ANPCS_IN_ZIP + "_Portraits/" + p + ".png"))
            else:
                print("  WARNING: %s: portrait %s.png missing (vanilla face shown)" % (name, p))
        for d in sorted(extra_dialogue):
            disk = os.path.join(source, "_Dialogue", d + ".json")
            if os.path.isfile(disk):
                entries.append((disk, ANPCS_IN_ZIP + "_Dialogue/" + d + ".json"))
            else:
                print("  WARNING: %s: dialogue type %s missing" % (name, d))
        text = "%s %s for Advanced NPCs (needs the Advanced NPCs framework).\r\n\r\nANPCs: %s\r\n\r\nCredits and notes per ANPC:\r\n%s\r\n" % (
            name, version, ", ".join(folders), "\r\n".join("- " + c for c in credits) or "- (none found)")
        write_zip(os.path.join(OUT, "%s-%s.zip" % (name, version)), entries, [(ANPCS_IN_ZIP + "_Credits/" + name + ".txt", text)], dry)

    loose = [d for d in src_dirs if d not in owner and d not in never]
    if loose:
        print("Not in any zip (add to release-packs.json or 'never'): " + ", ".join(loose))


if __name__ == "__main__":
    main(sys.argv[1:])
