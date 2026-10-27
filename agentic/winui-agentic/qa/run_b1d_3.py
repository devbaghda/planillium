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
def allmsgs(w): return [(n[:60], a) for n, a, _ in texts(w) if any(k in n.lower() for k in ("number", "negative", "more than", "saved", "name"))
                        and not n.startswith("Diary") and not n.startswith("No name set")]
p, w = start()
try:
    top_scroll(w)
    # C: line 5 with clean add line
    def targets(w):
        sal = fe(w, SAL).rectangle()
        head = [t for t in w.descendants(control_type="Text") if t.element_info.automation_id == "IncomeHeading"][0].rectangle()
        lab = [t for t in w.descendants(control_type="Text") if (t.element_info.name or "").startswith("Diary time carrying")][0].rectangle()
        emp = [b for b in w.descendants(control_type="Button") if b.element_info.automation_id == "EmployedToggle"][0].rectangle()
        return {"blank": (sal.right + 300, sal.top + 20), "heading": (head.left + 20, (head.top + head.bottom)//2),
                "label": (lab.left + 40, (lab.top + lab.bottom)//2), "button": ((emp.left+emp.right)//2, (emp.top+emp.bottom)//2),
                "title": (w.rectangle().left + 300, w.rectangle().top + 90)}
    top_scroll(w); cc(fe(w, SAL), "2925"); safe_keys("{ENTER}"); time.sleep(.5)
    for origin in ("salary", "tag", "addline", "none"):
        for tname in ("blank", "heading", "label", "button", "title"):
            top_scroll(w); time.sleep(.3)
            if origin == "salary": cc(fe(w, SAL), "3222")
            elif origin == "tag": cc(fe(w, TAG), "1650")
            elif origin == "addline": cc(fe(w, "NewPaidTagName"), "q")
            x, y = targets(w)[tname]; sclick(x, y); time.sleep(1.0)
            fe_ = focused_edits(w); typ("9"); time.sleep(.8)
            sv, tv = nums(val(fe(w, SAL))), nums(val(fe(w, TAG))); nn = val(fe(w, "NewPaidTagName")); na = val(fe(w, "NewPaidTagRate"))
            ok = (not fe_) and sv == str(salary()) and tv == str(tagrate()[0]) and nn == "" and nums(na) == ""
            rec("C5-%s->%s" % (origin, tname), ok, "focusedEdits=%r salaryBox=%r cfg=%r tagBox=%r cfg=%r newName=%r newAmt=%r" % (fe_, sv, salary(), tv, tagrate(), nn, na))
        top_scroll(w); cc(fe(w, SAL), "2925"); safe_keys("{ENTER}"); time.sleep(.4); cc(fe(w, TAG), "1600"); safe_keys("{ENTER}"); time.sleep(.4)
    print("income:", cfg()["income"], flush=True)
except Abort as ex:
    print('ABORT', ex, flush=True)
finally:
    kill(p)
print('SUMMARY')
for r in R: print(r[0], r[1])
