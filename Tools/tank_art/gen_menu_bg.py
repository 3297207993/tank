#!/usr/bin/env python3
"""Generate the main-menu cover background (Assets/control/StartScene/start_bg.png).

The art mirrors the game's own flat top-down look: a blue-grey floor lit from
above, thin near-black maze walls (the roadblock prefab is a 0.2 x 3 unit black
bar), dark navy tanks with amber marker lights (same #f2b545 as the UI primary
button) and a calm, darker centre for the start-screen card to sit on.

Usage:
    python gen_menu_bg.py
    python gen_menu_bg.py --out out/start_bg.png
"""
import argparse
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
ASSET = os.path.join(ROOT, "Assets", "control", "StartScene", "start_bg.png")

W, H = 2048, 1280
SS = 2  # supersampling

# Palette (kept in sync with Assets/control/style/TankTheme.uss and the tank sprites)
FLOOR_TOP = (24, 41, 63)
FLOOR_BOTTOM = (34, 56, 82)
WALL = (6, 11, 19)
WALL_LIP = (46, 70, 100)
AMBER = (242, 181, 69)
AMBER_DIM = (196, 138, 46)
AMBER_HOT = (255, 214, 140)

# The start card covers this ellipse; keep it free of maze walls.
CENTER = (W * SS // 2, H * SS // 2)
CLEAR_RX, CLEAR_RY = 390 * SS, 360 * SS


def lerp(a, b, t):
    return tuple(round(x + (y - x) * t) for x, y in zip(a, b))


def vertical_gradient(size, top, bottom):
    w, h = size
    strip = Image.new("RGB", (1, h))
    draw = ImageDraw.Draw(strip)
    for y in range(h):
        t = y / max(1, h - 1)
        draw.point((0, y), lerp(top, bottom, t * t * (3 - 2 * t)))
    return strip.resize((w, h), Image.BILINEAR)


def _falloff(size=512, power=2.0):
    mask = Image.radial_gradient("L").resize((size, size), Image.BILINEAR)
    return mask.point(lambda v: int((1 - v / 255) ** power * 255))


_FALLOFF = _falloff()


def radial_glow(size, center, radius, color, peak=255):
    """Soft round light whose alpha falls off smoothly from the centre."""
    d = max(2, int(radius * 2))
    mask = _FALLOFF.resize((d, d), Image.BILINEAR)
    if peak < 255:
        mask = mask.point(lambda v: v * peak // 255)
    tint = Image.new("RGBA", (d, d), (*color, 255))
    tint.putalpha(mask)
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    layer.alpha_composite(tint, (int(center[0] - radius), int(center[1] - radius)))
    return layer


# ---------------------------------------------------------------- floor ----

def draw_floor_grid(layer):
    """Faint 128px grid, stronger every 512px, like a tactical map."""
    w, h = layer.size
    draw = ImageDraw.Draw(layer)
    minor, major = 128 * SS, 512 * SS
    for x in range(0, w, minor):
        color = (70, 104, 142, 26) if x % major else (92, 130, 170, 46)
        draw.line((x, 0, x, h), fill=color, width=2 * SS)
    for y in range(0, h, minor):
        color = (70, 104, 142, 26) if y % major else (92, 130, 170, 46)
        draw.line((0, y, w, y), fill=color, width=2 * SS)


# ----------------------------------------------------------------- maze ----

def _in_clear_zone(x, y, pad=0):
    dx = (x - CENTER[0]) / (CLEAR_RX + pad)
    dy = (y - CENTER[1]) / (CLEAR_RY + pad)
    return dx * dx + dy * dy <= 1.0


def _maze_edges():
    """Cell-edge wall segments on a 16x10 grid, with the centre carved out."""
    cols, rows = 16, 10
    cw, ch = W * SS / cols, H * SS / rows
    edges = []

    def add(x0, y0, x1, y1):
        mx, my = (x0 + x1) / 2, (y0 + y1) / 2
        if _in_clear_zone(mx, my):
            return
        edges.append((x0, y0, x1, y1))

    # outer border-ish frame, inset so it does not hug the screen edge
    for i in range(cols):
        add(i * cw, 0, (i + 1) * cw, 0)
        add(i * cw, H * SS, (i + 1) * cw, H * SS)
    for j in range(rows):
        add(0, j * ch, 0, (j + 1) * ch)
        add(W * SS, j * ch, W * SS, (j + 1) * ch)

    # random interior walls; sparser near the frame so edges stay readable
    for i in range(1, cols):
        for j in range(rows):
            if random.random() < 0.62:
                x = i * cw
                add(x, j * ch, x, (j + 1) * ch)
    for j in range(1, rows):
        for i in range(cols):
            if random.random() < 0.52:
                y = j * ch
                add(i * cw, y, (i + 1) * cw, y)
    return edges, cw, ch


def draw_maze(canvas):
    edges, cw, ch = _maze_edges()
    walls = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(walls)
    thick = 13 * SS
    r = thick // 2
    for x0, y0, x1, y1 in edges:
        draw.line((x0, y0, x1, y1), fill=(*WALL, 255), width=thick)
        for x, y in ((x0, y0), (x1, y1)):
            draw.ellipse((x - r, y - r, x + r, y + r), fill=(*WALL, 255))

    # A thin lit lip on the upper-left side makes the walls read as standing.
    lip = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ld = ImageDraw.Draw(lip)
    off = 3 * SS
    for x0, y0, x1, y1 in edges:
        ld.line((x0 - off, y0 - off, x1 - off, y1 - off), fill=(*WALL_LIP, 120), width=2 * SS)

    # shadow
    shadow = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    shadow.putalpha(walls.split()[3].filter(ImageFilter.GaussianBlur(10 * SS)).point(
        lambda v: int(v * 0.7)))
    canvas.alpha_composite(shadow, (3 * SS, 7 * SS))
    canvas.alpha_composite(walls)
    canvas.alpha_composite(lip)

    # a few lamp points on wall nodes outside the card area
    lights = []
    nodes = [(x0, y0) for x0, y0, _, _ in edges]
    random.shuffle(nodes)
    placed = 0
    for x, y in nodes:
        if placed >= 8:
            break
        if _in_clear_zone(x, y, pad=170 * SS):
            continue
        draw_lamp(ImageDraw.Draw(canvas), lights, x, y)
        placed += 1
    return lights


def draw_lamp(draw, lights, x, y, r=5 * SS):
    lights.append((x, y, AMBER_HOT, 170, 62 * SS))
    draw.ellipse((x - r, y - r, x + r, y + r), fill=AMBER_HOT)


# ---------------------------------------------------------------- tanks ----

def load_tank_layer(path, angle, size, tint, alpha):
    src = Image.open(path).convert("RGBA").resize(size, Image.LANCZOS)
    src = src.rotate(angle, resample=Image.BICUBIC, expand=True)
    r, g, b, a = src.split()
    boosted = Image.merge("RGBA", (
        r.point(lambda v: min(255, int(v * tint[0]))),
        g.point(lambda v: min(255, int(v * tint[1]))),
        b.point(lambda v: min(255, int(v * tint[2]))),
        a.point(lambda v: int(v * alpha)),
    ))
    return boosted


def place_tank(canvas, hull_path, turret_path, pos, angle, size, alpha, lights):
    # The hull art faces DOWN and the barrel faces LEFT, so the turret needs an
    # extra +90 degrees to line its barrel up with the hull's heading.
    hull = load_tank_layer(hull_path, angle, size, (0.72, 0.78, 0.92), alpha)
    canvas.alpha_composite(hull, (pos[0] - hull.width // 2, pos[1] - hull.height // 2))

    tw = (int(size[0] * 0.95), int(size[1] * 0.95))
    turret = load_tank_layer(turret_path, angle + 90, tw, (0.80, 0.86, 1.0), alpha)
    canvas.alpha_composite(turret, (pos[0] - turret.width // 2, pos[1] - turret.height // 2))

    # The hull faces DOWN at angle 0; rotating the canvas CCW by `angle` turns
    # that heading into (sin, cos) in screen coordinates.
    rad = math.radians(angle)
    lights.append((
        pos[0] + math.sin(rad) * size[0] * 0.36,
        pos[1] + math.cos(rad) * size[0] * 0.36,
        AMBER, 150, 48 * SS,
    ))


# ------------------------------------------------------------ finishing ----

def grain(size, opacity=0.045):
    noise = Image.effect_noise(size, 14).convert("L")
    alpha = noise.point(lambda v: int(abs(v - 128) / 128 * 255 * opacity))
    layer = Image.new("RGBA", size, (0, 0, 0, 255))
    layer.putalpha(alpha)
    return layer


def vignette(size, strength=170):
    mask = Image.radial_gradient("L").resize(size, Image.BILINEAR)
    mask = mask.point(lambda v: int((v / 255) ** 1.5 * strength))
    layer = Image.new("RGBA", size, (3, 7, 13, 255))
    layer.putalpha(mask)
    return layer


def build():
    size = (W * SS, H * SS)
    canvas = vertical_gradient(size, FLOOR_TOP, FLOOR_BOTTOM).convert("RGBA")

    # key light from above, warm fill from below, both very broad
    canvas.alpha_composite(radial_glow(size, (size[0] // 2, -int(H * SS * 0.15)),
                                       int(W * SS * 0.80), (140, 175, 215), peak=54))
    canvas.alpha_composite(radial_glow(size, (int(W * SS * 0.15), int(H * SS * 1.05)),
                                       int(W * SS * 0.70), AMBER_DIM, peak=34))

    grid = Image.new("RGBA", size, (0, 0, 0, 0))
    draw_floor_grid(grid)
    canvas.alpha_composite(grid)

    # calm pool behind the card
    canvas.alpha_composite(radial_glow(size, CENTER, int(W * SS * 0.34),
                                       (120, 160, 205), peak=26))

    lights = draw_maze(canvas)

    # shell trails hint at the game without adding clutter
    trail = Image.new("RGBA", size, (0, 0, 0, 0))
    td = ImageDraw.Draw(trail)
    td.arc((int(W * SS * 0.62), int(H * SS * 0.58), int(W * SS * 1.04), int(H * SS * 1.0)),
           198, 286, fill=(242, 181, 69, 90), width=2 * SS)
    td.arc((int(W * SS * 0.0), int(H * SS * 0.02), int(W * SS * 0.46), int(H * SS * 0.5)),
           38, 122, fill=(242, 181, 69, 70), width=2 * SS)
    canvas.alpha_composite(trail)

    hull = os.path.join(ROOT, "Assets", "game_obj", "tank", "坦克机身.png")
    turret = os.path.join(ROOT, "Assets", "game_obj", "tank", "炮塔.png")
    place_tank(canvas, hull, turret, (268 * SS, 1010 * SS), 42,
               (380 * SS, 380 * SS), 0.95, lights)
    place_tank(canvas, hull, turret, (1782 * SS, 372 * SS), 214,
               (300 * SS, 300 * SS), 0.85, lights)

    for x, y, color, peak, radius in lights:
        canvas.alpha_composite(radial_glow(size, (x, y), radius, color, peak=peak))

    canvas.alpha_composite(vignette(size, strength=175))
    canvas.alpha_composite(grain(size))

    return canvas.convert("RGB").resize((W, H), Image.LANCZOS)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", default=ASSET)
    parser.add_argument("--seed", type=int, default=11)
    args = parser.parse_args()
    random.seed(args.seed)
    img = build()
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    img.save(args.out, optimize=True)
    print(f"wrote {args.out} ({img.width}x{img.height})")


if __name__ == "__main__":
    main()
