# Request -- G2 Diary browsing (user, 2026-10-23)

Reports page, Diary section.
1. Filters (Category, App, Page, Tag): every filter list gets a "-" entry that shows the rows that were never
   categorized / have no value for that field.
2. Search bar: the user chooses which field to search in, including "all fields" (default = current behaviour).
3. Paging: next to "Show 50 more", graded options (100 / 500 / 1000 ...) up to "Show all".
4. INVESTIGATE ONLY: the App filter list contains strange records ("74% complete", "99% complete", "Add a device",
   "Add Conditional Column", "1-Click Cleaner v1.1") -- window titles / page names ending up as app names. Find the
   cause and report it with a proposed fix; do not change stored data and do not filter anything out until the user
   decides what counts as an app.
Not to change: marking behaviour, scoring, how diary rows are recorded.
