"""Generate stopwatch+checklist icons and transparent Sauron's eye variants."""
import math, os
from PIL import Image, ImageDraw

OUT = "icon_options"
os.makedirs(OUT, exist_ok=True)

MASTER = 1024


def seg(d, x1, y1, x2, y2, width, fill):
    d.line([(x1, y1), (x2, y2)], fill=fill, width=width)
    r = width // 2
    d.ellipse([x1 - r, y1 - r, x1 + r, y1 + r], fill=fill)
    d.ellipse([x2 - r, y2 - r, x2 + r, y2 + r], fill=fill)


def apply_rounded_square_mask(img, radius_frac=0.22):
    """Apply a rounded-square alpha mask in-place."""
    M = img.width
    mask = Image.new("L", (M, M), 0)
    r = int(M * radius_frac)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, M - 1, M - 1], radius=r, fill=255)
    img.putalpha(mask)
    return img


def apply_circle_mask(img):
    M = img.width
    hi = M * 4
    mask = Image.new("L", (hi, hi), 0)
    ImageDraw.Draw(mask).ellipse([0, 0, hi - 1, hi - 1], fill=255)
    mask = mask.resize((M, M), Image.LANCZOS)
    img.putalpha(mask)
    return img


# ─────────────────────────────────────────────────────────────────────────────
# STOPWATCH + CHECKLIST
# ─────────────────────────────────────────────────────────────────────────────

