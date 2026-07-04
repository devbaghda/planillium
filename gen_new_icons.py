"""New icon designs inspired by reference images — added to icon_options/."""
import math, os
from PIL import Image, ImageDraw

OUT = "icon_options"
os.makedirs(OUT, exist_ok=True)

M = 1024
H = M // 2   # 512

# ── drawing primitives ────────────────────────────────────────────────────────

def fresh(bg):
    img = Image.new("RGBA", (M, M), (0, 0, 0, 0))
    ImageDraw.Draw(img).ellipse([0, 0, M-1, M-1], fill=bg)
    return img, ImageDraw.Draw(img)

def finalize(img, name):
    hi = M * 4
    mask = Image.new("L", (hi, hi), 0)
    ImageDraw.Draw(mask).ellipse([0, 0, hi-1, hi-1], fill=255)
    mask = mask.resize((M, M), Image.LANCZOS)
    img.putalpha(mask)
    out = img.resize((256, 256), Image.LANCZOS)
    out.save(os.path.join(OUT, name + ".png"))
    szs = [16, 24, 32, 48, 64, 128, 256]
    imgs = [out.resize((s, s), Image.LANCZOS) for s in szs]
    imgs[-1].save(os.path.join(OUT, name + ".ico"), format="ICO",
                  sizes=[(s, s) for s in szs], append_images=imgs[:-1])
    print(f"  {name}")

def seg(d, x1, y1, x2, y2, col, w):
    """Round-capped line segment."""
    x1,y1,x2,y2 = int(x1),int(y1),int(x2),int(y2); w=int(w)
    d.line([(x1,y1),(x2,y2)], fill=col, width=w)
    r=w//2
    for px,py in [(x1,y1),(x2,y2)]:
        d.ellipse([px-r,py-r,px+r,py+r], fill=col)

def dot(d, x, y, r, col):
    x,y,r=int(x),int(y),int(r)
    d.ellipse([x-r,y-r,x+r,y+r], fill=col)

def star5(d, cx, cy, ro, ri, col):
    pts=[]
    for i in range(10):
        a=math.radians(i*36-90)
        r2=ro if i%2==0 else ri
        pts.append((cx+r2*math.cos(a), cy+r2*math.sin(a)))
    d.polygon(pts, fill=col)

# ── generic figure builder ────────────────────────────────────────────────────

def figure(d, fx, fy, fh, col, pose='stand', tilt=0):
    """
    Draw stick figure.  fx,fy = feet centre.  fh = total height.
    tilt: degrees the torso leans (positive = right).
    """
    w  = max(6, int(fh * 0.095))
    hr = int(fh * 0.135)
    tr = math.radians(tilt)

    # key joint positions along the tilted spine
    def spine(frac):
        ox = fh * frac * math.sin(tr)
        oy = fh * frac * math.cos(tr)
        return fx + ox, fy - oy

    foot_l = (fx - fh*0.15, fy)
    foot_r = (fx + fh*0.15, fy)
    hip    = spine(0.36)
    sho    = spine(0.64)
    neck   = spine(0.70)
    hd     = spine(0.86)

    # head
    dot(d, hd[0], hd[1], hr, col)
    # torso
    seg(d, *neck, *hip, col, w)
    # legs
    seg(d, *hip, *foot_l, col, w)
    seg(d, *hip, *foot_r, col, w)

    if pose == 'stand':
        seg(d, *sho, sho[0]-fh*0.22, sho[1]+fh*0.14, col, w)
        seg(d, *sho, sho[0]+fh*0.22, sho[1]+fh*0.14, col, w)

    elif pose == 'arm_up_r':       # right arm raised
        seg(d, *sho, sho[0]+fh*0.28, sho[1]-fh*0.26, col, w)
        seg(d, *sho, sho[0]-fh*0.22, sho[1]+fh*0.12, col, w)

    elif pose == 'arm_up_l':       # left arm raised
        seg(d, *sho, sho[0]-fh*0.28, sho[1]-fh*0.26, col, w)
        seg(d, *sho, sho[0]+fh*0.22, sho[1]+fh*0.12, col, w)

    elif pose == 'both_up':
        seg(d, *sho, sho[0]-fh*0.28, sho[1]-fh*0.26, col, w)
        seg(d, *sho, sho[0]+fh*0.28, sho[1]-fh*0.26, col, w)

    elif pose == 'reach_down_r':   # reaching down-right (helper)
        seg(d, *sho, sho[0]+fh*0.30, sho[1]+fh*0.22, col, w)
        seg(d, *sho, sho[0]-fh*0.22, sho[1]+fh*0.10, col, w)

    elif pose == 'reach_up_r':     # reaching up-right (mentee)
        seg(d, *sho, sho[0]+fh*0.28, sho[1]-fh*0.30, col, w)
        seg(d, *sho, sho[0]-fh*0.18, sho[1]+fh*0.14, col, w)

    elif pose == 'push':           # pushing forward (Sisyphus)
        seg(d, *sho, sho[0]+fh*0.30, sho[1]+fh*0.02, col, w)
        seg(d, *sho, sho[0]+fh*0.26, sho[1]+fh*0.18, col, w)

    elif pose == 'walk':
        seg(d, *sho, sho[0]-fh*0.22, sho[1]+fh*0.14, col, w)
        seg(d, *sho, sho[0]+fh*0.22, sho[1]+fh*0.14, col, w)
        d.polygon([hip, (fx-fh*0.25, fy+fh*0.05), (fx+fh*0.05, fy)], fill=col)
        d.polygon([hip, (fx+fh*0.22, fy-fh*0.08), (fx+fh*0.14, fy)], fill=col)
        return   # skip default legs

