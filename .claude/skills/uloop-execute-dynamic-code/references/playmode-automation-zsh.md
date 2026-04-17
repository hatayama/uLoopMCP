# PlayMode Automation (zsh)

Code examples for runtime automation during Play mode using `execute-dynamic-code`.
These examples manipulate live scene objects while the game is running.
Shell command examples in this file target `zsh`-style usage.

## When to use dedicated mouse simulation tools instead

The examples in this file call UI handlers or runtime methods from C#.
That is useful for targeted automation, but it is not the same as simulating a real mouse input path.

Use dedicated mouse tools when you want input behavior closer to what a human player produces:

| Scenario | Recommended tool | Why |
|----------|------------------|-----|
| Click or drag a uGUI element through the EventSystem path | `simulate-mouse-ui` | Fires `PointerDown` / `PointerUp` / `PointerClick` / drag events through EventSystem raycasts instead of calling handlers from custom C# directly. |
| Test gameplay that reads `Mouse.current`, button state, delta, or scroll | `simulate-mouse-input` | Injects Input System mouse state into `Mouse.current`, so game code can observe `wasPressedThisFrame`, movement delta, and scroll like player input. |
| Jump straight to a known button callback or inspect internal state | `execute-dynamic-code` | Best when you intentionally want a direct method call, reflection, or state tweak without reproducing the full input pipeline. |

In short: `execute-dynamic-code` is best for direct automation, while `simulate-mouse-ui` and `simulate-mouse-input` are better when you need the actual input route to be part of the test.

## zsh Quoting Notes

Use these patterns when you need shell-safe inline code:

### Double quotes inside C# strings

Single-quote the whole snippet and keep C# string literals unchanged.

```zsh
uloop execute-dynamic-code --code 'return "Hello from zsh";'
```

### Single quotes inside inline C# code

If the C# snippet itself contains a single quote, close and reopen the shell string with `'\''`.

```zsh
uloop execute-dynamic-code --code 'char initial = '\''A'\''; return initial.ToString();'
```

### JSON-like values passed via `--parameters`

Wrap the whole expression in single quotes so the shell does not interpret double quotes.

```zsh
uloop execute-dynamic-code --code 'return parameters["param0"];' --parameters '{"param0":"Hello from zsh"}'
```

## Click UI Button by Path

```csharp
using UnityEngine.UI;

Button btn = GameObject.Find("Canvas/StartButton")?.GetComponent<Button>();
if (btn == null) return "Button not found";

btn.onClick.Invoke();
return $"Clicked {btn.gameObject.name}";
```

## Click UI Button by Search

```csharp
using UnityEngine.UI;
using System.Linq;

Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
Button target = buttons.FirstOrDefault(b => b.gameObject.name == "PlayButton");
if (target == null) return $"PlayButton not found. Available: {string.Join(", ", buttons.Select(b => b.gameObject.name))}";

target.onClick.Invoke();
return $"Clicked {target.gameObject.name}";
```

## Raycast from Camera Center

```csharp
Camera cam = Camera.main;
if (cam == null) return "Main camera not found";

Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0));
if (Physics.Raycast(ray, out RaycastHit hit, 100f))
{
    return $"Hit: {hit.collider.gameObject.name} at {hit.point}";
}
return "No hit";
```

## Raycast Click at Screen Position

```csharp
using UnityEngine.EventSystems;
using System.Collections.Generic;

if (EventSystem.current == null) return "EventSystem not found";

PointerEventData pointerData = new PointerEventData(EventSystem.current)
{
    position = new Vector2(Screen.width / 2f, Screen.height / 2f)
};

List<RaycastResult> results = new List<RaycastResult>();
EventSystem.current.RaycastAll(pointerData, results);

if (results.Count == 0) return "No UI element at screen center";

GameObject target = results[0].gameObject;
ExecuteEvents.ExecuteHierarchy(target, pointerData, ExecuteEvents.pointerClickHandler);
return $"Clicked UI element: {target.name}";
```

## Toggle GameObject Active State

```csharp
GameObject obj = GameObject.Find("Enemy");
if (obj == null) return "Enemy not found";

obj.SetActive(!obj.activeSelf);
return $"{obj.name} is now {(obj.activeSelf ? "active" : "inactive")}";
```

## Invoke Method on MonoBehaviour

```csharp
using System.Reflection;

GameObject player = GameObject.Find("Player");
if (player == null) return "Player not found";

MonoBehaviour script = player.GetComponent("PlayerController") as MonoBehaviour;
if (script == null) return "PlayerController not found";

MethodInfo method = script.GetType().GetMethod("TakeDamage");
if (method == null) return "TakeDamage method not found";

method.Invoke(script, new object[] { 10f });
return $"Invoked TakeDamage(10) on {player.name}";
```

## Set Field Value at Runtime

