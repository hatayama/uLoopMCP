# PlayMode Automation

Code examples for runtime automation during Play mode using `execute-dynamic-code`.
These examples manipulate live scene objects while the game is running.

## Click UI Button by Path

```csharp
using UnityEngine.UI;

Button btn = GameObject.Find("Canvas/StartButton")?.GetComponent<Button>();
if (btn == null) return "Button not found";

btn.onClick.Invoke();
return $"Clicked {btn.gameObject.name}";
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

Camera cam = Camera.main;
if (cam == null) return "Main camera not found";

PointerEventData pointerData = new PointerEventData(EventSystem.current)
{
    position = new Vector2(Screen.width / 2f, Screen.height / 2f)
};

List<RaycastResult> results = new List<RaycastResult>();
EventSystem.current.RaycastAll(pointerData, results);

if (results.Count == 0) return "No UI element at screen center";

GameObject target = results[0].gameObject;
ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerClickHandler);
return $"Clicked UI element: {target.name}";
```
