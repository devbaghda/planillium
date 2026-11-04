from b1f_lib import *
make_root(with_fixture=False)
p = launch()
try:
    bring_front(); dismiss_dialogs(); refresh(); bring_front()
    log("window", w().rectangle(), "fg", fg_ok(), "focused", focused())
    section("Income"); refresh()
    for e in edits(): log("EDIT", repr(e.element_info.name), e.element_info.automation_id, e.rectangle())
    sc = byid("SectionScroll"); log("SectionScroll", sc.rectangle() if sc else None)
    for t in w().descendants(control_type="Text"):
        if t.rectangle().width() > 0: log("TEXT", repr((t.element_info.name or "")[:50]), t.element_info.automation_id, t.rectangle())
    for b in w().descendants(control_type="Button"):
        if b.rectangle().width() > 0: log("BTN", repr(b.element_info.name), b.element_info.automation_id, b.rectangle())
    r = sc.rectangle()
    for fy in (0.02, 0.1, 0.2, 0.3, 0.45, 0.6, 0.75, 0.9, 0.98):
        row = []
        for fx in (0.02, 0.2, 0.4, 0.6, 0.8, 0.97):
            x = int(r.left + fx * r.width()); y = int(r.top + fy * r.height()); row.append((x, y, at(x, y)))
        log("HIT", row)
    shot(os.path.join(HERE, "b1f_explore.png"))
finally:
    kill()