```csharp
using System.Reflection;

GameObject player = GameObject.Find("Player");
if (player == null) return "Player not found";

MonoBehaviour script = player.GetComponent("PlayerController") as MonoBehaviour;
if (script == null) return "PlayerController not found";

FieldInfo field = script.GetType().GetField("moveSpeed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
if (field == null) return "moveSpeed field not found";

field.SetValue(script, 20f);
return $"Set moveSpeed to 20 on {player.name}";
```

## Move Player to Position

```csharp
GameObject player = GameObject.Find("Player");
if (player == null) return "Player not found";

Vector3 targetPos = new Vector3(5f, 0f, 10f);
player.transform.position = targetPos;
return $"Moved {player.name} to {targetPos}";
```

---

# Tool Combination Workflows

Examples of combining `execute-dynamic-code` with other uloop tools for multi-step PlayMode automation.

## find-game-objects → Click Button

Use `find-game-objects` to discover buttons with their hierarchy paths, then click one via `execute-dynamic-code`.

**Step 1**: Find all GameObjects with Button component

```zsh
uloop find-game-objects --required-components UnityEngine.UI.Button --include-inactive false
```

**Step 2**: Click the target button using the path from Step 1

```csharp
using UnityEngine.UI;

// Use the path returned by find-game-objects (e.g. "Canvas/MainMenu/StartButton")
GameObject btnObj = GameObject.Find("Canvas/MainMenu/StartButton");
if (btnObj == null) return "Button not found at path";

Button btn = btnObj.GetComponent<Button>();
if (btn == null) return "No Button component";

btn.onClick.Invoke();
return $"Clicked {btnObj.name}";
```

## get-hierarchy → Navigate UI and Click

Use `get-hierarchy` to explore the UI tree structure, then target the right element.

**Step 1**: Get Canvas hierarchy to understand UI structure

```zsh
uloop get-hierarchy --root-path "Canvas" --max-depth 3 --include-components true
```

**Step 2**: Based on the hierarchy JSON, click the desired button

```csharp
using UnityEngine.UI;

// Path identified from hierarchy output
GameObject btnObj = GameObject.Find("Canvas/SettingsPanel/AudioTab/MuteToggle");
if (btnObj == null) return "MuteToggle not found";

Toggle toggle = btnObj.GetComponent<Toggle>();
if (toggle != null)
{
    toggle.isOn = !toggle.isOn;
    return $"Toggled {btnObj.name} to {toggle.isOn}";
}
return "No Toggle component found";
```

## Execute Action → Screenshot to Verify

Run an action then capture a screenshot to visually confirm the result.

**Step 1**: Perform the action

```csharp
using UnityEngine.UI;

Button btn = GameObject.Find("Canvas/PlayButton")?.GetComponent<Button>();
if (btn == null) return "PlayButton not found";

btn.onClick.Invoke();
return "Clicked PlayButton";
```

**Step 2**: Capture Game View to verify the result

```zsh
uloop screenshot --window-name Game
```

## Execute Action → Check Logs for Side Effects

Run an action then inspect Unity Console logs to verify expected behavior.

**Step 1**: Clear console before the action

```zsh
uloop clear-console
```

**Step 2**: Perform the action

```csharp
using System.Reflection;

GameObject player = GameObject.Find("Player");
if (player == null) return "Player not found";

MonoBehaviour script = player.GetComponent("PlayerController") as MonoBehaviour;
if (script == null) return "PlayerController not found";

MethodInfo method = script.GetType().GetMethod("TakeDamage");
if (method == null) return "TakeDamage method not found";

method.Invoke(script, new object[] { 50f });
return "Invoked TakeDamage(50)";
```

**Step 3**: Check logs for expected output

```zsh
uloop get-logs --log-type Log --search-text "damage"
```

## Full Automation: Play → Act → Capture → Stop

End-to-end test flow: start Play mode, perform actions, capture evidence, stop.

**Step 0**: Clear console to isolate this run

```zsh
uloop clear-console
```

**Step 1**: Start Play mode

```zsh
uloop control-play-mode --action Play
```

**Step 2**: Wait for scene initialization, then find and click a button

```csharp
using UnityEngine.UI;
using System.Linq;

// Scene may need a moment to initialize after Play starts
Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
Button startBtn = buttons.FirstOrDefault(b => b.gameObject.name.Contains("Start"));
if (startBtn == null) return $"Start button not found. Available: {string.Join(", ", buttons.Select(b => b.gameObject.name))}";

startBtn.onClick.Invoke();
return $"Clicked {startBtn.gameObject.name}";
```

**Step 3**: Capture screenshot as evidence

```zsh
uloop screenshot --window-name Game
```

**Step 4**: Check logs for errors

```zsh
uloop get-logs --log-type Error
```

**Step 5**: Stop Play mode

```zsh
uloop control-play-mode --action Stop
```
