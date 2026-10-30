import sqlite3
c=sqlite3.connect(r"C:\Users\devba\AppData\Local\Temp\claude\d--CLAUDE-Planillium\56fe036f-c245-4174-a179-705bf772c126\scratchpad\mroot-g12\data\progress.db",timeout=20)
rows=[]
for day in ("2026-10-26","2026-10-27","2026-10-28","2026-10-29","2026-10-30"):
    rows+=[(day,"08:30","09:15",45,"paid","Client call - Zoom","","Studio"),
           (day,"09:15","09:50",35,"off_plan","Family chat - Telegram","",None),
           (day,"09:50","10:20",30,"neutral","Inbox - Mozilla Firefox","",None),
           (day,"10:20","10:45",25,"off_plan","Marco - Work - Telegram","",None)]
rows.append(("2026-10-30","10:45","10:55",10,"neutral","","",None))
c.executemany("INSERT INTO time_diary(date,start_time,end_time,duration_min,category,window,description,tag) VALUES(?,?,?,?,?,?,?,?)",rows)
c.commit(); print(c.execute("select count(*) from time_diary").fetchone())
