"""Gradient stopwatch+checklist icons — transparent bg, matching reference style."""
import math, os
from PIL import Image, ImageDraw, ImageFont

OUT = "icon_options"
MASTER = 1024


def seg(d, x1, y1, x2, y2, width, fill):
    d.line([(x1, y1), (x2, y2)], fill=fill, width=width)
    r = width // 2
    d.ellipse([x1 - r, y1 - r, x1 + r, y1 + r], fill=fill)
    d.ellipse([x2 - r, y2 - r, x2 + r, y2 + r], fill=fill)


def lerp_color(c1, c2, t):
    return tuple(int(c1[i] + (c2[i] - c1[i]) * t) for i in range(3))


def make_gradient_timer(color_top, color_bottom, face_alpha=170):
    img = Image.new("RGBA", (MASTER, MASTER), (0, 0, 0, 0))

    # Build gradient strip
    grad = Image.new("RGBA", (MASTER, MASTER), (0, 0, 0, 0))
    gd = ImageDraw.Draw(grad)
    for y in range(MASTER):
        t = y / (MASTER - 1)
        r, g, b = lerp_color(color_top, color_bottom, t)
        gd.line([(0, y), (MASTER - 1, y)], fill=(r, g, b, 255))

    # Lighter gradient for watch face (blended toward white)
    grad_light = Image.new("RGBA", (MASTER, MASTER), (0, 0, 0, 0))
    gld = ImageDraw.Draw(grad_light)
    for y in range(MASTER):
        t = y / (MASTER - 1)
        base = lerp_color(color_top, color_bottom, t)
        light = lerp_color(base, (255, 255, 255), 0.38)
        gld.line([(0, y), (MASTER - 1, y)], fill=light + (255,))

    sw_cx, sw_cy, sw_r = 375, 570, 255
    face_r = sw_r - 42
    cx1, cy1, cx2, cy2 = 500, 360, 870, 770

    def paint_mask(source, mask_img, alpha_override=None):
        layer = source.copy()
        if alpha_override is not None:
            layer.putalpha(alpha_override)
        else:
            layer.putalpha(mask_img)
        img.alpha_composite(layer)

    # ── 1. Watch face (lighter gradient, behind ring) ───────────────────
    mask_face = Image.new("L", (MASTER, MASTER), 0)
    ImageDraw.Draw(mask_face).ellipse(
        [sw_cx - face_r, sw_cy - face_r, sw_cx + face_r, sw_cy + face_r], fill=255)
    paint_mask(grad_light, mask_face)

    # ── 2. Watch ring + crown + lug (main gradient, opaque) ─────────────
    mask_ring = Image.new("L", (MASTER, MASTER), 0)
    mr = ImageDraw.Draw(mask_ring)
    # Full circle
    mr.ellipse([sw_cx - sw_r, sw_cy - sw_r, sw_cx + sw_r, sw_cy + sw_r], fill=255)
    # Punch out the face
    mr.ellipse([sw_cx - face_r, sw_cy - face_r, sw_cx + face_r, sw_cy + face_r], fill=0)
    # Crown
    cw, ch = 72, 58
    cx0 = sw_cx - cw // 2
    cy0 = sw_cy - sw_r - ch + 8
    mr.rounded_rectangle([cx0, cy0, cx0 + cw, cy0 + ch], radius=10, fill=255)
    # Lug
    lug_cx = sw_cx + int(sw_r * 0.54)
    lug_cy = sw_cy - int(sw_r * 0.73)
    lug_pts = [
        (lug_cx - 11, lug_cy - 26), (lug_cx + 23, lug_cy - 11),
        (lug_cx + 11, lug_cy + 26), (lug_cx - 23, lug_cy + 11),
    ]
    mr.polygon(lug_pts, fill=255)
    paint_mask(grad, mask_ring)

    # ── 3. Card shadow (dark halo behind card for depth) ─────────────────
    shadow = Image.new("RGBA", (MASTER, MASTER), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)
    sd.rounded_rectangle([cx1 - 8, cy1 + 8, cx2 + 8, cy2 + 8], radius=44, fill=(0, 0, 0, 80))
    img.alpha_composite(shadow)

    # ── 4. Checklist card (main gradient, opaque) ────────────────────────
    mask_card = Image.new("L", (MASTER, MASTER), 0)
    ImageDraw.Draw(mask_card).rounded_rectangle([cx1, cy1, cx2, cy2], radius=40, fill=255)
    paint_mask(grad, mask_card)

    # ── WHITE DETAILS ───────────────────────────────────────────────────
    wd = ImageDraw.Draw(img)
    white = (255, 255, 255, 255)

    # Tick marks
    for i in range(12):
        angle = math.radians(i * 30 - 90)
        t_len, t_w = (36, 15) if i % 3 == 0 else (20, 9)
        r_out = face_r - 10
        r_in  = r_out - t_len
        seg(wd,
            sw_cx + r_out * math.cos(angle), sw_cy + r_out * math.sin(angle),
            sw_cx + r_in  * math.cos(angle), sw_cy + r_in  * math.sin(angle),
            t_w, white)

    # Hand
    ha = math.radians(-60)
    hl = face_r * 0.65
    seg(wd, sw_cx, sw_cy, sw_cx + hl * math.cos(ha), sw_cy + hl * math.sin(ha), 17, white)
    hr = 20
    wd.ellipse([sw_cx - hr, sw_cy - hr, sw_cx + hr, sw_cy + hr], fill=white)

    # Checkmarks and lines on card
    rows = [470, 565, 660]
    ck_x  = cx1 + 62
    ln_x1 = ck_x + 88
    ln_x2 = cx2 - 45
    for ry in rows:
        ck_w, ck_h = 48, 42
        seg(wd, ck_x, ry + ck_h * 0.4, ck_x + ck_w * 0.38, ry + ck_h, 18, white)
        seg(wd, ck_x + ck_w * 0.38, ry + ck_h, ck_x + ck_w, ry, 18, white)
        seg(wd, ln_x1, ry + 10, ln_x2, ry + 10, 18, white)

    return img


