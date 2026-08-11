#!/usr/bin/env python3
"""Generate Homestead's textures: the house marker building sprite (grayscale so
RimWorld's stuff tinting colors it), gizmo icons, and the About preview."""
from PIL import Image, ImageDraw, ImageFont
import os

ROOT = os.path.join(os.path.dirname(__file__), "..")
TEX = os.path.join(ROOT, "Textures", "Homestead")
os.makedirs(os.path.join(TEX, "UI"), exist_ok=True)


def save(img, *path):
    out = os.path.join(*path)
    img.save(out)
    print("wrote", out)


def marker():
    # Top-down welcome mat, grayscale so stuff tinting colors it.
    s = 4  # supersample
    W = 256 * s
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    def px(x):
        return int(x * W / 256)

    left, top, right, bottom = px(14), px(52), px(242), px(204)
    # Mat base with woven border
    d.rounded_rectangle([left, top, right, bottom], radius=px(10),
                        fill=(190, 190, 190, 255), outline=(90, 90, 90, 255), width=px(6))
    d.rounded_rectangle([left + px(14), top + px(14), right - px(14), bottom - px(14)],
                        radius=px(6), outline=(130, 130, 130, 255), width=px(5))
    # Bristle texture: subtle horizontal weave lines
    for y in range(top + px(24), bottom - px(20), px(12)):
        d.line([left + px(22), y, right - px(22), y], fill=(165, 165, 165, 255), width=px(3))
    # Fringe stitches on the short edges
    for y in range(top + px(18), bottom - px(12), px(16)):
        d.line([left + px(4), y, left + px(10), y], fill=(120, 120, 120, 255), width=px(4))
        d.line([right - px(10), y, right - px(4), y], fill=(120, 120, 120, 255), width=px(4))
    # Centered house glyph
    cx, cy = px(128), px(126)
    d.polygon([(cx, cy - px(34)), (cx - px(38), cy - px(2)), (cx + px(38), cy - px(2))],
              fill=(110, 110, 110, 255))
    d.rectangle([cx - px(26), cy - px(2), cx + px(26), cy + px(30)], fill=(140, 140, 140, 255))
    d.rectangle([cx - px(8), cy + px(6), cx + px(8), cy + px(30)], fill=(95, 95, 95, 255))

    img = img.resize((256, 256), Image.LANCZOS)
    save(img, TEX, "HouseMarker.png")


def icon(name, draw_fn):
    s = 4
    W = 64 * s
    img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    draw_fn(d, W)
    img = img.resize((64, 64), Image.LANCZOS)
    save(img, TEX, "UI", name + ".png")


def rename_icon(d, W):
    # Pencil over a tag
    d.rounded_rectangle([W * 0.08, W * 0.55, W * 0.72, W * 0.9], radius=W * 0.05,
                        outline=(255, 255, 255, 255), width=int(W * 0.05))
    d.line([W * 0.3, W * 0.85, W * 0.85, W * 0.3], fill=(255, 255, 255, 255), width=int(W * 0.09))
    d.polygon([(W * 0.85, W * 0.3), (W * 0.95, W * 0.2), (W * 0.9, W * 0.14), (W * 0.8, W * 0.24)],
              fill=(255, 255, 255, 255))


def yard_add_icon(d, W):
    # Dashed field border + plus
    dash = int(W * 0.12)
    step = int(W * 0.2)
    for x in range(int(W * 0.08), int(W * 0.9), step):
        d.line([x, W * 0.1, min(x + dash, W * 0.9), W * 0.1], fill=(255, 255, 255, 255), width=int(W * 0.05))
        d.line([x, W * 0.9, min(x + dash, W * 0.9), W * 0.9], fill=(255, 255, 255, 255), width=int(W * 0.05))
        d.line([W * 0.1, x, W * 0.1, min(x + dash, W * 0.9)], fill=(255, 255, 255, 255), width=int(W * 0.05))
        d.line([W * 0.9, x, W * 0.9, min(x + dash, W * 0.9)], fill=(255, 255, 255, 255), width=int(W * 0.05))
    d.line([W * 0.5, W * 0.28, W * 0.5, W * 0.72], fill=(140, 255, 140, 255), width=int(W * 0.1))
    d.line([W * 0.28, W * 0.5, W * 0.72, W * 0.5], fill=(140, 255, 140, 255), width=int(W * 0.1))


