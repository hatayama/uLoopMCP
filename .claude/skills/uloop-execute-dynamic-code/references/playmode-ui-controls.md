# PlayMode UI Controls

Code examples for manipulating UI controls at runtime using `execute-dynamic-code`.
These examples interact with Unity UI components while the game is running.

## Set InputField Text

```csharp
using UnityEngine.UI;

InputField input = GameObject.Find("Canvas/NameInput")?.GetComponent<InputField>();
if (input == null) return "InputField not found";

input.text = "Player1";
input.onEndEdit.Invoke(input.text);
return $"Set InputField text to: {input.text}";
```

## Set Slider Value

```csharp
using UnityEngine.UI;

Slider slider = GameObject.Find("Canvas/VolumeSlider")?.GetComponent<Slider>();
if (slider == null) return "Slider not found";

float oldValue = slider.value;
slider.value = 0.75f;
return $"Slider changed from {oldValue} to {slider.value}";
```

## Toggle On/Off

```csharp
using UnityEngine.UI;

Toggle toggle = GameObject.Find("Canvas/MuteToggle")?.GetComponent<Toggle>();
if (toggle == null) return "Toggle not found";

bool oldValue = toggle.isOn;
toggle.isOn = !toggle.isOn;
return $"Toggle changed from {oldValue} to {toggle.isOn}";
```

## Select Dropdown Item by Index

```csharp
using UnityEngine.UI;

Dropdown dropdown = GameObject.Find("Canvas/DifficultyDropdown")?.GetComponent<Dropdown>();
if (dropdown == null) return "Dropdown not found";

if (dropdown.options.Count == 0) return "Dropdown has no options";

int targetIndex = 2;
if (targetIndex >= dropdown.options.Count)
    return $"Index {targetIndex} out of range. Options count: {dropdown.options.Count}";

dropdown.value = targetIndex;
dropdown.onValueChanged.Invoke(targetIndex);
return $"Selected: {dropdown.options[targetIndex].text} (index {targetIndex})";
```

## Simulate Drag on UI Element

```csharp
using UnityEngine.EventSystems;
using System.Collections.Generic;

GameObject target = GameObject.Find("Canvas/DraggableItem");
if (target == null) return "DraggableItem not found";

PointerEventData pointerData = new PointerEventData(EventSystem.current)
{
    position = new Vector2(Screen.width / 2f, Screen.height / 2f)
};

// Begin drag
ExecuteEvents.Execute(target, pointerData, ExecuteEvents.beginDragHandler);

// Move (simulate drag to a new position)
pointerData.position = new Vector2(Screen.width / 2f + 100f, Screen.height / 2f);
ExecuteEvents.Execute(target, pointerData, ExecuteEvents.dragHandler);

// End drag
ExecuteEvents.Execute(target, pointerData, ExecuteEvents.endDragHandler);
return $"Dragged {target.name} 100px to the right";
```
