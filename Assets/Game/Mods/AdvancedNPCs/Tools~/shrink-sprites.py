"""Makes ANPC sprite sets smaller on disk and in video memory.

Two passes over every ANPCs/<npc>/sprites*/ set given:

1. Oversized sets (on-screen detail far above a human's, e.g. a 7 cm viper drawn with a 256 px cell) are halved,
   repeatedly, until their detail is at most --max-density pixels per metre (humans are ~105), but never below
   1/--max-factor of the original size. Every pixel field of
   sprites.json is scaled with them, so the world size does not change. Projectile sheets are left alone.
2. Every sheet is re-saved as a maximally compressed PNG, with the colour of fully transparent pixels zeroed. The
   game turns sheets into DXT5, which already drops those colours, so what is shown does not change.

Originals are copied to --backup first (same relative paths). Running it again does nothing new: halved sets are under
the limit and re-saving only replaces a file when it gets smaller.

Usage: python shrink-sprites.py ANPCS_DIR [--backup DIR] [--max-density 250] [--max-factor 4] [--only id,id] [--jobs 6] [--dry-run]
Needs Pillow and numpy.
"""
import argparse
import glob
import io
import json
import os
import shutil
import sys
from concurrent.futures import ThreadPoolExecutor

import numpy as np
from PIL import Image

DIRECTIONS = 8
VANILLA_HEIGHT = 2.08     # vanilla Warrior frame (m): the world height of sets without spriteHeight


