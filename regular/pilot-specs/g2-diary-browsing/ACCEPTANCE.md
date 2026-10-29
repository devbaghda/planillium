# Acceptance -- G2 (scratch MENTOR_ROOT only; never real data)

1. Each of the four filter lists has a "-" entry; choosing it lists exactly the rows with no value for that field;
   combining it with other filters works.
2. The search field chooser lists each searchable field plus "all fields"; searching a field matches only that field.
3. Paging offers graded increments up to "Show all"; counts shown stay correct ("showing N of M").
4. Filters, search and paging keep working together and after returning to the page.
5. The cause of the odd App-filter entries is reported with a proposal; no stored data changed.
6. Build clean (/warnaserror); existing tests pass.
