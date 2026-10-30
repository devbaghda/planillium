from grp_h import *
import grp_h as G, sqlite3
G.attach_pid(); w().set_focus()
def db():
    c = sqlite3.connect("file:" + H.ROOT + r"\data\progress.db?mode=ro", uri=True)
    r = c.execute("select category,count(*) from time_diary where window not like '%-%' group by category").fetchall(); c.close(); return r
u = [e for e in w().descendants() if e.element_info.control_type in ("Button", "Hyperlink") and "Undo" in (e.element_info.name or "")]
log("undo controls on Diary:", [(e.element_info.control_type, e.element_info.name) for e in u])
if not u:
    G.nav("Settings"); G.click("Change history", "ListItem")
    u = [e for e in w().descendants() if e.element_info.control_type in ("Button", "Hyperlink") and "Undo" in (e.element_info.name or "")]
    log("undo controls in history:", [(e.element_info.control_type, e.element_info.name) for e in u])
u[0].invoke() if u[0].element_info.control_type == "Hyperlink" else u[0].click_input(); time.sleep(2)
pb = [e for e in w().descendants(control_type="Button") if e.element_info.automation_id == "PrimaryButton"]
if pb: log("undo confirm:", pb[0].element_info.name); pb[0].click_input(); time.sleep(3)
log("after Undo db", db(), [e.element_info.name for e in w().descendants(control_type="Text") if e.element_info.name and ("ndo" in e.element_info.name or "restored" in e.element_info.name.lower())][:4])
shot("grp_g3_undo.png")