def mtn(d, cx, top_y, bot_y, half_w, col):
    d.polygon([(cx-half_w, bot_y), (cx, top_y), (cx+half_w, bot_y)], fill=col)

def flagpole(d, px, py, ph, fw, fh, pc, fc):
    lw = max(8, int(ph*0.045))
    seg(d, px, py, px, py-ph, pc, lw)
    d.polygon([(px,py-ph),(px+fw,py-ph+fh//2),(px,py-ph+fh)], fill=fc)

# ── 8 new designs ─────────────────────────────────────────────────────────────

def d07_summit_together():
    """Two climbers on mountain — white silhouettes, deep teal."""
    img, d = fresh((12, 90, 100, 255))
    # mountain
    mtn(d, H-30, H-280, H+340, 420, (255,255,255,230))
    # snow cap
    mtn(d, H-30, H-280, H-130, 140, (200,235,255,255))
    # flag
    flagpole(d, H-30, H-280, 200, 160, 95, (255,255,255,255), (255,210,30,255))
    # top figure (at summit)
    figure(d, H-30, H-280+15, 145, (255,255,255,255), 'both_up')
    # helper below — reaches up toward summit figure
    figure(d, H-195, H-80,   130, (255,255,255,200), 'reach_up_r')
    finalize(img, "7_summit_together")

def d08_colorful_podium():
    """Colorful figures on podium — inspired by ref 2, navy bg."""
    img, d = fresh((12, 18, 70, 255))
    # podium blocks
    bw = 185
    bot = H + 310
    d.rectangle([H-bw//2,  H+95,  H+bw//2,  bot], fill=(200,220,255,130))  # center (tallest)
    d.rectangle([H+bw//2+10, H+155, H+bw*1.5+10, bot], fill=(150,185,255,100))  # right
    d.rectangle([H-bw*1.5-10, H+205, H-bw//2-10, bot], fill=(130,165,245,90))   # left
    # winner on top — teal/gold
    figure(d, H,         H+95,  280, (60,210,190,255), 'both_up')
    # second — purple
    figure(d, H+bw+55,   H+155, 230, (180,110,240,255), 'arm_up_r')
    # third — white
    figure(d, H-bw-55,   H+205, 200, (255,255,255,210), 'arm_up_l')
    # gold star above winner
    star5(d, H+70, H-100, 75, 32, (255,210,0,255))
    finalize(img, "8_colorful_podium")

def d09_warm_mountain():
    """Warm circle, dark figures, mountain + helper — inspired by ref 3."""
    img, d = fresh((220, 175, 75, 255))
    # sky gradient feel — lighter inner circle
    for i in range(12):
        rr = H - i*22
        a = 12 - i
        d.ellipse([H-rr,H-rr,H+rr,H+rr], fill=(245,215,110,a))
    # mountain
    mtn(d, H+20, H-245, H+330, 400, (45,35,25,230))
    # flag
    flagpole(d, H+20, H-245, 185, 155, 90, (45,35,25,255), (210,60,40,255))
    # figure at summit
    figure(d, H+20, H-245+10, 135, (45,35,25,255), 'both_up')
    # helper pushing up someone from below-left
    figure(d, H-190, H+20,  140, (45,35,25,220), 'reach_up_r', tilt=-12)
    finalize(img, "9_warm_mountain")

def d10_compass_detailed():
    """Detailed compass — dark figure on cream, ref 4 style."""
    img, d = fresh((240, 235, 220, 255))
    col = (28, 24, 18, 255)
    # outer ring
    rr = int(H*0.84)
    d.ellipse([H-rr,H-rr,H+rr,H+rr], outline=col, width=22)
    # 8 tick marks
    for ang in range(0,360,45):
        r2 = math.radians(ang)
        tl = rr*0.11 if ang%90==0 else rr*0.06
        x1,y1 = H+rr*math.cos(r2),  H+rr*math.sin(r2)
        x2,y2 = H+(rr-tl)*math.cos(r2), H+(rr-tl)*math.sin(r2)
        seg(d, x1,y1,x2,y2, col, 18 if ang%90==0 else 10)
    # needle — pointing ~NE (−52°)
    ang = -52
    r2  = math.radians(ang)
    nl  = int(H*0.62)
    sl  = int(H*0.42)
    # north (dark)
    d.polygon([
        (H+nl*math.cos(r2),     H+nl*math.sin(r2)),
        (H+sl*0.22*math.cos(r2+math.pi/2), H+sl*0.22*math.sin(r2+math.pi/2)),
        (H-sl*0.7*math.cos(r2),  H-sl*0.7*math.sin(r2)),
        (H+sl*0.22*math.cos(r2-math.pi/2), H+sl*0.22*math.sin(r2-math.pi/2)),
    ], fill=col)
    # south (lighter)
    r3 = math.radians(ang+180)
    d.polygon([
        (H+sl*0.75*math.cos(r3),  H+sl*0.75*math.sin(r3)),
        (H+sl*0.18*math.cos(r3+math.pi/2), H+sl*0.18*math.sin(r3+math.pi/2)),
        (H-sl*0.35*math.cos(r3),  H-sl*0.35*math.sin(r3)),
        (H+sl*0.18*math.cos(r3-math.pi/2), H+sl*0.18*math.sin(r3-math.pi/2)),
    ], fill=(160,140,100,255))
    # pivot circle
    dot(d, H, H, 38, col)
    dot(d, H, H, 22, (240,235,220,255))
    finalize(img, "10_compass_detailed")

def d11_dynamic_duo():
    """Two outlined figures in motion — dark outline on light, ref 5 style."""
    img, d = fresh((235, 232, 225, 255))
    col = (30, 28, 22, 255)
    # ground
    seg(d, H-380, H+320, H+380, H+320, col, 14)
    # left figure — striding forward (arm fwd)
    figure(d, H-130, H+320, 310, col, 'arm_up_r', tilt=8)
    # right figure — arms out, dynamic
    figure(d, H+120, H+320, 280, col, 'reach_up_r', tilt=-5)
    # motion lines for dynamism
    for i, (ox,oy) in enumerate([(-380,120),(-400,210),(-390,300)]):
        seg(d, H+ox, H+oy, H+ox+70, H+oy, (80,75,65,120-i*20), 10)
    finalize(img, "11_dynamic_duo")

def d12_sisyphus():
    """Sisyphus — blue figure pushing boulder, dark bg — inspired by ref 6."""
    img, d = fresh((8, 8, 18, 255))
    # slope
    slope = [(H-420, H+390), (H+420, H+390), (H+420, H-20)]
    d.polygon(slope, fill=(55, 55, 68, 255))
    # slope highlight edge
    seg(d, H-420, H+390, H+420, H-20, (80,80,95,200), 12)
    # boulder
    br = 170
    bx, by = H+100, H+50
    dot(d, bx, by, br, (90,90,105,255))
    dot(d, bx-40, by-50, 45, (130,130,148,180))   # highlight
    # pusher — blue, leaning hard into boulder
    figure(d, bx-br-30, by+br-40, 285, (55,140,220,255), 'push', tilt=25)
    finalize(img, "12_sisyphus")

def d13_compass_mountain_merge():
    """Compass ring + mountain inside — creative merge, deep navy + gold."""
    img, d = fresh((10, 22, 68, 255))
    col_gold  = (230, 168, 0, 255)
    col_white = (255, 255, 255, 220)
    # compass ring
    rr = int(H*0.83)
    d.ellipse([H-rr,H-rr,H+rr,H+rr], outline=col_gold, width=24)
    # 4 cardinal tick marks
    for ang in (0,90,180,270):
        r2=math.radians(ang)
        seg(d,H+(rr-2)*math.cos(r2),H+(rr-2)*math.sin(r2),
              H+(rr-90)*math.cos(r2),H+(rr-90)*math.sin(r2), col_gold, 20)
    # mountain inside
    mtn(d, H+10, H-260, H+310, 370, col_white)
    mtn(d, H+10, H-260, H-100, 125, (200,225,255,255))  # snow cap
    # compass needle pointing to mountain peak
    seg(d, H, H+200, H+10, H-260, col_gold, 28)
    dot(d, H+10, H-260, 22, col_gold)
    dot(d, H, H+200, 30, col_gold)
    # flag
    flagpole(d, H+10, H-260, 160, 130, 78, col_white, (255,60,60,255))
    finalize(img, "13_compass_mountain")

def d14_mentor_torch():
    """Tall mentor holding torch high, guiding smaller figure — amber on dark."""
    img, d = fresh((18, 12, 6, 255))
    amber = (230, 145, 0, 255)
    white = (255, 255, 255, 220)
    # torch flame glow (concentric)
    tx, ty = H+60, H-340
    for i in range(8, 0, -1):
        rg = i*30
        d.ellipse([tx-rg,ty-rg*2,tx+rg,ty+rg], fill=(220,120,0,18*i))
    # torch flame
    d.polygon([(tx-35,ty+20),(tx+35,ty+20),(tx+15,ty-80),(tx,ty-110),(tx-15,ty-80)],
              fill=(255,180,0,255))
    d.polygon([(tx-20,ty+20),(tx+20,ty+20),(tx+8,ty-50),(tx,ty-70),(tx-8,ty-50)],
              fill=(255,240,100,255))
    # torch pole / arm (part of the mentor figure's arm)
    seg(d, H-55, H-290, tx, ty+20, amber, 22)
    # mentor — tall, gold, arm raised holding torch
    figure(d, H-55, H+300, 380, amber, 'arm_up_r')
    # mentee — white, smaller, beside mentor
    figure(d, H+155, H+300, 260, white, 'arm_up_l')
    # ground
    seg(d, H-400, H+300, H+400, H+300, (80,60,20,120), 12)
    finalize(img, "14_mentor_torch")

# ── build & preview grid ──────────────────────────────────────────────────────

NEW = [
    (d07_summit_together,   "7 — Summit Together"),
    (d08_colorful_podium,   "8 — Colorful Podium"),
    (d09_warm_mountain,     "9 — Warm Mountain"),
    (d10_compass_detailed,  "10 — Compass Detailed"),
    (d11_dynamic_duo,       "11 — Dynamic Duo"),
    (d12_sisyphus,          "12 — Sisyphus"),
    (d13_compass_mountain_merge, "13 — Compass + Mountain"),
    (d14_mentor_torch,      "14 — Mentor & Torch"),
]

print("Generating new icons…")
for fn, _ in NEW:
    fn()

# ── preview grid (new icons only) ────────────────────────────────────────────
from PIL import ImageFont
ICON=260; PAD=24; LBL=36; COLS=4
ROWS = math.ceil(len(NEW)/COLS)
W = COLS*(ICON+PAD)+PAD
H2 = ROWS*(ICON+LBL+PAD)+PAD+10
canvas = Image.new("RGBA", (W, H2), (22,22,26,255))
dc     = ImageDraw.Draw(canvas)
try:    font = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 15)
except: font = ImageFont.load_default()

for idx, (fn, label) in enumerate(NEW):
    col = idx%COLS;  row = idx//COLS
    x = PAD+col*(ICON+PAD);  y = PAD+row*(ICON+LBL+PAD)
    name = label.split("—")[0].strip().replace(" ","_").lower()
    fname = label.split("—")[0].strip().replace(" ","").lower()
    # find the file
    num = label.split("—")[0].strip().replace(" ","")
    parts = label.split("—")
    short = parts[1].strip() if len(parts)>1 else label
    # load saved png
    files = [f for f in os.listdir(OUT) if f.endswith(".png") and f.startswith(num.split(" ")[-1]+"_")]
    if not files:
        files = [f for f in os.listdir(OUT) if f.endswith(".png") and not f.startswith("_")]
        files = sorted(files)
        # pick by index
        files = [files[idx]] if idx < len(files) else []
    if files:
        icon = Image.open(os.path.join(OUT, files[0])).resize((ICON,ICON), Image.LANCZOS)
        canvas.paste(icon,(x,y),icon)
    dc.text((x+ICON//2,y+ICON+6), label, fill=(200,200,210,255), font=font, anchor="mt")

canvas.save(os.path.join(OUT, "_preview_new.png"))
print(f"\nAll saved to ./{OUT}/")
print(f"Preview: {OUT}/_preview_new.png")
