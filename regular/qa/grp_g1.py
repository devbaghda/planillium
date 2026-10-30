from grp_h import *
import grp_h as G, re
G.attach(); w().set_focus()
def eur(s):
    s = s.replace(" ", "").replace(" ", "").replace(" ", "").replace("€", "").replace(".", "").replace(",", ".")
    return float(s)
for p in ("Day", "Week", "Month", "Year"):
    G.click(p, "RadioButton", wait=3)
    els = []
    for e in w().descendants(control_type="Text"):
        try: r = e.rectangle(); els.append((e.element_info.name, r.left, r.top))
        except Exception: pass
    hdr = {n: (x, y) for n, x, y in els if n in ("Income lost/gained", "Of which paid work")}
    if len(hdr) < 2: log(p, "HEADERS MISSING", hdr); continue
    for col, (x, y0) in hdr.items():
        cells = sorted([(y, n) for n, xx, y in els if xx == x and y > y0], key=lambda t: t[0])
        # stop at the end of the table: Total row is the one whose row-label is Total
        totaly = [y for n, xx, y in els if n == "Total" and y > y0]
        ty = min(totaly) if totaly else None
        rows = [n for y, n in cells if ty and y < ty]; tot = [n for y, n in cells if y == ty]
        try:
            s = round(sum(eur(v) for v in rows), 2); t = eur(tot[0])
            log(f"{p} | {col} | rows={len(rows)} sum={s} total={t} {'OK' if abs(s-t)<0.005 else 'MISMATCH'} | sample={rows[:3]} tot={tot}")
        except Exception as ex: log(p, col, "parse err", ex, rows[:5], tot)
    labels = [n for n, xx, y in els if xx == 641 and y > 200][:14]
    log(p, "row labels:", labels)
    hd = [n for n, xx, y in els if 150 < y < 230 and xx > 600]
    log(p, "header area texts:", hd[:6])
    shot(f"grp_g1_{p}.png")
