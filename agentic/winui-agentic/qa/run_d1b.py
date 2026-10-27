from common import *
import common
R=[]
def rec(n, ok, ev):
    R.append((n, ok)); print(("%s: %s -- %s" % (n, "PASS" if ok else "FAIL", ev)).encode("ascii","replace").decode(), flush=True)
def run(ntags):
    make_root()
    c = cfg(); c["paid_tags"] = [{"tag": "T%d"%i, "label": "T%d"%i, "monthly_net_eur": 100*(i+1)} for i in range(ntags)]
    json.dump(c, open(CFG,"w"), indent=2)
    p = launch()
    try:
        w = win(p)
        try: w.child_window(auto_id="PrimaryButton", control_type="Button").click_input(); time.sleep(1)
        except Exception: pass
        w = win(p)
        for li in w.descendants(control_type="ListItem"):
            if li.element_info.name == "Income": li.click_input(); break
        time.sleep(1.5)
        wr = w.rectangle(); print("window", wr.width(), wr.height())
        def E(sub): return [x for x in w.descendants(control_type="Edit") if x.element_info.name.startswith(sub)][0]
        n = E("New payable tag"); 
        try: n.iface_scroll_item if False else None
        except Exception: pass
        n.click_input(); send_keys("bad"); time.sleep(.3)
        r = E("Monthly net")
        r.click_input(); send_keys("-500", with_spaces=True, pause=0.05); time.sleep(1.2)
        hs = [x for x in w.descendants() if "negative" in (x.element_info.name or "") and x.rectangle().width()>0]
        info = [(h.rectangle().top, h.rectangle().bottom) for h in hs]
        print("hint rects", info, "window", wr.top, wr.bottom)
        vis = bool(hs) and any(h.rectangle().top >= wr.top+40 and h.rectangle().bottom <= wr.bottom for h in hs)
        try: w.capture_as_image().save("d1b_%dtags.png" % ntags)
        except Exception as ex: print("shot", ex)
        rec("D1-%dtags-hint-in-view" % ntags, vis, "hint rects=%s window=%s-%s box=%r" % (info, wr.top, wr.bottom, E("Monthly net").get_value()))
        # focus still in the box: more typing lands there
        send_keys("7"); time.sleep(.3)
        v = E("Monthly net").get_value()
        rec("D1-%dtags-focus-kept" % ntags, v.replace(" ","") == "-5007", "after typing 7 box=%r" % v)
        # remove minus -> hint gone
        send_keys("{HOME}{DELETE}"); time.sleep(.5)
        gone = not [x for x in w.descendants() if "negative" in (x.element_info.name or "") and x.rectangle().width()>0]
        rec("D1-%dtags-hint-clears" % ntags, gone, "box=%r" % E("Monthly net").get_value())
    finally:
        kill(p)
for n in (4,6):
    run(n); time.sleep(4)
print([r for r in R if not r[1]] or "ALL PASS")
