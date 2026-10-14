# simulate-mouse-ui and UI Toolkit

`Click`, `LongPress`, and the drag actions reach UI Toolkit controls on screen-space panels —
the `AnnotatedElements` entries with `UiSystem` `UIToolkit`. Pass the entry's `SimX`/`SimY` as
`--x`/`--y` (`--from-x`/`--from-y` for `Drag`); the response reports `UiSystem` `UIToolkit`, the
control's `Name` as `HitGameObjectName`, and its annotation `Path` as `HitPath`.

## How a press is delivered

- The point is mapped to each panel the same way the screenshot annotation maps controls to
  `SimX`/`SimY`, so an annotated point lands on the annotated control. It does not depend on
  which Editor view was drawn last.
- Panels are searched front to back by their panel settings' `sortingOrder`; the first panel
  with a control under the point gets the press. The reported control is the outermost one an
  annotation would list, so a click on a scrollbar's thumb reports the scrollbar.
- Where a uGUI element and a UI Toolkit panel are both under the point, the uGUI element wins
  only when its Canvas sorts above the panel (raycaster sort priority greater than the panel's
  `sortingOrder`), the same rule the EventSystem applies to real input. A uGUI image without
  any pointer handler in front still blocks the panel, as real input would.
- A full-screen container left at the default `pickingMode` (`Position`) counts as a hit, so
  uGUI underneath it is not reached — real input behaves the same way.
- UI Toolkit presses do not need an EventSystem. With neither an EventSystem nor a UI Toolkit
  control under the point, the command fails and says so.
- A disabled control receives the press but does not react, as with real input.

## LongPress

A UI Toolkit `Button` fires `clicked` once when the long press is released (UI Toolkit's
`Clickable` behaviour); a uGUI long press sends no click. A `RepeatButton` fires repeatedly
while held.

## Drags

- A drag whose start point resolves to a UI Toolkit control (by the rule above) presses it
  with the left button, moves the pointer, and releases at the end, so a `Slider` or a
  `Scroller` follows the drag. The start point is what counts: the drag stays with that panel
  wherever it moves.
- A labelled slider's bounds include its label; start the drag on the track.
- `--drop-target-path` applies to uGUI drags only. A `Drag` with it fails before anything is
  sent; a `DragEnd` with it fails and leaves the drag in progress, so call `DragEnd` again
  without it.
- If the control's document is disabled or destroyed during a split drag, the next `DragMove`
  or `DragEnd` fails and the drag ends. A one-shot `Drag` stops sending and still succeeds.
- If a UI Toolkit handler throws, the button is released and the exception ends the command.
  A `DragMove` that throws leaves the drag in progress, as a cancelled `DragMove` does.

## Not supported

- `--bypass-raycast`, `--target-path`, and `--drop-target-path` take GameObject paths only;
  a UI Toolkit `Path` (it contains `#`) is reported as not found.
- World-space panels (Unity 6), as in the annotation.
- Two panels with the same `sortingOrder` overlapping at the point: Unity orders them by
  creation order, which is not exposed, so either one may be chosen.

## Notes

- Move the real mouse cursor out of the Game View while simulating, and keep it away between
  `DragStart` and `DragEnd`. Real pointer input over the Game View reaches the same panels and
  can interleave with the simulated press or move a held slider.
- Device Simulator has the same limits as the annotation: coordinates use the simulated device
  resolution.