def make_timer_icon(bg_color, shape="rounded_square"):
    img = Image.new("RGBA", (MASTER, MASTER), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    bg = bg_color + (255,)
    white = (255, 255, 255, 255)

    # Background fill (full square first, mask applied later)
    d.rectangle([0, 0, MASTER - 1, MASTER - 1], fill=bg)

    # ── STOPWATCH ──────────────────────────────────────────────────────────
    sw_cx, sw_cy, sw_r = 375, 570, 255

    # Outer ring (solid white)
    d.ellipse([sw_cx - sw_r, sw_cy - sw_r, sw_cx + sw_r, sw_cy + sw_r], fill=white)

    # Inner face (bg cutout)
    face_r = sw_r - 42
    d.ellipse([sw_cx - face_r, sw_cy - face_r, sw_cx + face_r, sw_cy + face_r], fill=bg)

    # Tick marks on face (white)
    for i in range(12):
        angle = math.radians(i * 30 - 90)
        if i % 3 == 0:
            t_len, t_w = 36, 15
        else:
            t_len, t_w = 20, 9
        r_out = face_r - 10
        r_in  = r_out - t_len
        x1 = sw_cx + r_out * math.cos(angle)
        y1 = sw_cy + r_out * math.sin(angle)
        x2 = sw_cx + r_in  * math.cos(angle)
        y2 = sw_cy + r_in  * math.sin(angle)
        seg(d, x1, y1, x2, y2, t_w, white)

    # Hand (white, ~10-o'clock direction)
    hand_angle = math.radians(-60)
    hand_len   = face_r * 0.65
    hx = sw_cx + hand_len * math.cos(hand_angle)
    hy = sw_cy + hand_len * math.sin(hand_angle)
    seg(d, sw_cx, sw_cy, hx, hy, 17, white)
    # Center hub
    hub_r = 20
    d.ellipse([sw_cx - hub_r, sw_cy - hub_r, sw_cx + hub_r, sw_cy + hub_r], fill=white)

    # Crown (top button)
    cw, ch = 72, 58
    cx0 = sw_cx - cw // 2
    cy0 = sw_cy - sw_r - ch + 8
    d.rounded_rectangle([cx0, cy0, cx0 + cw, cy0 + ch], radius=10, fill=white)

    # Right lug (angled button, top-right)
    lug_cx = sw_cx + int(sw_r * 0.54)
    lug_cy = sw_cy - int(sw_r * 0.73)
    lug_pts = [
        (lug_cx - 11, lug_cy - 26),
        (lug_cx + 23, lug_cy - 11),
        (lug_cx + 11, lug_cy + 26),
        (lug_cx - 23, lug_cy + 11),
    ]
    d.polygon(lug_pts, fill=white)

    # ── CHECKLIST CARD ─────────────────────────────────────────────────────
    cx1, cy1 = 500, 360
    cx2, cy2 = 870, 770
    d.rounded_rectangle([cx1, cy1, cx2, cy2], radius=40, fill=white)

    rows = [470, 565, 660]
    ck_x   = cx1 + 62
    ln_x1  = ck_x + 88
    ln_x2  = cx2 - 45

    for ry in rows:
        # Checkmark in bg color
        ck_w, ck_h = 48, 42
        # short left stroke
        seg(d, ck_x, ry + ck_h * 0.4, ck_x + ck_w * 0.38, ry + ck_h, 18, bg)
        # long right stroke
        seg(d, ck_x + ck_w * 0.38, ry + ck_h, ck_x + ck_w, ry, 18, bg)
        # Horizontal line
        seg(d, ln_x1, ry + 10, ln_x2, ry + 10, 18, bg)

    # Apply shape mask
    if shape == "rounded_square":
        apply_rounded_square_mask(img, 0.22)
    else:
        apply_circle_mask(img)

    return img


timer_variants = [
    ("15_timer_blue",        (25,  90, 180),  "rounded_square"),
    ("16_timer_green",       (15, 100,  48),  "rounded_square"),
    ("17_timer_purple",      (80,  20, 140),  "rounded_square"),
    ("18_timer_navy",        (10,  25,  85),  "rounded_square"),
    ("19_timer_charcoal",    (30,  30,  42),  "rounded_square"),
    ("20_timer_crimson",     (150,  0,  35),  "rounded_square"),
    ("21_timer_teal_circle", (0,  115, 115),  "circle"),
    ("22_timer_blue_circle", (25,  90, 180),  "circle"),
]


# ─────────────────────────────────────────────────────────────────────────────
# SAURON'S EYE  —  transparent background, icon only
# ─────────────────────────────────────────────────────────────────────────────

def make_sauron_eye(
    iris_outer=(255, 140, 0),
    iris_mid=(220, 60, 0),
    iris_inner=(180, 30, 0),
    pupil_col=(10, 5, 0),
    glow_col=None,
    ring_count=4,
    flare=False,
):
    """
    Transparent-bg Sauron eye.
    Eye shape: almond / lens (intersection of two circles).
    Iris: concentric ellipses.
    Pupil: vertical slit.
    Optional outer glow ring.
    """
    img = Image.new("RGBA", (MASTER, MASTER), (0, 0, 0, 0))
    d   = ImageDraw.Draw(img)

    cx, cy = MASTER // 2, MASTER // 2

    # ── Eye almond shape mask ────────────────────────────────────────────
    # Width and height of the almond
    ew = int(MASTER * 0.88)   # eye width
    eh = int(MASTER * 0.46)   # eye height

    eye_mask = Image.new("L", (MASTER, MASTER), 0)
    md = ImageDraw.Draw(eye_mask)
    md.ellipse([cx - ew // 2, cy - eh // 2, cx + ew // 2, cy + eh // 2], fill=255)

    # ── Build iris layers (ellipses, innermost last) ─────────────────────
    # Each ring shrinks by a fraction
    for i in range(ring_count, -1, -1):
        frac = i / ring_count
        rw = int((ew // 2) * (0.25 + 0.75 * frac))
        rh = int((eh // 2) * (0.18 + 0.82 * frac))
        t  = i / ring_count
        r = int(iris_outer[0] * t + iris_inner[0] * (1 - t))
        g = int(iris_outer[1] * t + iris_inner[1] * (1 - t))
        b = int(iris_outer[2] * t + iris_inner[2] * (1 - t))
        d.ellipse([cx - rw, cy - rh, cx + rw, cy + rh], fill=(r, g, b, 255))

    # ── Pupil (vertical slit) ─────────────────────────────────────────────
    pw = int(ew * 0.075)
    ph = int(eh * 0.72)
    # Slit shape: diamond / rounded thin vertical
    slit_pts = [
        (cx,          cy - ph // 2),
        (cx + pw // 2, cy - ph // 6),
        (cx + pw // 2, cy + ph // 6),
        (cx,          cy + ph // 2),
        (cx - pw // 2, cy + ph // 6),
        (cx - pw // 2, cy - ph // 6),
    ]
    d.polygon(slit_pts, fill=pupil_col + (255,))

    # Optional radial flare lines (like fire rays)
    if flare:
        flare_col = iris_outer + (160,)
        for angle_deg in range(0, 360, 30):
            angle = math.radians(angle_deg)
            r1 = int(ew * 0.47)
            r2 = int(ew * 0.56)
            fx1 = cx + r1 * math.cos(angle)
            fy1 = cy + r1 * math.sin(angle) * 0.5  # squish vertically for eye shape
            fx2 = cx + r2 * math.cos(angle)
            fy2 = cy + r2 * math.sin(angle) * 0.5
            seg(d, fx1, fy1, fx2, fy2, 8, flare_col)

    # ── Optional outer glow (soft ring around the almond) ────────────────
    if glow_col:
        glow_layer = Image.new("RGBA", (MASTER, MASTER), (0, 0, 0, 0))
        gd = ImageDraw.Draw(glow_layer)
        for thickness in range(30, 0, -1):
            alpha = int(80 * (1 - thickness / 30))
            gw = ew // 2 + thickness * 3
            gh = eh // 2 + thickness * 3
            gd.ellipse([cx - gw, cy - gh, cx + gw, cy + gh],
                       fill=glow_col + (alpha,))
        img = Image.alpha_composite(glow_layer, img)
        d = ImageDraw.Draw(img)

    # ── Apply almond mask ─────────────────────────────────────────────────
    img.putalpha(eye_mask)
    return img


def make_sauron_eye_on_dark(
    iris_outer=(255, 140, 0),
    iris_mid=(220, 60, 0),
    iris_inner=(180, 30, 0),
    pupil_col=(10, 5, 0),
    bg=(0, 0, 0),
    shape="circle",
):
    """Eye on a solid circular/rounded-square dark background (no transparency on bg)."""
    eye = make_sauron_eye(iris_outer, iris_mid, iris_inner, pupil_col, flare=True)
    img = Image.new("RGBA", (MASTER, MASTER), (0, 0, 0, 0))
    d   = ImageDraw.Draw(img)
    d.rectangle([0, 0, MASTER - 1, MASTER - 1], fill=bg + (255,))
    img.alpha_composite(eye)
    if shape == "circle":
        apply_circle_mask(img)
    else:
        apply_rounded_square_mask(img)
    return img


sauron_variants = [
    # name, iris_outer, iris_mid, iris_inner, pupil, transparent_bg
    ("23_sauron_fire",      (255, 140,   0), (210,  50,   0), (150,  20,   0), (8,  4,  0), True),
    ("24_sauron_gold",      (255, 215,   0), (200, 140,   0), (120,  70,   0), (10, 5,  0), True),
    ("25_sauron_violet",    (200,  80, 255), (130,  20, 200), ( 60,   0, 130), (10, 0, 20), True),
    ("26_sauron_ice",       (120, 220, 255), ( 40, 140, 220), (  0,  60, 160), ( 0, 5, 20), True),
    ("27_sauron_green",     ( 80, 220,  80), ( 20, 160,  20), (  0,  80,   0), ( 0, 8,  0), True),
    ("28_sauron_crimson",   (255,  60,  60), (180,  10,  10), (100,   0,   0), ( 8, 0,  0), True),
    # With dark rounded-square background (icon-format)
    ("29_sauron_dark_fire", (255, 140,   0), (210,  50,   0), (150,  20,   0), (8,  4,  0), False),
    ("30_sauron_dark_void", (200,  80, 255), (130,  20, 200), ( 60,   0, 130), (10, 0, 20), False),
]


# ─────────────────────────────────────────────────────────────────────────────
# GENERATE ALL
# ─────────────────────────────────────────────────────────────────────────────

ICO_SIZES = [16, 24, 32, 48, 64, 128, 256]


def save_icon(img, name):
    png_path = f"{OUT}/{name}.png"
    ico_path = f"{OUT}/{name}.ico"
    img.save(png_path)
    base = img.resize((256, 256), Image.LANCZOS)
    imgs = [base.resize((s, s), Image.LANCZOS) for s in ICO_SIZES]
    imgs[0].save(ico_path, format="ICO", append_images=imgs[1:],
                 sizes=[(s, s) for s in ICO_SIZES])
    print(f"  {name}")


print("Timer icons...")
for name, color, shape in timer_variants:
    save_icon(make_timer_icon(color, shape), name)

print("Sauron's eye icons...")
for entry in sauron_variants:
    name, io, im, ii, pu, transparent = entry
    if transparent:
        icon = make_sauron_eye(io, im, ii, pu, flare=True)
    else:
        # dark bg, with glow
        icon = make_sauron_eye(io, im, ii, pu, glow_col=io, flare=True)
        bg = (8, 4, 0)   # near-black warm
        bg_img = Image.new("RGBA", (MASTER, MASTER), (0, 0, 0, 0))
        ImageDraw.Draw(bg_img).rectangle([0, 0, MASTER-1, MASTER-1], fill=bg + (255,))
        bg_img.alpha_composite(icon)
        icon = bg_img
        apply_rounded_square_mask(icon, 0.22)
    save_icon(icon, name)

print("\nRebuilding preview grid...")

import math as _math

ICON_SZ = 260; PAD = 24; LBL = 38; COLS = 4

files = sorted(f for f in os.listdir(OUT) if f.endswith(".png") and not f.startswith("_"))
ROWS = _math.ceil(len(files) / COLS)
W    = COLS * (ICON_SZ + PAD) + PAD
H    = ROWS * (ICON_SZ + LBL + PAD) + PAD + 10

canvas = Image.new("RGBA", (W, H), (22, 22, 26, 255))
dc     = ImageDraw.Draw(canvas)

from PIL import ImageFont
try:
    font = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 15)
except Exception:
    font = ImageFont.load_default()

for idx, fname in enumerate(files):
    col = idx % COLS; row = idx // COLS
    x = PAD + col * (ICON_SZ + PAD)
    y = PAD + row * (ICON_SZ + LBL + PAD)
    icon = Image.open(os.path.join(OUT, fname)).resize((ICON_SZ, ICON_SZ), Image.LANCZOS)
    # Paste with alpha for transparent icons (checkerboard bg)
    checker = Image.new("RGBA", (ICON_SZ, ICON_SZ), (40, 40, 46, 255))
    checker.alpha_composite(icon)
    canvas.paste(checker, (x, y))
    label = fname.replace(".png", "").replace("_", " ")
    dc.text((x + ICON_SZ // 2, y + ICON_SZ + 6), label,
            fill=(200, 200, 210, 255), font=font, anchor="mt")

out_path = os.path.join(OUT, "_all_preview.png")
canvas.save(out_path)
print(f"Preview saved: {out_path}")
