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

## Click TMP Button (TextMeshPro)

```csharp
using UnityEngine.UI;
using TMPro;
using System.Linq;

Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
Button target = buttons.FirstOrDefault(b =>
{
    TMP_Text label = b.GetComponentInChildren<TMP_Text>();
    return label != null && label.text == "Start Game";
});
if (target == null) return "Button with 'Start Game' label not found";

target.onClick.Invoke();
return $"Clicked button with label: {target.GetComponentInChildren<TMP_Text>().text}";
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

## List All Active Buttons in Scene

```csharp
using UnityEngine.UI;
using TMPro;
using System.Text;

Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
StringBuilder sb = new StringBuilder();
sb.AppendLine($"Found {buttons.Length} buttons:");

foreach (Button btn in buttons)
{
    string label = "";
    TMP_Text tmp = btn.GetComponentInChildren<TMP_Text>();
    if (tmp != null) label = tmp.text;
    else
    {
        Text legacyText = btn.GetComponentInChildren<Text>();
        if (legacyText != null) label = legacyText.text;
    }

    string path = GetPath(btn.transform);
    sb.AppendLine($"  [{path}] label=\"{label}\" interactable={btn.interactable}");
}
return sb.ToString();

string GetPath(Transform t)
{
    if (t.parent == null) return t.name;
    return GetPath(t.parent) + "/" + t.name;
}
```

## Trigger Animation State

```csharp
Animator animator = GameObject.Find("Character")?.GetComponent<Animator>();
if (animator == null) return "Animator not found on Character";

animator.SetTrigger("Jump");
return "Triggered Jump animation";
```

## Move Player to Position

```csharp
GameObject player = GameObject.Find("Player");
if (player == null) return "Player not found";

Vector3 targetPos = new Vector3(5f, 0f, 10f);
player.transform.position = targetPos;
return $"Moved {player.name} to {targetPos}";
```
