# Acceptance -- B1c (scratch MENTOR_ROOT only; never real data)

Fixture: salary 2925, tag Studioshoo 1600/month.

1. Type 3000 in the salary box, press Enter: config.json holds 3000; focus is on no money box (and not on the
   salary box); typing "9" afterwards changes nothing.
2. Same with the Studioshoo box.
3. Type 3100 in the salary box, press Tab: config.json holds 3100; focus does not land in the salary box.
4. Same by clicking blank space, and by clicking another box (focus then goes there, as expected).
5. Add line: name "zed", amount 500, press Enter in the name box and, separately, in the amount box: tag zed with
   500 is added exactly as with the Add button; afterwards no box holds the cursor.
6. Add line with an invalid amount (minus/empty) and Enter: nothing is added, message visible.
7. Type "-500" in any money box: the box shows "-500" (sign kept); message visible; config.json unchanged.
8. Continue typing digits after the minus: box keeps the sign and digits; message still visible.
9. Delete the minus: the box holds a valid number; message disappears.
10. With "-500" in the box, press Tab / click elsewhere: text and message stay; config.json holds the old value.
11. Esc or click outside the add line still clears it without adding a tag.
12. Message is readable at a glance in both apps (screenshot compared with the agentic B1b one as baseline).
13. Thin-space formatting, no spinners (B1 lines 6-8) still hold.
14. Build clean (/warnaserror), tests pass, QA covers lines 1-10.
