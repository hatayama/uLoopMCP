# PlayMode Inspection

Code examples for inspecting and verifying game state at runtime using `execute-dynamic-code`.
These examples read or check runtime state for debugging and automated testing.

## Read Multiple Fields via Reflection

```csharp
using System.Reflection;
using System.Text;

GameObject target = GameObject.Find("Player");
if (target == null) return "Player not found";

MonoBehaviour script = target.GetComponent("PlayerController") as MonoBehaviour;
if (script == null) return "PlayerController not found";

StringBuilder sb = new StringBuilder();
sb.AppendLine($"Fields on {script.GetType().Name}:");

FieldInfo[] fields = script.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
foreach (FieldInfo field in fields)
{
    if (field.DeclaringType == script.GetType())
    {
        object value = field.GetValue(script);
        sb.AppendLine($"  {field.Name} ({field.FieldType.Name}) = {value}");
    }
}
return sb.ToString();
```

## Read Property via Reflection

```csharp
using System.Reflection;

GameObject target = GameObject.Find("GameManager");
if (target == null) return "GameManager not found";

MonoBehaviour script = target.GetComponent("GameManager") as MonoBehaviour;
if (script == null) return "GameManager component not found";

PropertyInfo prop = script.GetType().GetProperty("Score", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
if (prop == null) return "Score property not found";

object value = prop.GetValue(script);
return $"Score = {value}";
```

## Check Rigidbody State

```csharp
Rigidbody rb = GameObject.Find("TargetCube")?.GetComponent<Rigidbody>();
if (rb == null) return "Rigidbody not found on TargetCube";

// Use rb.velocity for Unity < 2023.3, rb.linearVelocity for Unity 2023.3+
return $"velocity={rb.linearVelocity}, angularVelocity={rb.angularVelocity}, isKinematic={rb.isKinematic}, useGravity={rb.useGravity}, isSleeping={rb.IsSleeping()}";
```
