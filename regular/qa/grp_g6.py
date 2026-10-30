from grp_h import *
import grp_h as G, datetime, re
H.kill()
sd = (datetime.date.today() - datetime.timedelta(days=3)).isoformat()
plan = {"id": "qa-plan", "name": "QA plan", "start_date": sd, "total_days": 6, "phases": [{"phase": 1, "name": "P1", "tasks": [
    {"day": 1, "task": "Overdue QA task", "category": "work", "duration_min": 30},
    {"day": 4, "task": "Today QA task", "category": "work", "duration_min": 30},
    {"day": 5, "task": "Tomorrow QA task", "category": "work", "duration_min": 30}]}]}
json.dump(plan, open(H.ROOT + r"\plans\active\qa-plan.json", "w"))
t = open(H.CFG, encoding="utf-8").read()
t = re.sub(r'"spend_month": "[^"]*", "spend_eur": [0-9.]+', '"spend_month": "2026-09", "spend_eur": 99', t)
t = re.sub(r'"cap_eur": [0-9.]+', '"cap_eur": 3', t)
open(H.CFG, "w", encoding="utf-8").write(t)
G.relaunch(); G.attach_pid(); w().set_focus(); G.skip_welcome()
G.nav("Today"); time.sleep(2)
def todayitem():
    it = byname("Today", "ListItem"); return [(d.element_info.control_type, d.element_info.name, d.element_info.automation_id) for d in it.descendants()]
log("Today item parts:", todayitem())
log("banner texts:", texts("overdue") + texts("Overdue"))
G.dump(G.S + r"\today2.txt"); shot("grp_g6_today.png")
G.nav("Reports"); log("on Reports, Today item parts:", todayitem()); shot("grp_g6_reports.png")
G.nav("Settings"); G.click("AI", "ListItem"); log("month boundary:", texts("Spent this month"))
