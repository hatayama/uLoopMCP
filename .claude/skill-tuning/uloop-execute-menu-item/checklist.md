# Checklist — uloop-execute-menu-item

## Scenario A (Save scene)
- [critical] Issues `uloop execute-menu-item --menu-item-path "File/Save"`
- [critical] Reports success/failure using documented field names (`Success`, `ErrorMessage`)
- Does not invent field names

## Scenario B (Project Settings)
- [critical] Issues `uloop execute-menu-item --menu-item-path "Edit/Project Settings..."`
- Reports `Success`; mentions `MenuItemFound` if needed

## Scenario H (Non-existent menu path)
- [critical] Recognizes `Success: false` + `MenuItemFound: false` from documented fields
- Surfaces `ErrorMessage` to the user
- Does not invent a "not found" field name