# ─────────────────────────────────────────────────────────────────────────────
# Color variants  (top_color, bottom_color)
# ─────────────────────────────────────────────────────────────────────────────

variants = [
    ("31_grad_teal",        (0,  128, 128),  (0,  210, 210)),   # original style
    ("32_grad_ocean_blue",  (10,  60, 160),  (20, 180, 230)),
    ("33_grad_royal_blue",  (30,  50, 200),  (80, 140, 255)),
    ("34_grad_purple",      (90,  10, 160),  (180, 60, 255)),
    ("35_grad_magenta",     (160,  0, 120),  (255, 80, 200)),
    ("36_grad_crimson_red", (160,  0,  30),  (255,  80,  60)),
    ("37_grad_amber",       (180,  80,  0),  (255, 190,  30)),
    ("38_grad_forest",      (10,  80,  30),  (40,  200, 100)),
    ("39_grad_midnight",    (15,  15,  50),  (40,  100, 200)),
    ("40_grad_slate_violet",(40,  40,  90),  (100, 100, 220)),
    ("41_grad_rose_gold",   (180, 80,  60),  (255, 180, 160)),
    ("42_grad_cyan_lime",   (0,  140,  80),  (80,  240, 180)),
]

ICO_SIZES = [16, 24, 32, 48, 64, 128, 256]

print("Generating gradient timer icons...")
for name, top, bot in variants:
    img = make_gradient_timer(top, bot)
    img.save(f"{OUT}/{name}.png")
    base = img.resize((256, 256), Image.LANCZOS)
    ico_imgs = [base.resize((s, s), Image.LANCZOS) for s in ICO_SIZES]
    ico_imgs[0].save(f"{OUT}/{name}.ico", format="ICO",
                     append_images=ico_imgs[1:], sizes=[(s, s) for s in ICO_SIZES])
    print(f"  {name}")

# ─────────────────────────────────────────────────────────────────────────────
# Preview grid — only the new gradient ones
# ─────────────────────────────────────────────────────────────────────────────

print("\nBuilding gradient preview...")
import math as _math

files = sorted(f for f in os.listdir(OUT)
               if f.endswith(".png") and "_grad_" in f)

ICON_SZ = 280; PAD = 24; LBL = 38; COLS = 3
ROWS = _math.ceil(len(files) / COLS)
W    = COLS * (ICON_SZ + PAD) + PAD
H    = ROWS * (ICON_SZ + LBL + PAD) + PAD + 10

canvas = Image.new("RGBA", (W, H), (22, 22, 26, 255))
dc     = ImageDraw.Draw(canvas)

try:    font = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 15)
except: font = ImageFont.load_default()

# Checkerboard bg for transparent icons
def checker_bg(size):
    sq = 16
    bg = Image.new("RGB", (size, size), (80, 80, 80))
    bd = ImageDraw.Draw(bg)
    for row in range(size // sq + 1):
        for col in range(size // sq + 1):
            if (row + col) % 2 == 0:
                bd.rectangle([col*sq, row*sq, col*sq+sq-1, row*sq+sq-1], fill=(100, 100, 100))
    return bg.convert("RGBA")

for idx, fname in enumerate(files):
    col = idx % COLS; row = idx // COLS
    x = PAD + col * (ICON_SZ + PAD)
    y = PAD + row * (ICON_SZ + LBL + PAD)
    icon = Image.open(f"{OUT}/{fname}").resize((ICON_SZ, ICON_SZ), Image.LANCZOS)
    bg   = checker_bg(ICON_SZ)
    bg.alpha_composite(icon)
    canvas.paste(bg, (x, y))
    label = fname.replace(".png", "").replace("_", " ").replace("grad", "").strip()
    dc.text((x + ICON_SZ // 2, y + ICON_SZ + 6), label,
            fill=(200, 200, 210, 255), font=font, anchor="mt")

out_path = f"{OUT}/_gradient_preview.png"
canvas.save(out_path)
print(f"Saved: {out_path}")