def yard_remove_icon(d, W):
    dash = int(W * 0.12)
    step = int(W * 0.2)
    for x in range(int(W * 0.08), int(W * 0.9), step):
        d.line([x, W * 0.1, min(x + dash, W * 0.9), W * 0.1], fill=(255, 255, 255, 255), width=int(W * 0.05))
        d.line([x, W * 0.9, min(x + dash, W * 0.9), W * 0.9], fill=(255, 255, 255, 255), width=int(W * 0.05))
        d.line([W * 0.1, x, W * 0.1, min(x + dash, W * 0.9)], fill=(255, 255, 255, 255), width=int(W * 0.05))
        d.line([W * 0.9, x, W * 0.9, min(x + dash, W * 0.9)], fill=(255, 255, 255, 255), width=int(W * 0.05))
    d.line([W * 0.28, W * 0.5, W * 0.72, W * 0.5], fill=(255, 140, 140, 255), width=int(W * 0.1))


def expand_doors_icon(d, W):
    # Two rooms with a connecting door and an arrow through it
    d.rectangle([W * 0.06, W * 0.2, W * 0.46, W * 0.8], outline=(255, 255, 255, 255), width=int(W * 0.05))
    d.rectangle([W * 0.54, W * 0.2, W * 0.94, W * 0.8], outline=(255, 255, 255, 255), width=int(W * 0.05))
    d.rectangle([W * 0.42, W * 0.4, W * 0.58, W * 0.6], fill=(0, 0, 0, 0))
    d.line([W * 0.25, W * 0.5, W * 0.68, W * 0.5], fill=(140, 255, 140, 255), width=int(W * 0.07))
    d.polygon([(W * 0.68, W * 0.38), (W * 0.82, W * 0.5), (W * 0.68, W * 0.62)], fill=(140, 255, 140, 255))


def outer_door_icon(d, W):
    # Door leaf with a bold threshold bar: claiming stops at this line
    d.rectangle([W * 0.24, W * 0.12, W * 0.76, W * 0.78], outline=(255, 255, 255, 255), width=int(W * 0.05))
    d.ellipse([W * 0.62, W * 0.42, W * 0.7, W * 0.5], fill=(255, 255, 255, 255))
    d.line([W * 0.1, W * 0.88, W * 0.9, W * 0.88], fill=(255, 190, 120, 255), width=int(W * 0.09))


def preview():
    W, H = 640, 360
    img = Image.new("RGB", (W, H), (38, 34, 30))
    d = ImageDraw.Draw(img)
    # Simple scene: three little houses, one highlighted
    for i, (x, tint) in enumerate([(60, (96, 74, 52)), (250, (120, 92, 62)), (440, (96, 74, 52))]):
        w, h = 150, 130
        y = 150
        d.rectangle([x, y, x + w, y + h], fill=tint, outline=(20, 18, 16), width=4)
        d.polygon([(x - 14, y), (x + w + 14, y), (x + w // 2, y - 62)], fill=(70, 54, 40),
                  outline=(20, 18, 16))
        d.rectangle([x + w // 2 - 18, y + h - 56, x + w // 2 + 18, y + h], fill=(40, 30, 22))
        d.rectangle([x + 18, y + 24, x + 52, y + 58], fill=(210, 190, 120))
        d.rectangle([x + w - 52, y + 24, x + w - 18, y + 58], fill=(210, 190, 120))
    # Highlight ring around middle house
    d.rectangle([236, 74, 416, 292], outline=(140, 220, 140), width=3)
    try:
        font_big = ImageFont.truetype("/usr/share/fonts/TTF/DejaVuSans-Bold.ttf", 52)
        font_small = ImageFont.truetype("/usr/share/fonts/TTF/DejaVuSans.ttf", 22)
    except OSError:
        font_big = ImageFont.load_default()
        font_small = ImageFont.load_default()
    d.text((W // 2, 36), "Homestead", font=font_big, fill=(240, 232, 216), anchor="mm")
    d.text((W // 2, 330), "Houses, homemaking and homeless colonists", font=font_small,
           fill=(200, 190, 170), anchor="mm")
    save(img, os.path.join(ROOT, "About"), "Preview.png")


marker()
icon("Rename", rename_icon)
icon("YardAdd", yard_add_icon)
icon("YardRemove", yard_remove_icon)
icon("ExpandDoors", expand_doors_icon)
icon("OuterDoor", outer_door_icon)
preview()
