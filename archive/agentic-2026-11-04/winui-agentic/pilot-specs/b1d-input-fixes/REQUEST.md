# Request -- B1d: fixes after the eyes-on checks of B1c (Settings -> Income)

1. Empty amount in the add-paid-job line is refused: nothing is added, a message is shown. Same for text that is
   not a number. (A tag with amount 0 must never be saved.)
2. Tab out of the add-line amount box KEEPS what was typed (does not clear). Only Esc or a click outside clear it.
3. The cursor never lands in the salary box unless the user clicked or tabbed into it: not after any click on
   blank space, a heading, a button or another control, and not after leaving any box.
4. Amount "500 123" (typed with a space) must not show "= --/h"; the hourly readout is either correct or hidden.
5. After a refused entry the old "Saved." message is gone.
6. After pressing the Add button with the mouse, no box holds the cursor and the Add button does not keep focus.
7. Leaving an emptied box or one holding "abc" must not leave the box blank while the stored value stays
   different: box and stored value agree on leaving (or the text stays with a visible message, as for minus).
Unchanged: everything in B1, B1b, B1c.
