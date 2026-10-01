# Bug report: Top distractions columns don't line up

User report (2026-10-01, screenshot): in Reports "TOP DISTRACTIONS", the hours figure and the euro
figure on each row start at different x positions row to row (their width varies with the text),
so the hours column and the euro column are ragged. They should form two clean, aligned columns —
hours column aligned on one edge, euro column aligned on one edge — and the bars should all start
and end at the same x on every row. Widest realistic values (e.g. "-€12 345,67", "123,4 h") must
not clip. Check the sibling lists on the page that share this row style.
