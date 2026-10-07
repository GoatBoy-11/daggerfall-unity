# Renders Advanced NPCs sprite sheets from a rigged, animated character (spec 1b): one sheet per Action,
# 8 direction rows (front, front_right, right, back_right, back, back_left, left, front_left), one column per
# frame (the last keyframe, a repeat of the first, is left out), one scale for every sheet, feet on a fixed
# ground row, plus sprites.json. The ground is the lowest point of the idle poses (the character need not
# stand at Z=0). Orthographic camera tilted 7 degrees down; strong key light from the viewer's
# upper left and a weak fill, high-contrast look (like Daggerfall's own sprites). Never saves the .blend.
#
# Usage:
#   blender -b character.blend --python render-sprites.py -- <out_dir> [test] [--rig NAME] [--mesh NAME]
#   test: renders only the first frame of idle_1 and attack_2 (or the first two Actions), to check light and size.
#   --rig / --mesh: armature and body mesh (default: the first armature and the meshes parented to it).
import bpy, math, sys, os, json
import numpy as np
from mathutils import Vector, Matrix

ARGS = sys.argv[sys.argv.index("--") + 1:]
OUT = ARGS[0]
TEST = "test" in ARGS[1:]
def option(name):
    return ARGS[ARGS.index(name) + 1] if name in ARGS else None
CELL_H = 256
PAD = 4                                   # pixels kept free around the figure
TILT = math.radians(7)
DIRS = ["front", "front_right", "right", "back_right", "back", "back_left", "left", "front_left"]
os.makedirs(OUT, exist_ok=True)
scene = bpy.context.scene
rig = bpy.data.objects[option("--rig")] if option("--rig") else next(o for o in bpy.data.objects if o.type == 'ARMATURE')
if option("--mesh"):
    body = bpy.data.objects[option("--mesh")]
else:
    body = next(o for o in bpy.data.objects if o.type == 'MESH' and o.parent == rig and not o.hide_render)
print("RIG", rig.name, "MESH", body.name)
for o in bpy.data.objects:
    o.hide_render = o not in (rig, body)

def set_action(act):
    ad = rig.animation_data or rig.animation_data_create()
    ad.action = act
    if hasattr(ad, "action_slot") and ad.action_slot is None and len(getattr(act, "slots", [])) > 0:
        ad.action_slot = act.slots[0]

def frames(act):
    a, b = act.frame_range
    return list(range(int(round(a)), int(round(b)) + 1))

# Camera axes in pivot space (camera looks toward +Y, tilted down).
d = Vector((0, math.cos(TILT), -math.sin(TILT)))
u = Vector((0, math.sin(TILT), math.cos(TILT)))
rgt = Vector((1, 0, 0))
def axes(k):
    rot = Matrix.Rotation(math.radians(-45 * k), 3, 'Z')
    return np.array(rot @ rgt), np.array(rot @ u)

def world_vertices():
    ev = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
    me = ev.to_mesh()
    co = np.empty(len(me.vertices) * 3); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
    m = np.array(body.matrix_world); w = co @ m[:3, :3].T + m[:3, 3]
    ev.to_mesh_clear()
    return w

# Ground height: the lowest point of the standing (idle) poses; every action if there is no idle one.
standing = [a for a in bpy.data.actions if a.name.lower().startswith("idle")] or list(bpy.data.actions)
ground_z = 1e9
for act in standing:
    set_action(act)
    for f in frames(act):
        scene.frame_set(f)
        ground_z = min(ground_z, float(world_vertices()[:, 2].min()))
print("GROUND z=%.3f (lowest point of %s)" % (ground_z, ", ".join(a.name for a in standing)))
feet = np.array([0.0, 0.0, ground_z])

# Exact extents on screen, relative to the feet, for every action, direction and frame.
ext = {}
for act in bpy.data.actions:
    set_action(act)
    half, lo, hi = 0.0, 1e9, -1e9
    for f in frames(act):
        scene.frame_set(f)
        w = world_vertices() - feet
        for k in range(len(DIRS)):
            ra, ua = axes(k)
            x = w @ ra; y = w @ ua
            half = max(half, float(np.abs(x).max()))
            lo = min(lo, float(y.min())); hi = max(hi, float(y.max()))
    ext[act.name] = (half, lo, hi)
lo_all = min(0.0, min(e[1] for e in ext.values())); hi_all = max(e[2] for e in ext.values())   # the feet row is always in the cell
ppu = (CELL_H - 2 * PAD) / (hi_all - lo_all)
ground_px = int(round(PAD + (0 - lo_all) * ppu))     # feet row, counted from the bottom of the cell
print("SCALE ppu=%.2f ground=%dpx up=[%.2f,%.2f]" % (ppu, ground_px, lo_all, hi_all))

