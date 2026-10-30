from grp_h import *
import grp_h as G, sqlite3, glob
G.attach_pid(); w().set_focus()
try: [e for e in w().descendants(control_type="Button") if e.element_info.automation_id=="CloseButton"][0].click_input(); time.sleep(1.5)
except Exception: pass
def db():
    c = sqlite3.connect("file:" + H.ROOT + r"\data\progress.db?mode=ro", uri=True)
    r = c.execute("select category,count(*) from time_diary where window in ('Untitled window','Settings') or window not like '%-%' group by category").fetchall(); c.close(); return r
G.nav("Reports"); time.sleep(2); G.wheel_down(25)
cb = byname("Apply filters across the last 90 days instead of just this day", "CheckBox")
if cb.get_toggle_state() != 1: cb.click_input(); time.sleep(2)
G.toggle("app", "Other"); log("Other:", G.showing(), G.tcount(), "db before", db())
bk0 = glob.glob(H.ROOT + r"\data\**\*backup*", recursive=True) + glob.glob(H.ROOT + r"\**\*.bak", recursive=True)
b = [e for e in w().descendants(control_type="Button") if (e.element_info.name or "").startswith("Mark on-plan")][0]; b.click_input(); time.sleep(2)
dl = [e.element_info.name for e in w().descendants(control_type="Text") if e.element_info.name and any(ch.isdigit() for ch in e.element_info.name)]
log("confirm dialog texts:", [x for x in dl if any(k in x.lower() for k in ("fill", "overwr", "unchang", "change", "row"))][:8]); shot("grp_g3_confirm.png")
log("dialog buttons:", [e.element_info.name for e in w().descendants(control_type="Button")][3:9])
G.click("Cancel", "Button", wait=2); log("after Cancel db", db())
b = [e for e in w().descendants(control_type="Button") if (e.element_info.name or "").startswith("Mark on-plan")][0]; b.click_input(); time.sleep(2)
pb = [e for e in w().descendants(control_type="Button") if e.element_info.automation_id == "PrimaryButton"]; log("primary:", pb[0].element_info.name if pb else None)
pb[0].click_input(); time.sleep(3)
log("after Mark db", db(), "| status:", [e.element_info.name for e in w().descendants(control_type="Text") if e.element_info.name and ("Undo" in e.element_info.name or "marked" in e.element_info.name.lower() or "Marked" in e.element_info.name)][:3])
bk1 = [p for p in glob.glob(H.ROOT + r"\**\*", recursive=True) if "back" in p.lower()]
log("backup files:", bk1)
G.nav("Today"); G.nav("Reports"); time.sleep(2); G.wheel_down(25); log("after return: buttons", G.tcount(), G.showing())
shot("grp_g3_after.png")
