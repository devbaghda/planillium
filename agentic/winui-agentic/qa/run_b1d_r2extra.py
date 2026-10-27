from b1d_lib import *
SAL = "Previous salary"; TAG = "Studioshoo"
def names(): return [p["tag"] for p in cfg().get("paid_tags", [])]
def top_scroll(w):
    r = w.rectangle()
    for _ in range(6):
        mouse.scroll(coords=(r.left + 500, r.top + 400), wheel_dist=5); time.sleep(.2)
def btn(w, name):
    bs = [b for b in w.descendants(control_type="Button") if b.element_info.name == name and b.rectangle().width() > 0]
    return bs[-1] if bs else None
def reveal(w, el):
    W = w.rectangle()
    for _ in range(25):
        time.sleep(.5); r = el.rectangle()
        if r.width() == 0:
            mouse.scroll(coords=(W.left + 600, W.top + 400), wheel_dist=-3 if _ % 4 < 2 else 3)
        elif r.bottom > W.bottom - 80: mouse.scroll(coords=(W.left + 600, W.top + 400), wheel_dist=-3)
        elif r.top < W.top + 130: mouse.scroll(coords=(W.left + 600, W.top + 400), wheel_dist=2)
        else: break
        time.sleep(.25)
def fill(w, name, amt):
    print("FILL", name, "win", w.rectangle(), "name", fe(w,"NewPaidTagName").rectangle(), "rate", fe(w,"NewPaidTagRate").rectangle(), flush=True)
    reveal(w, fe(w, "NewPaidTagName")); cc(fe(w, "NewPaidTagName"), name)
    reveal(w, fe(w, "NewPaidTagRate")); cc(fe(w, "NewPaidTagRate"), amt)
def hints(w): return [n[:50] for n, a, _ in texts(w) if a == "NewPaidTagHint" and n]
def tg(w):
    sal = fe(w, SAL).rectangle()
    head = [t for t in w.descendants(control_type="Text") if t.element_info.automation_id == "IncomeHeading"][0].rectangle()
    lab = [t for t in w.descendants(control_type="Text") if (t.element_info.name or "").startswith("Diary time carrying")][0].rectangle()
    return {"blank": (sal.right + 300, sal.top + 20), "heading": (head.left + 20, (head.top + head.bottom)//2),
            "label": (lab.left + 40, (lab.top + lab.bottom)//2)}
def refuse_by_mouse(w, name, amt):
    fill(w, name, amt)
    reveal(w, btn(w, "Add")); btn(w, "Add").click_input(); time.sleep(1.0)
p, w = start()
try:
    # extra tags to test next-worse layout
    for n_extra in (0, 4):
        for i in range(n_extra):
            fill(w, "x%d" % i, "100")
            reveal(w, btn(w, "Add")); btn(w, "Add").click_input(); time.sleep(1.0)
        nt = len(names())
        for amt in ("", "abc", "0"):
            for how in ("esc", "blank", "heading", "label", "salarybox", "tagbox"):
                refuse_by_mouse(w, "asd", amt)
                before = (val(fe(w, "NewPaidTagName")), val(fe(w, "NewPaidTagRate")), hints(w), focused_edits(w))
                if how == "esc": safe_keys("{ESC}")
                elif how in ("blank", "heading", "label"):
                    top_scroll(w); x, y = tg(w)[how]; sclick(x, y)
                elif how == "salarybox": top_scroll(w); fe(w, SAL).click_input()
                else: top_scroll(w); fe(w, TAG).click_input()
                time.sleep(1.0)
                after = (val(fe(w, "NewPaidTagName")), nums(val(fe(w, "NewPaidTagRate"))), hints(w), focused_edits(w))
                ok = after[0] == "" and after[1] == "" and not after[2] and "asd" not in names() and len(names()) == nt and salary() == 2925
                rec("B@%dtags-amt%r-%s" % (nt, amt, how), ok, "before=%r after=%r tags=%d sal=%r" % (before, after, len(names()), salary()))
                shot(w, "r2_B_%dtags_%s.png" % (nt, how)) if amt == "" else None
                # reset: leave the app on a clean state
                top_scroll(w); sclick(*blank(w)); time.sleep(.3)
                for k in (SAL, TAG):
                    if val(fe(w, k)) and nums(val(fe(w, k))) not in ("2925", "1600"): rec("leak", False, "box %s=%r" % (k, val(fe(w, k))))
        # after an Esc from a refused Enter in the line (cursor in line): stays working
        fill(w, "asd", ""); reveal(w, fe(w, "NewPaidTagName")); fe(w, "NewPaidTagName").click_input(); safe_keys("{ENTER}"); time.sleep(.8)
        safe_keys("{ESC}"); time.sleep(.8)
        rec("B-enter-refusal-then-esc@%dtags" % nt, val(fe(w, "NewPaidTagName")) == "" and not hints(w), "name=%r hints=%r" % (val(fe(w, "NewPaidTagName")), hints(w)))
        # valid add after a cleared refusal still works
        refuse_by_mouse(w, "asd", ""); safe_keys("{ESC}"); time.sleep(.5)
        fill(w, "good%d" % nt, "300"); reveal(w, btn(w, "Add")); btn(w, "Add").click_input(); time.sleep(1.0)
        rec("add-works-after-refusal@%dtags" % nt, ("good%d" % nt) in names(), "tags=%r focus=%r" % (names(), focused_any(w)))
except Abort as ex:
    import traceback; traceback.print_exc(); print("ABORT (safety guard):", ex, flush=True)
finally:
    kill(p)
print("SUMMARY")
for r in R: print(r[0], r[1])
