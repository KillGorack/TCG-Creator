"""
render_set.py  --  renders ONE card-frame set: 2 layers x 2 base colors (white, black) = 4 images.
From these four, any frame color is rebuilt exactly:  color = black + base_color * (white - black)   (in linear light).

Layers (from the objects in Card_Frame.blend):
  back : _Bezel, _Standard, _Text_Plane            (the frame, painted by the "Darker" material on _Standard)
  cuts : Holes, Backs                              (the seven badge pieces, painted by "Darker" on Holes; Backs stays black)

Run (writes back_white.png, back_black.png, cuts_white.png, cuts_black.png into the folder):

    SET_OUT=CardCreator/Assets/frames/monolith SET_SAMPLES=1024 SET_ROUGHNESS=0.65 \
        blender -b --factory-startup --disable-autoexec Card_Frame.blend --python tools/render_set.py

Settings (environment variables):
  SET_OUT        folder to write the four images into (required)
  SET_SAMPLES    Cycles samples, default 256 (the finished set used 1024)
  SET_METALLIC   0..1, default 0.0 (soft frame). A metal set uses 1.0 and its own roughness.
  SET_ROUGHNESS  0..1, default 0.65
  SET_CUTS_HOLDOUT  1 (default) keeps the back set as a holdout in the cuts render so its shadows reach the badges; 0 = badges alone
  SET_REFS       optional real-color reference renders, to check the maths: "red:0.8,0.05,0.05;teal:0.02,0.55,0.55"

Works on the in-memory copy only; nothing is saved back to the .blend.
"""
import bpy, os, time
OUT = os.environ["SET_OUT"]; os.makedirs(OUT, exist_ok=True)
SAMPLES = int(os.environ.get("SET_SAMPLES", "256"))
METALLIC = float(os.environ.get("SET_METALLIC", "0.0"))
ROUGHNESS = float(os.environ.get("SET_ROUGHNESS", "0.65"))
BACK = ["_Bezel", "_Standard", "_Text_Plane"]
CUTS = ["Holes", "Backs"]
sc = bpy.context.scene
sc.cycles.samples = SAMPLES
bsdf = bpy.data.materials["Darker"].node_tree.nodes["Principled BSDF"]

# For the cuts layer the back set is kept in the scene as a HOLDOUT: it stays invisible (transparent) but still
# casts shadows and bounces light onto the badge rims, like in the full frame. SET_CUTS_HOLDOUT=0 turns that off.
CUTS_HOLDOUT = os.environ.get("SET_CUTS_HOLDOUT", "1") != "0"

def show(layer):
    for o in sc.objects:
        if o.type != "MESH":
            continue
        if layer == "back":
            o.hide_render = o.name not in BACK
            o.is_holdout = False
        else:
            holdout = CUTS_HOLDOUT and o.name in BACK
            o.hide_render = not (o.name in CUTS or holdout)
            o.is_holdout = holdout

def render(layer, label, rgb):
    show(layer)
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Metallic"].default_value = METALLIC
    bsdf.inputs["Roughness"].default_value = ROUGHNESS
    sc.render.filepath = os.path.join(OUT, f"{layer}_{label}.png")
    t = time.time(); bpy.ops.render.render(write_still=True)
    print(f"RENDERED {layer}_{label} in {time.time()-t:.0f}s", flush=True)

for layer in ("cuts", "back"):          # the quick layer first, so a problem shows up early
    render(layer, "white", (1, 1, 1))
    render(layer, "black", (0, 0, 0))
for ref in filter(None, os.environ.get("SET_REFS", "").split(";")):
    name, rgb = ref.split(":"); rgb = tuple(float(x) for x in rgb.split(","))
    for layer in ("back", "cuts"):
        render(layer, "ref_" + name, rgb)
print("DONE", flush=True)
