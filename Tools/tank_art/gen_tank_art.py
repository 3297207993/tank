#!/usr/bin/env python3
"""Generate the flat top-down tank sprites and their editable SVG sources.

The PNGs in Assets/game_obj/tank/ are produced by this script. The SVG files
are the same shapes in vector form, meant for editing in Inkscape / Figma / a
text editor; re-run this script after changing the design instead of exporting
from the SVG by hand, so the runtime PNG stays pixel-consistent.

Canvas conventions (keep them in sync with tank.prefab):
  * 1024x1024, pixels-per-unit 100 in Unity.
  * Hull image faces DOWN; the prefab rotates it 180 degrees.
  * Turret image faces LEFT (barrel points to -X); the prefab rotates it -90
    degrees, so its canvas center becomes the turret pivot.
  * The hull is scaled 0.2 uniformly; the turret is scaled (0.25, 0.2) so a
    circle drawn as an ellipse here (rx = ry * 0.8) stays round in game.

Usage:
    python gen_tank_art.py --out out
    python gen_tank_art.py --out out --format png
    python gen_tank_art.py --out out --format svg
"""
import argparse
import os

from PIL import Image, ImageDraw

W = 1024
SS = 4  # supersampling factor for the PNG output

# Palette (amber matches the UI primary button in Assets/control/style/TankTheme.uss)
OUTLINE = "#0e141f"
TRACK = "#1b2230"
TRACK_IN = "#2c3a4e"
HULL = "#36445b"
PLATE = "#43536d"
DETAIL = "#2a374b"
DARK = "#202a3b"
AMBER = "#f2b545"
FENDER = "#46566f"


class PilPainter:
    """Draws at 4x and lets Save() downsample for anti-aliased PNG output."""

    def __init__(self):
        self.image = Image.new("RGBA", (W * SS, W * SS), (0, 0, 0, 0))
        self.draw = ImageDraw.Draw(self.image)

    def rrect(self, box, radius, fill, outline=None, width=0):
        x0, y0, x1, y1 = (v * SS for v in box)
        self.draw.rounded_rectangle(
            (x0, y0, x1, y1),
            radius=radius * SS,
            fill=fill,
            outline=outline,
            width=width * SS if width else 0,
        )

    def ellipse(self, cx, cy, rx, ry, fill=None, outline=None, width=0):
        box = ((cx - rx) * SS, (cy - ry) * SS, (cx + rx) * SS, (cy + ry) * SS)
        self.draw.ellipse(
            box,
            fill=fill,
            outline=outline,
            width=width * SS if width else 0,
        )

    def save(self, path):
        self.image.resize((W, W), Image.LANCZOS).save(path)


class SvgPainter:
    """Emits the same primitives as SVG elements, in the same draw order."""

    def __init__(self):
        self.parts = []

    def rrect(self, box, radius, fill, outline=None, width=0):
        x0, y0, x1, y1 = box
        attrs = [
            f'x="{x0}"', f'y="{y0}"',
            f'width="{x1 - x0}"', f'height="{y1 - y0}"',
            f'rx="{radius}"', f'fill="{fill}"',
        ]
        if outline and width:
            attrs += [f'stroke="{outline}"', f'stroke-width="{width}"']
        self.parts.append("<rect " + " ".join(attrs) + "/>")

    def ellipse(self, cx, cy, rx, ry, fill=None, outline=None, width=0):
        attrs = [
            f'cx="{cx}"', f'cy="{cy}"',
            f'rx="{rx}"', f'ry="{ry}"',
            f'fill="{fill or "none"}"',
        ]
        if outline and width:
            attrs += [f'stroke="{outline}"', f'stroke-width="{width}"']
        self.parts.append("<ellipse " + " ".join(attrs) + "/>")

    def save(self, path):
        lines = [
            f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{W}" '
            f'viewBox="0 0 {W} {W}">'
        ]
        lines += ["  " + part for part in self.parts]
        lines.append("</svg>")
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(lines) + "\n")


def draw_hull(p):
    # Tracks
    for x0, x1 in ((180, 304), (720, 844)):
        p.rrect((x0, 170, x1, 854), 56, TRACK, OUTLINE, 14)
        y = 238
        while y <= 782:
            p.rrect((x0 + 20, y, x1 - 20, y + 22), 10, TRACK_IN)
            y += 76

    # Hull body
    p.rrect((296, 154, 728, 870), 80, HULL, OUTLINE, 20)

    # Rear engine deck (top of the image)
    p.rrect((392, 198, 632, 372), 30, DETAIL, OUTLINE, 10)
    for i in range(3):
        y = 236 + i * 52
        p.rrect((424, y, 600, y + 24), 9, DARK)

    # Side fenders
    for x in (330, 694):
        p.rrect((x, 250, x + 16, 780), 8, FENDER)

    # Front glacis plate (bottom of the image)
    p.rrect((346, 704, 678, 840), 34, PLATE, OUTLINE, 10)
    p.rrect((438, 750, 586, 784), 11, AMBER, OUTLINE, 6)
    for cx in (398, 626):
        p.ellipse(cx, 736, 14, 14, fill="#cdd7e6", outline=OUTLINE, width=6)

    # Turret ring
    p.ellipse(512, 512, 200, 200, fill=DARK, outline=OUTLINE, width=12)
    p.ellipse(512, 512, 184, 184, outline="#3d4c63", width=8)


def draw_turret(p):
    # Gun barrel points left; the prefab rotates this canvas -90 degrees.
    p.rrect((40, 462, 392, 562), 26, "#2d3a4f", OUTLINE, 14)
    p.rrect((40, 448, 116, 576), 24, "#1f2938", OUTLINE, 14)
    p.rrect((330, 462, 368, 562), 10, AMBER, OUTLINE, 6)

    # Turret disc: rx = ry * 0.8 so the non-uniform prefab scale keeps it round.
    p.ellipse(512, 512, 140, 175, fill=HULL, outline=OUTLINE, width=16)
    p.ellipse(512, 512, 106, 134, outline="#2a374b", width=10)
    p.ellipse(512, 512, 48, 60, fill=PLATE, outline=OUTLINE, width=8)
    p.ellipse(512, 512, 17, 21, fill="#1f2938")
    p.ellipse(430, 512, 16, 20, fill=AMBER, outline=OUTLINE, width=6)


PAINTERS = {"png": PilPainter, "svg": SvgPainter}
PAINTER_EXT = {"png": ".png", "svg": ".svg"}
PAINTERS_BY_NAME = {"hull": draw_hull, "turret": draw_turret}


def main():
    parser = argparse.ArgumentParser(description="Generate tank hull/turret art.")
    parser.add_argument("--out", default="out", help="output directory (default: out)")
    parser.add_argument(
        "--format",
        choices=("png", "svg", "both"),
        default="both",
        help="output format (default: both)",
    )
    args = parser.parse_args()

    os.makedirs(args.out, exist_ok=True)
    formats = ("png", "svg") if args.format == "both" else (args.format,)
    for fmt in formats:
        painter_cls = PAINTERS[fmt]
        for name, draw in PAINTERS_BY_NAME.items():
            painter = painter_cls()
            draw(painter)
            path = os.path.join(args.out, name + PAINTER_EXT[fmt])
            painter.save(path)
            print("wrote", path)


if __name__ == "__main__":
    main()