def load_json(path):
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def standing_px(sdir, js):
    """standingHeight, else the tallest opaque idle pixel above groundY (as the mod measures it)."""
    if js.get("standingHeight", 0) > 0:
        return js["standingHeight"]
    ch, ground, best = js["cellHeight"], js.get("groundY", 0), 0
    for name, a in js.get("animations", {}).items():
        path = os.path.join(sdir, name + ".png")
        if not name.startswith("idle") or "cellHeight" in a or not os.path.exists(path):
            continue
        alpha = np.asarray(Image.open(path).convert("RGBA").getchannel("A"))
        for r in range(min(DIRECTIONS, alpha.shape[0] // ch)):
            cell = alpha[r * ch:(r + 1) * ch, :a["frames"] * a["cellWidth"]]
            ys = np.nonzero((cell > 16).any(axis=1))[0]
            if len(ys):
                best = max(best, ch - ys.min() - ground)
    return best


def density(npc_json, sdir, js):
    height = (npc_json.get("spriteHeight") or VANILLA_HEIGHT) * (npc_json.get("spriteScale") or 1.0)
    return standing_px(sdir, js) / height


def shrink_cells(img, cw, ch, frames, ncw, nch):
    """Each cell resized (premultiplied, box filter) to its new size, centred in an ncw-wide cell."""
    src = img.convert("RGBA").convert("RGBa")
    rows = img.height // ch
    rw = min(ncw, max(1, round(cw * nch / ch)))
    out = Image.new("RGBa", (frames * ncw, rows * nch))
    for r in range(rows):
        for f in range(frames):
            cell = src.crop((f * cw, r * ch, (f + 1) * cw, (r + 1) * ch)).resize((rw, nch), Image.BOX)
            out.paste(cell, (f * ncw + (ncw - rw) // 2, r * nch))
    return out.convert("RGBA")


def halve_set(sdir, js, factor):
    """Scales every non-projectile sheet, death_static.png and pixel field of one set down by factor."""
    ch = js["cellHeight"]
    nch = max(1, round(ch / factor))
    scale = nch / ch
    for name, a in js.get("animations", {}).items():
        if "cellHeight" in a:
            continue        # projectile: its own size and scale
        cw, frames = a["cellWidth"], a["frames"]
        ncw = max(1, round(cw * scale))
        while (frames * ncw) % 4:       # DXT5 needs a width divisible by 4
            ncw += 1
        path = os.path.join(sdir, name + ".png")
        if os.path.exists(path):
            with Image.open(path) as im:
                small = shrink_cells(im, cw, ch, frames, ncw, nch)
            small.save(path, "PNG")
        a["cellWidth"] = ncw
    death = os.path.join(sdir, "death_static.png")
    if os.path.exists(death):
        with Image.open(death) as im:
            w = max(4, round(im.width * scale / 4) * 4)
            h = max(4, round(im.height * scale / 4) * 4)
            small = im.convert("RGBA").convert("RGBa").resize((w, h), Image.BOX).convert("RGBA")
        small.save(death, "PNG")
        if isinstance(js.get("deathStatic"), dict) and "groundY" in js["deathStatic"]:
            js["deathStatic"]["groundY"] = round(js["deathStatic"]["groundY"] * scale)
    js["cellHeight"] = nch
    js["groundY"] = round(js.get("groundY", 0) * scale)
    if js.get("standingHeight", 0) > 0:
        js["standingHeight"] = max(1, round(js["standingHeight"] * scale))
    if "pixelsPerUnit" in js:
        js["pixelsPerUnit"] = round(js["pixelsPerUnit"] * scale, 4)
    with open(os.path.join(sdir, "sprites.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(js, f, indent=2)
        f.write("\n")


def recompress(path, dry):
    """Re-saves one PNG as 8-bit RGBA, transparent colour zeroed, maximum compression; returns bytes saved."""
    old = os.path.getsize(path)
    with Image.open(path) as im:
        rgba = np.array(im.convert("RGBA"))
    rgba[rgba[:, :, 3] == 0] = 0
    buf = io.BytesIO()
    Image.fromarray(rgba, "RGBA").save(buf, "PNG", optimize=True)
    data = buf.getvalue()
    if len(data) >= old:
        return 0
    if not dry:
        with open(path, "wb") as f:
            f.write(data)
    return old - len(data)


def backup(path, root, backup_root):
    dest = os.path.join(backup_root, os.path.relpath(path, root))
    if not os.path.exists(dest):
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        shutil.copy2(path, dest)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("anpcs")
    ap.add_argument("--backup")
    ap.add_argument("--max-density", type=float, default=250.0)
    ap.add_argument("--only", default="")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--jobs", type=int, default=6)
    ap.add_argument("--max-factor", type=int, default=4, help="never smaller than 1/N (a 1/8 viper looked blurry)")
    args = ap.parse_args()
    if not args.dry_run and not args.backup:
        sys.exit("--backup DIR is required (or --dry-run)")
    only = set(filter(None, args.only.split(",")))
    root = os.path.abspath(args.anpcs)
    work = []
    for npc_path in sorted(glob.glob(os.path.join(root, "*", "npc.json"))):
        npc_dir = os.path.dirname(npc_path)
        if only and os.path.basename(npc_dir) not in only:
            continue
        try:
            npc_json = load_json(npc_path)
        except ValueError:
            npc_json = {}
        for set_json in sorted(glob.glob(os.path.join(npc_dir, "sprites*", "sprites.json"))):
            work.append((npc_json, set_json))

    def one(item):
        npc_json, set_json = item
        sdir = os.path.dirname(set_json)
        js = load_json(set_json)
        pngs = sorted(glob.glob(os.path.join(sdir, "*.png")))
        if not args.dry_run:
            for p in pngs + [set_json]:
                backup(p, root, args.backup)
        d = density(npc_json, sdir, js)
        factor = 1
        while d / factor > args.max_density and factor < args.max_factor:
            factor *= 2
        before = sum(os.path.getsize(p) for p in pngs)
        if factor > 1 and not args.dry_run:
            halve_set(sdir, js, factor)
        gain = sum(recompress(p, args.dry_run) for p in pngs)
        after = sum(os.path.getsize(p) for p in pngs) if not args.dry_run else before - gain
        print("%-40s %5.0f px/m  %s  %.1f -> %.1f MB" % (os.path.relpath(sdir, root), d,
              ("1/%d size" % factor) if factor > 1 else "        ", before / 1e6, after / 1e6), flush=True)
        return before - after

    # Threads, not processes: Pillow and numpy release the GIL, and no child console windows appear.
    with ThreadPoolExecutor(max_workers=args.jobs) as pool:
        saved = sum(pool.map(one, work))
    print("saved %.0f MB%s" % (saved / 1e6, " (dry run: halving skipped)" if args.dry_run else ""))


if __name__ == "__main__":
    main()
