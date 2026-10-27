# Right-edge strip of the Settings pane (ScrollViewer padding / scrollbar), with and without a scrollable pane.
# For each point: real click in a source box, ~1 s, real click at the point; report the hit element, focus, and
# whether a typed "9" lands in the salary box. Marks the points on a screenshot.
from b1f_lib import *
from PIL import ImageDraw, Image
extra = int(sys.argv[1]) if len(sys.argv) > 1 else 0
make_root(with_fixture=True)
c = cfg(); c["paid_tags"] += [{"tag": "x%d" % i, "label": "x%d" % i, "monthly_net_eur": 100} for i in range(extra)]
json.dump(c, open(CFG, "w"), indent=2)
p = launch()
try:
    bring_front(); dismiss_dialogs(); refresh(); bring_front(); section("Income"); refresh(); time.sleep(1)
    P = byid("SectionScroll").rectangle(); sal = salary_box().rectangle(); W = w().rectangle()
    pts = [("x=right-1 top", P.right - 1, sal.top + 15), ("x=right-2 mid", P.right - 2, (P.top + P.bottom) // 2),
           ("x=right-4 bottom", P.right - 4, P.bottom - 20), ("x=right-6 mid", P.right - 6, (P.top + P.bottom) // 2),
           ("x=right-10 mid", P.right - 10, (P.top + P.bottom) // 2)]
    log("pane", P, "window", W, "extra tags", extra)
    tot = {}
    for nm, x, y in pts:
        res = []
        for i, hold in enumerate((0.0, 0.05, 0.12, 0.25) * 3):
            src = salary_box() if i % 2 else fe("Studioshoo")
            eclick(src, hold); time.sleep(1.0)
            hit = at(x, y)
            click(x, y, hold); time.sleep(.5)
            j = is_salary_focus(); b = val(salary_box()); keys("9"); time.sleep(.4); a = val(salary_box())
            res.append((hold, j, b != a))
            if j or b != a:
                eclick(salary_box(), 0.05); time.sleep(.3); keys("^a{BACKSPACE}2925{ENTER}"); time.sleep(.7)
        bad = sum(1 for r in res if r[1] or r[2])
        tot[nm] = (bad, len(res))
        log("point %-18s %r hit=%r  salary-focus-or-9-landed %d/%d  by hold: %r" % (nm, (x, y), hit, bad, len(res),
            {h: sum(1 for r in res if r[0] == h and (r[1] or r[2])) for h in (0.0, 0.05, 0.12, 0.25)}))
    shot(os.path.join(HERE, "b1f_probe_edge_%d.png" % extra))
    im = Image.open(os.path.join(HERE, "b1f_probe_edge_%d.png" % extra)); d = ImageDraw.Draw(im)
    for nm, x, y in pts: d.line((x - W.left, y - W.top - 12, x - W.left, y - W.top + 12), fill=(255, 0, 0), width=1)
    d.rectangle((P.left - W.left, P.top - W.top, P.right - W.left, P.bottom - W.top), outline=(0, 160, 255))
    im.save(os.path.join(HERE, "b1f_probe_edge_%d.png" % extra))
    log("final config", salary(), tags()[:2])
finally:
    kill()
