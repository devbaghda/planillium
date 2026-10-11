# Bug report: working inside Planillium is logged as an absence

User report (2026-10-01): "I was working at the PC in Planillium, but it counts as absence." The
user was actively using Planillium's own window (e.g. typing/clicking in the app), and the
"Welcome back — you were away N min" dialog later appeared for that period.

Find why activity inside Planillium's own window isn't counted as presence, fix it so time spent
actively using Planillium is not treated as idle/absent, and make sure real absence (no input at
all) is still detected as before. Cause is not known — investigate the idle/activity detection.
