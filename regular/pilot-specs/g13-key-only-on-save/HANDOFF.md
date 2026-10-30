# G13 hand-off (regular)
Precheck: strict Debug build 0 warnings 0 errors; suite 229/229 green.
Finding: rule 1 defect does NOT exist here. Enter/leave in Model and cap boxes already save only that box (SaveModelFromBox / SaveCapFromBox; Page_PreviewKeyDown). The key is read only in AiSave_Click (Save button); the key box has no Enter-to-save and leaving it by Tab/click stores nothing.
Gap vs rule 2: a blank-page click left the typed key in the box, and a later Save click stored it. Esc already emptied it.
Change: SettingsPage.xaml.cs Page_PointerPressed: blank click while the key box has text empties it (one line). Nothing else touched.
Behaviour for ACCEPTANCE 5: leaving the key box by Tab/click on another box keeps the text unsaved; it is stored only by Save.
