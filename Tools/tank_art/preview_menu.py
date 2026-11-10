#!/usr/bin/env python3
"""Approximate preview of the start screen over the generated cover image.

It mirrors StartView.uxml / StartView.uss at a 1920x1080 panel so the cover art
can be judged without opening Unity. It is a mock-up, not a renderer: text
metrics come from the same STKAITI font but UI Toolkit may wrap/space slightly
differently.

Usage:
    python preview_menu.py
    python preview_menu.py --bg Assets/control/StartScene/start_bg.png --out out/menu_preview.png
"""
import argparse
import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
BG = os.path.join(ROOT, "Assets", "control", "StartScene", "start_bg.png")
OUT = os.path.join(HERE, "out", "menu_preview.png")
FONT = os.path.join(ROOT, "Assets", "fonts", "STKAITI.TTF")

SCREEN = (1920, 1080)
# PanelSettings StartView.asset: reference 1200x800, scale with screen size,
# match 0.5 -> geometric mean of 1920/1200 and 1080/800.
SCALE = ((SCREEN[0] / 1200) * (SCREEN[1] / 800)) ** 0.5


def px(v):
    return round(v * SCALE)


def cover(img, size):
    """CSS `background-size: cover` equivalent (scale-and-crop)."""
    sw, sh = size
    iw, ih = img.size
    scale = max(sw / iw, sh / ih)
    resized = img.resize((round(iw * scale), round(ih * scale)), Image.LANCZOS)
    x = (resized.width - sw) // 2
    y = (resized.height - sh) // 2
    return resized.crop((x, y, x + sw, y + sh))


def font(size):
    return ImageFont.truetype(FONT, size)


def draw():
    canvas = cover(Image.open(BG).convert("RGBA"), SCREEN).convert("RGBA")
    canvas.alpha_composite(Image.new("RGBA", SCREEN, (8, 14, 24, 76)))  # .start-scrim

    # Draw the card on its own layer and composite it once, so translucent
    # fills (ghost buttons) blend instead of replacing pixels.
    ui = Image.new("RGBA", SCREEN, (0, 0, 0, 0))
    draw = ImageDraw.Draw(ui)

    # .start-card: width 520, padding 52 56 36 56, centered
    card_w = px(520)
    pad_t, pad_r, pad_b, pad_l = px(52), px(56), px(36), px(56)
    title_h = px(68 * 1.2)
    sub_h = px(19 * 1.25)
    sub_mt, sub_mb = px(12), px(44)
    btn_h, btn_gap, btn_font = px(56), px(16), px(30)
    quit_h, quit_mt, quit_font = px(40), px(14), px(22)
    card_h = (pad_t + title_h + sub_mt + sub_h + sub_mb + 2 * (btn_h + btn_gap)
              + quit_mt + quit_h + pad_b)
    cx, cy = SCREEN[0] // 2, SCREEN[1] // 2
    card = (cx - card_w // 2, cy - card_h // 2,
            cx + card_w // 2, cy + card_h // 2)

    radius = px(18)
    draw.rounded_rectangle(card, radius=radius, fill=(13, 23, 39, 209),
                           outline=(255, 255, 255, 26), width=max(1, px(1)))

    y = card[1] + pad_t
    draw.text((cx, y + title_h // 2), "坦克动荡", font=font(px(68)),
              fill=(244, 239, 228, 255), anchor="mm")
    y += title_h + sub_mt
    draw.text((cx, y + sub_h // 2), "和朋友来一场坦克对战", font=font(px(19)),
              fill=(244, 239, 228, 140), anchor="mm")
    y += sub_h + sub_mb

    buttons = [
        ("开房间", (242, 181, 69, 255), (38, 32, 22, 255),
         (255, 214, 140, 217), btn_h, btn_font),
        ("加入房间", (255, 255, 255, 10), (244, 239, 228, 230),
         (255, 255, 255, 56), btn_h, btn_font),
    ]
    for label, fill, fg, border, h, fs in buttons:
        box = (cx - px(300) // 2, y, cx + px(300) // 2, y + h)
        draw.rounded_rectangle(box, radius=px(10), fill=fill, outline=border,
                               width=max(1, px(1)))
        draw.text((cx, y + h // 2), label, font=font(fs), fill=fg, anchor="mm")
        y += h + btn_gap

    y += quit_mt
    qh = quit_h
    box = (cx - px(120) // 2, y, cx + px(120) // 2, y + qh)
    draw.rounded_rectangle(box, radius=px(10), fill=(255, 255, 255, 10),
                           outline=(255, 255, 255, 56), width=max(1, px(1)))
    draw.text((cx, y + qh // 2), "退出", font=font(quit_font),
              fill=(244, 239, 228, 230), anchor="mm")

    canvas.alpha_composite(ui)
    return canvas.convert("RGB")


def main():
    global BG
    parser = argparse.ArgumentParser()
    parser.add_argument("--bg", default=BG)
    parser.add_argument("--out", default=OUT)
    args = parser.parse_args()
    BG = os.path.abspath(args.bg)
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    img = draw()
    img.save(args.out, optimize=True)
    print(f"wrote {args.out} ({img.width}x{img.height})")


if __name__ == "__main__":
    main()
