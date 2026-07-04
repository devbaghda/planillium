"""Build a clean preview grid from all PNGs in icon_options/."""
import os, math
from PIL import Image, ImageDraw, ImageFont

OUT   = "icon_options"
ICON  = 260; PAD = 24; LBL = 36; COLS = 4

files = sorted(f for f in os.listdir(OUT) if f.endswith(".png") and not f.startswith("_"))
ROWS  = math.ceil(len(files)/COLS)
W     = COLS*(ICON+PAD)+PAD
H     = ROWS*(ICON+LBL+PAD)+PAD+10

canvas = Image.new("RGBA",(W,H),(22,22,26,255))
dc     = ImageDraw.Draw(canvas)
try:    font = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf",15)
except: font = ImageFont.load_default()

for idx,fname in enumerate(files):
    col=idx%COLS; row=idx//COLS
    x=PAD+col*(ICON+PAD); y=PAD+row*(ICON+LBL+PAD)
    icon=Image.open(os.path.join(OUT,fname)).resize((ICON,ICON),Image.LANCZOS)
    canvas.paste(icon,(x,y),icon)
    label=fname.replace(".png","").replace("_"," ")
    dc.text((x+ICON//2,y+ICON+6),label,fill=(200,200,210,255),font=font,anchor="mt")

out=os.path.join(OUT,"_all_preview.png")
canvas.save(out)
print("Saved",out)
