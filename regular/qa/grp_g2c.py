from grp_h import *
import grp_h as G
G.attach(); w().set_focus()
def search(field, q):
    c = byname("Search field", "ComboBox"); c.click_input(); time.sleep(1)
    [i for i in w().descendants(control_type="ListItem") if i.element_info.name == field][-1].click_input(); time.sleep(1)
    e = byname("Search the time diary", "Edit"); e.click_input(); send_keys("^a{BACKSPACE}"); send_keys(q, with_spaces=True); time.sleep(2.5)
    return G.showing()
for f, q in (("App","Telegram"),("Page","Telegram"),("Description","chat"),("Page","chat"),("All fields","chat"),("All fields","Word"),("Category","off")):
    log(f"search {f}:{q!r} -> {search(f, q)}")
e = byname("Search the time diary", "Edit"); e.click_input(); send_keys("^a{BACKSPACE}"); time.sleep(2)
log("search cleared:", G.showing())
# paging controls
pg = [(e.element_info.control_type, e.element_info.name) for e in w().descendants() if e.element_info.control_type in ("Button","ComboBox","Hyperlink") and any(k in (e.element_info.name or "") for k in ("Show", "more", "all", "Next", "100", "200"))]
log("paging controls:", pg)
