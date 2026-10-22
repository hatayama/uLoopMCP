# Prefab Capture (`--prefab-path`)

`uloop screenshot --prefab-path <asset path>` instantiates the prefab or model asset in a temporary preview scene, renders it with a temporary camera, and returns one PNG. No window opens, and open scenes, Prefab Mode, and the asset itself are left untouched. It works in Edit Mode and Play Mode.

```bash
uloop screenshot --prefab-path Assets/UI/Panel.prefab
uloop screenshot --prefab-path Assets/UI/Panel.prefab --prefab-background-color "#00000000"
```

## How the prefab is framed

The first matching rule decides the image (the response `Message` says which one was used):

1. **Screen UI** — the prefab has an enabled Screen Space Canvas (Overlay or Camera). The outermost Canvases are switched to the capture camera and the image is the Game View size, so the layout matches the game.
2. **UI part** — the root is a RectTransform. The image is sized to the union of all active RectTransforms, so children that overflow the root are included. Sizes are counted in UI units, so a world-space Canvas saved at a small scale keeps its pixel size. A stretched root fills the Game View size.
3. **3D object** — anything else with an enabled Renderer or a world-space Canvas is viewed from the front-left with a perspective camera at the Game View size, with the whole object in frame.
4. **Flat object** — content with no depth (sprites, quads, a lone world-space Canvas) is viewed straight on with an orthographic camera at the Game View size.

A prefab with nothing to render is rejected. When a prefab has both Screen Space UI and 3D content, the UI rule wins and the 3D content may not appear.

The Game View size comes from the current Game View; change it with `uloop set-game-view-size`.

## Options

- `--window-name` and `--match-mode` are ignored.
- `--capture-mode` other than `auto`, `--annotate-elements`, `--annotate-raycast-grid`, `--raycast-layer-mask`, and `--elements-only` are rejected.
- `--resolution-scale` shrinks the image after it is rendered.
- `--prefab-background-color` takes `#RRGGBB` or `#RRGGBBAA`. The default `#808080` keeps both dark and white text readable. `#00000000` gives a transparent background with URP; whether alpha survives depends on the render pipeline.
- `--prefab-background-color` without `--prefab-path` is rejected.

## What happens to the instance

- If the prefab has no enabled Light, one directional light is added to the preview scene.
- A root saved inactive is activated so it can be captured; children keep their saved state.
- Inside the preview scene, scripts other than `[ExecuteAlways]` ones do not run, even in Play Mode. uGUI draws normally.

## Output

`ResolvedCaptureMode` is `"prefab"` and `ImageCoordinateSystem` is `"top-left-prefab-image"`. Prefab images are not Game View captures, so `ScreenshotToInputFormula` reports that mouse-input coordinates are unavailable.
