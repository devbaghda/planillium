# G12 acceptance (mechanical)
1. In Settings, type a new value in the AI cap box, press Esc: the box loses focus, shows the last saved value, no validation message remains.
2. Same for the Model box and the API key box (key box: the typed key is discarded, nothing saved).
3. Type -5 in the AI cap box, press Enter (message appears), press Esc: box shows the saved value and the message is gone.
4. Type a new AI cap value, click an empty area of the Settings page (right of and below the boxes): focus leaves the box and the value is saved as before.
5. Income boxes still revert on Esc as before (no regression).
6. Salary box: type 999999999999, press Tab: "Amount is too large", nothing saved, the box shows the previous saved amount; the stored value is unchanged.
7. Salary box: a normal amount (2925) still saves and formats as before.
8. With paid work on several days of one week (amounts with fractions of a cent, for example 33,333 + 33,333 + 32,534), the paid figure is identical to the cent in the Week total, the Month row and the Year row for that week.
9. Strict build 0 warnings, existing tests green.
10. AI -> Model box: type a new model name, click blank page space: the name is saved (reopen Settings: still there) and the cursor is in no box.
11. Click in the AI cap box, type 6, click blank page space; then type 9: nothing changes in any box, including the API key box.
12. Income salary box: click it, press Esc, press Shift+Tab: focus moves to the control before the salary box.
13. Settings -> AI: click the cap box, type 6, click blank space, press Shift+Tab: focus goes to the Model box (never the nav list); Tab after leaving the Income tag box by blank click goes to that tag's Remove button.
14. Settings -> Income: New payable tag name box, type text, Esc: text cleared, focus parked (not on the section list).
15. A click on a NumberBox spin button still changes its value.