pivot = bpy.data.objects.new("anpc_pivot", None); scene.collection.objects.link(pivot)
pivot.location = tuple(feet)
cam_data = bpy.data.cameras.new("anpc_cam"); cam_data.type = 'ORTHO'; cam_data.clip_end = 100
cam = bpy.data.objects.new("anpc_cam", cam_data); scene.collection.objects.link(cam)
cam.parent = pivot; cam.rotation_mode = 'QUATERNION'; cam.rotation_quaternion = d.to_track_quat('-Z', 'Y')
scene.camera = cam

def light(name, energy, elev, azim):
    ld = bpy.data.lights.new(name, 'SUN'); ld.energy = energy; ld.angle = math.radians(20)
    lo = bpy.data.objects.new(name, ld); scene.collection.objects.link(lo); lo.parent = pivot
    lo.rotation_euler = (math.radians(90 - elev), 0, math.radians(azim))
light("anpc_key", 4.5, 40, -25)            # from the viewer's upper left      # strong key near the viewer, upper left (Daggerfall look)
light("anpc_fill", 0.5, 10, 70)             # weak, from the viewer's right      # barely any fill: deep shadow sides
world = bpy.data.worlds.new("anpc_world"); world.use_nodes = True
bg = world.node_tree.nodes.get("Background"); bg.inputs[0].default_value = (1, 1, 1, 1); bg.inputs[1].default_value = 0.05
scene.world = world

r = scene.render
r.resolution_percentage = 100; r.film_transparent = True; r.filter_size = 0.75; r.use_motion_blur = False
r.image_settings.file_format = 'PNG'; r.image_settings.color_mode = 'RGBA'
scene.view_settings.view_transform = 'Standard'
try:
    scene.view_settings.look = 'Medium High Contrast'
except TypeError:
    scene.view_settings.look = 'None'
print('LOOK', scene.view_settings.look)

def save_sheet(sheet, path):
    h, w = sheet.shape[:2]
    img = bpy.data.images.new("sheet", w, h, alpha=True); img.pixels = sheet.ravel()
    img.filepath_raw = path; img.file_format = 'PNG'; img.save(); bpy.data.images.remove(img)

tmp = os.path.join(OUT, "_cells"); os.makedirs(tmp, exist_ok=True)
meta = {"pixelsPerUnit": round(ppu, 4), "cellHeight": CELL_H, "groundY": ground_px, "anchorX": "centre",
        "fps": scene.render.fps, "directions": DIRS, "animations": {}}
test_names = [n for n in ("idle_1", "attack_2") if n in bpy.data.actions] or [a.name for a in bpy.data.actions][:2]
for act in [a for a in bpy.data.actions if not TEST or a.name in test_names]:
    half = ext[act.name][0]
    cw = int(math.ceil((2 * half * ppu + 2 * PAD) / 8.0)) * 8
    cw = max(cw, 64)
    r.resolution_x, r.resolution_y = cw, CELL_H
    wu, hu = cw / ppu, CELL_H / ppu
    cam_data.ortho_scale = max(wu, hu)
    centre_up = (CELL_H / 2.0 - ground_px) / ppu       # screen centre, in units above the feet
    cam.location = Vector(u) * centre_up - d * 20
    set_action(act)
    fs = frames(act)[:-1]                              # the last keyframe repeats the first: not rendered
    if TEST:
        fs = fs[:1]
    sheet = np.zeros((len(DIRS) * CELL_H, len(fs) * cw, 4), dtype=np.float32)
    for k, dname in enumerate(DIRS):
        pivot.rotation_euler = (0, 0, math.radians(-45 * k))
        for i, f in enumerate(fs):
            scene.frame_set(f)
            p = os.path.join(tmp, "%s_%s_%02d.png" % (act.name, dname, i))
            r.filepath = p
            bpy.ops.render.render(write_still=True)
            img = bpy.data.images.load(p)
            px = np.array(img.pixels[:], dtype=np.float32).reshape(CELL_H, cw, 4)
            bpy.data.images.remove(img)
            sheet[k * CELL_H:(k + 1) * CELL_H, i * cw:(i + 1) * cw] = px[::-1]
    save_sheet(sheet[::-1].copy(), os.path.join(OUT, act.name + ".png"))
    meta["animations"][act.name] = {"cellWidth": cw, "frames": len(fs)}
    print("SHEET %s %dx%d cells, %d frames" % (act.name, cw, CELL_H, len(fs)))
with open(os.path.join(OUT, "sprites.json"), "w") as fh:
    json.dump(meta, fh, indent=2)
print("DONE")
