# simulate-mouse-ui Output Fields

Returns JSON with:

- `Success`: Whether the operation succeeded
- `Message`: Status message (e.g. "Hit element: ButtonStart" or "No UI element under (x, y)")
- `Action`: Echoes which action was executed (`Click`, `Drag`, `DragStart`, `DragMove`, `DragEnd`, or `LongPress`)
- `HitGameObjectName`: Name of the GameObject that received the pointer events (nullable string; null if nothing was hit). It is the event-handler target resolved up the hierarchy from the raycast hit (the first `IPointerDownHandler`, else the first `IPointerClickHandler`, for `Click`/`LongPress`; the first `IDragHandler` for drags), so it can be an ancestor of the topmost raycast hit — a Button's child Text reports the Button. With `--bypass-raycast` it is the handler resolved the same way from `--target-path` (also possibly an ancestor), not the raycast hit and not necessarily the raw path GameObject itself. For a UI Toolkit hit it is the name of the outermost control an annotation would list (the same as the annotation's `Name`); when the picked element is not inside such a control (a bare `Label` or `VisualElement`), it is that element's own `name`, or its type name when empty, and the element is not annotated. Unlike uGUI, which reports null when no handler is found, UI Toolkit reports the picked element whether or not it handles pointer events
- `UiSystem`: Which UI system received the press (nullable string; `Click`/`LongPress` and drags, null if nothing was hit): `UGUI` for a target under a Canvas, `UIToolkit` for a UI Toolkit control, and `""` for a non-UI target such as a physics object
- `HitPath`: Path of the hit target in the form `AnnotatedElements[].Path` uses (nullable string; `Click`/`LongPress` and drags): a hierarchy path for GameObjects, `Document/Path#element` for UI Toolkit controls. Null when nothing was hit, or when the hit element belongs to no document, such as the menu of an open dropdown
- `PositionX`: Target X coordinate that was used
- `PositionY`: Target Y coordinate that was used
- `EndPositionX`: Drag end X coordinate (nullable float; populated for drag actions only)
- `EndPositionY`: Drag end Y coordinate (nullable float; populated for drag actions only)
- `InterruptedByPausePoint` / `PausePointId` / `PausePointHitCount` / `PausePointHits`: Pause-point interruption info (all nullable except the boolean). `PausePointHits` lists every marker hit during this input in hit order; `PausePointId` only names the latest one. See the Pause Point Inspection section in SKILL.md

Verify the visual outcome with a follow-up `uloop screenshot --capture-mode rendering --annotate-elements`.

Note: Click and LongPress on empty space (no UI element) still return `Success = true` with `HitGameObjectName = null`. Drag actions on empty space return `Success = false`.

## UI Toolkit Elements

`Click`, `LongPress`, and the drag actions reach UI Toolkit controls at their annotated `SimX`/`SimY` (`--from-x`/`--from-y` for `Drag`). How presses and drags are delivered and what is not supported: `ui-toolkit.md`.
