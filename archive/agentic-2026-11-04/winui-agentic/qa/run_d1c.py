from common import *
import sys
ntags = int(sys.argv[1]); make_root()
c = cfg(); c["paid_tags"] = [{"tag": "T%d"%i, "label": "T%d"%i, "monthly_net_eur": 100*(i+1)} for i in range(ntags)]
json.dump(c, open(CFG,"w"), indent=2)
p = launch()
try:
    w = win(p)
    try: w.child_window(auto_id="PrimaryButton", control_type="Button").click_input(); time.sleep(1)
    except Exception: pass
    w = win(p); w.set_focus(); time.sleep(.5)
    for li in w.descendants(control_type="ListItem"):
        if li.element_info.name == "Income": li.click_input(); break
    time.sleep(1.5)
    def E(sub): return [x for x in w.descendants(control_type="Edit") if x.element_info.name.startswith(sub)][0]
    def hint():
        return [(h.rectangle().top, h.rectangle().bottom) for h in w.descendants() if "negative" in (h.element_info.name or "") and h.rectangle().width()>0]
    n = E("New payable tag")
    try: n.iface_scroll_item.ScrollIntoView()
    except Exception as ex: print("scrollintoview n/a", ex)
    time.sleep(.5)
    n.click_input(); send_keys("bad"); time.sleep(.3)
    r = E("Monthly net"); r.click_input(); send_keys("-500"); 
    for t in (0.3, 1, 3):
        time.sleep(t); print("t+", t, "hint", hint(), "box", E("Monthly net").get_value(), "rate rect", E("Monthly net").rectangle().top, "win", w.rectangle().top, w.rectangle().bottom, flush=True)
    w.capture_as_image().save("d1c_%dtags_a.png" % ntags)
    import pywinauto.mouse as M; M.scroll(coords=(600,500), wheel_dist=-5); time.sleep(.8)
    print("after wheel hint", hint())
    w.capture_as_image().save("d1c_%dtags_b.png" % ntags)
finally:
    kill(p)
