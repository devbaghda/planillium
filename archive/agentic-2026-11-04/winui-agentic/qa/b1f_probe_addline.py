# Where is the "bottom-mid" blank point after the add line was scrolled into view (5 extra tags)?  Marks the point
# on a screenshot and repeats the name-box -> point click with real input; reports focus and whether "9" lands.
import sys
from b1f_lib import *
from PIL import ImageDraw, Image
make_root(with_fixture=True)
c = cfg(); c["paid_tags"] += [{"tag": "x%d" % i, "label": "x%d" % i, "monthly_net_eur": 100} for i in range(5)]
json.dump(c, open(CFG, "w"), indent=2)
p = launch()
try:
    bring_front(); dismiss_dialogs(); refresh(); bring_front(); section("Income"); refresh(); time.sleep(1)
    nb = reveal(lambda: fe("NewPaidTagName"))
    P = byid("SectionScroll").rectangle()
    add = [b for b in w().descendants(control_type="Button") if b.element_info.name == "Add"][-1]
    rects = {"name": nb.rectangle(), "rate": fe("NewPaidTagRate").rectangle(), "add": add.rectangle(), "pane": P}
    log("rects", rects)
    pts = [("bottom-mid", P.left + P.width() // 2, P.bottom - 50), ("above-add", (rects["add"].left + rects["add"].right) // 2, rects["name"].top + 8),
           ("right-of-add", rects["add"].right + 30, (rects["add"].top + rects["add"].bottom) // 2), ("below-line", rects["name"].left + 40, rects["name"].bottom + 12)]
    W = w().rectangle()
    shot(os.path.join(HERE, "b1f_probe_addline.png"))
    im = Image.open(os.path.join(HERE, "b1f_probe_addline.png")); d = ImageDraw.Draw(im)
    for nm, x, y in pts:
        d.ellipse((x - W.left - 5, y - W.top - 5, x - W.left + 5, y - W.top + 5), outline=(255, 0, 0), width=2); d.text((x - W.left + 8, y - W.top - 6), nm, fill=(255, 0, 0))
    im.save(os.path.join(HERE, "b1f_probe_addline.png"))
    for nm, x, y in pts:
        log("point", nm, (x, y), "hit", at(x, y))
        res = []
        for hold in (0.0, 0.12, 0.25):
            eclick(fe("NewPaidTagName"), hold); time.sleep(1.0); click(x, y, hold); time.sleep(.5)
            f = focused(); keys("9"); time.sleep(.4); v = val(fe("NewPaidTagName"))
            res.append((hold, f[0] or f[1], v))
            if v: eclick(fe("NewPaidTagName")); keys("{ESC}"); time.sleep(.5)
        log("   ", res)
finally:
    kill()
