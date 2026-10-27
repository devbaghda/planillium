# Debug: why drag-selection checks read '' (YourName, DataInfo) and how the slider thumb is exposed.
from b1f_lib import *
from pywinauto.uia_defines import get_elem_interface
def sel(el):
    try:
        arr = get_elem_interface(el.element_info.element, "Text").GetSelection()
        return [arr.GetElement(i).GetText(-1) for i in range(arr.Length)]
    except Exception as ex: return repr(ex)[:80]
make_root(with_fixture=True)
p = launch()
try:
    bring_front(); dismiss_dialogs(); refresh(); bring_front(); section("General"); refresh(); time.sleep(1)
    yn = byid("YourName"); log("YourName rect", yn.rectangle(), "value", repr(val(yn)))
    eclick(yn); time.sleep(.3); keys("^a{BACKSPACE}Alexander Smith"); time.sleep(.3); log("typed value", repr(val(byid("YourName"))))
    keys("{TAB}"); time.sleep(.6)
    r = byid("YourName").rectangle()
    for y in (r.bottom - 16, r.bottom - 10, (r.top + r.bottom) // 2 + 8):
        drag(r.left + 12, y, r.left + 90, y, steps=20, hold=0.15); time.sleep(.5)
        log("drag y", y, "sel", sel(byid("YourName")), "focus", focused()[:3])
        shot(os.path.join(HERE, "b1f_dbg_yourname_%d.png" % y))
        keys("{TAB}"); time.sleep(.4)
    # slider thumb
    sl = byid("OpacitySlider")
    for d in sl.descendants(): log("slider child", d.element_info.control_type, repr(d.element_info.name), d.element_info.automation_id, d.rectangle())
    section("Data"); refresh(); time.sleep(.8)
    di = byid("DataInfo"); dr = di.rectangle()
    drag(dr.left + 2, dr.top + 9, dr.left + 220, dr.top + 9, steps=20, hold=0.15); time.sleep(.6)
    log("DataInfo sel", sel(byid("DataInfo")), "focus", focused()[:3])
    shot(os.path.join(HERE, "b1f_dbg_datainfo.png"))
finally:
    kill()
