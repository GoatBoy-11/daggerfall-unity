#!/usr/bin/env python3
"""Local meal overlay: eat/drink sheets made outside the repo (output/arp_meals_batch). Kept out of git until they move
into release bundles; only rendered sheets may ship, never the Oblivion-derived 3D source scenes.

Usage: python apply-local-meals.py <overlay ANPCs dir> <installed ANPCs dir>
For every <overlay>/<anpc>/sprites*/ folder whose ANPC is installed, copies its eat*/drink* sheets into the install and
adds their entries to the installed sprites.json, leaving every other sheet and entry as installed. A set whose layout
(pixelsPerUnit, cellHeight, groundY) differs from the installed one is skipped with a warning, since its sheets would
draw at the wrong size. build-mod.sh runs this after copying the examples, so builds no longer drop the meal sheets.
"""
import json, re, shutil, sys
from pathlib import Path

MEAL = re.compile(r'^(eat|drink)(_\d+)?\.png$', re.I)
LAYOUT = ('pixelsPerUnit', 'cellHeight', 'groundY')


def main(overlay, install):
    applied, skipped = 0, []
    for src_set in sorted(overlay.glob('*/sprites*')):
        anpc = src_set.parent.name
        dst_set = install / anpc / src_set.name
        sheets = [p for p in src_set.iterdir() if MEAL.match(p.name)]
        if not sheets or not (dst_set / 'sprites.json').exists():
            continue
        src_json = json.loads((src_set / 'sprites.json').read_text(encoding='utf-8'))
        try:
            dst_json = json.loads((dst_set / 'sprites.json').read_text(encoding='utf-8'))
        except ValueError as e:
            skipped.append('%s/%s: installed sprites.json unreadable (%s)' % (anpc, src_set.name, e)); continue
        if any(abs(float(src_json[k]) - float(dst_json[k])) > 1e-3 for k in LAYOUT):
            skipped.append('%s/%s: layout differs from the installed set' % (anpc, src_set.name)); continue
        for sheet in sheets:
            name = sheet.stem
            if name not in src_json['animations']:
                skipped.append('%s/%s: %s has no sprites.json entry' % (anpc, src_set.name, sheet.name)); continue
            shutil.copy2(sheet, dst_set / sheet.name)
            dst_json['animations'][name] = src_json['animations'][name]
        (dst_set / 'sprites.json').write_text(json.dumps(dst_json, indent=2), encoding='utf-8')
        readme = src_set.parent / 'README_MEALS.txt'
        if readme.exists():
            shutil.copy2(readme, install / anpc / readme.name)
        applied += 1
    for s in skipped:
        print('meals overlay: skipped ' + s)
    print('Applied local meal sheets to %d sprite set(s) from %s' % (applied, overlay))
    return 0


if __name__ == '__main__':
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    sys.exit(main(Path(sys.argv[1]), Path(sys.argv[2])))
