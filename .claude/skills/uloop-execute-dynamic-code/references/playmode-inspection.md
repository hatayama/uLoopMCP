# PlayMode Inspection

Code examples for inspecting and verifying game state at runtime using `execute-dynamic-code`.
These examples read or check runtime state for debugging and automated testing.

## Check Animator State

```csharp
Animator animator = GameObject.Find("Character")?.GetComponent<Animator>();
if (animator == null) return "Animator not found on Character";

AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
return $"Layer 0: stateHash={state.shortNameHash}, normalizedTime={state.normalizedTime:F2}, isLooping={state.loop}";
```

## Check Animator Parameter Values

```csharp
using System.Text;

Animator animator = GameObject.Find("Character")?.GetComponent<Animator>();
if (animator == null) return "Animator not found";

StringBuilder sb = new StringBuilder();
sb.AppendLine($"Animator on {animator.gameObject.name}:");

for (int i = 0; i < animator.parameterCount; i++)
{
    AnimatorControllerParameter param = animator.GetParameter(i);
    string value = param.type switch
    {
        AnimatorControllerParameterType.Float => animator.GetFloat(param.name).ToString("F2"),
        AnimatorControllerParameterType.Int => animator.GetInteger(param.name).ToString(),
        AnimatorControllerParameterType.Bool => animator.GetBool(param.name).ToString(),
        AnimatorControllerParameterType.Trigger => "(trigger)",
        _ => "unknown"
    };
    sb.AppendLine($"  {param.name} ({param.type}) = {value}");
}
return sb.ToString();
```

## Verify Animator Is in Specific State

```csharp
Animator animator = GameObject.Find("Character")?.GetComponent<Animator>();
if (animator == null) return "Animator not found";

string expectedState = "Idle";
bool isInState = animator.GetCurrentAnimatorStateInfo(0).IsName(expectedState);
return $"Character is in '{expectedState}': {isInState}";
```

## Check AudioSource Playing

```csharp
using System.Text;
using System.Linq;

AudioSource[] sources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
StringBuilder sb = new StringBuilder();
sb.AppendLine($"Found {sources.Length} AudioSources:");

foreach (AudioSource src in sources)
{
    string clipName = src.clip != null ? src.clip.name : "(none)";
    sb.AppendLine($"  {src.gameObject.name}: clip={clipName}, playing={src.isPlaying}, volume={src.volume:F2}, mute={src.mute}");
}
return sb.ToString();
```

## Verify Specific Audio Is Playing

```csharp
using System.Linq;

AudioSource[] sources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
AudioSource bgm = sources.FirstOrDefault(s => s.gameObject.name == "BGMSource");
if (bgm == null) return "BGMSource not found";

return $"BGM: clip={bgm.clip?.name ?? "none"}, playing={bgm.isPlaying}, time={bgm.time:F1}/{(bgm.clip != null ? bgm.clip.length : 0):F1}s";
```

## Get Active Scene Info

```csharp
using UnityEngine.SceneManagement;

Scene scene = SceneManager.GetActiveScene();
return $"Scene: {scene.name}, path: {scene.path}, buildIndex: {scene.buildIndex}, isLoaded: {scene.isLoaded}, rootCount: {scene.rootCount}";
```

## Check Loaded Scenes

```csharp
using UnityEngine.SceneManagement;
using System.Text;

int count = SceneManager.sceneCount;
StringBuilder sb = new StringBuilder();
sb.AppendLine($"Loaded scenes ({count}):");

for (int i = 0; i < count; i++)
{
    Scene scene = SceneManager.GetSceneAt(i);
    sb.AppendLine($"  [{i}] {scene.name} (buildIndex={scene.buildIndex}, isLoaded={scene.isLoaded})");
}
return sb.ToString();
```

## Read Component Field via Reflection

```csharp
using System.Reflection;

GameObject target = GameObject.Find("Player");
if (target == null) return "Player not found";

MonoBehaviour script = target.GetComponent("PlayerController") as MonoBehaviour;
if (script == null) return "PlayerController not found";

FieldInfo hpField = script.GetType().GetField("hp", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
if (hpField == null) return "hp field not found";

object value = hpField.GetValue(script);
return $"Player hp = {value}";
```

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

## Check If GameObject Exists (Spawned/Destroyed)

```csharp
using System.Linq;

string targetName = "Enemy";
GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
GameObject[] matches = all.Where(g => g.name.Contains(targetName)).ToArray();

if (matches.Length == 0) return $"No GameObjects containing '{targetName}' found";

return $"Found {matches.Length} objects matching '{targetName}': {string.Join(", ", matches.Select(g => g.name))}";
```

## Verify Object Position/Rotation

```csharp
GameObject target = GameObject.Find("Player");
if (target == null) return "Player not found";

Transform t = target.transform;
return $"{target.name}: pos={t.position}, rot={t.rotation.eulerAngles}, scale={t.localScale}";
```

## Check Rigidbody State

```csharp
Rigidbody rb = GameObject.Find("TargetCube")?.GetComponent<Rigidbody>();
if (rb == null) return "Rigidbody not found on TargetCube";

// Use rb.velocity for Unity < 2023.3, rb.linearVelocity for Unity 2023.3+
return $"velocity={rb.linearVelocity}, angularVelocity={rb.angularVelocity}, isKinematic={rb.isKinematic}, useGravity={rb.useGravity}, isSleeping={rb.IsSleeping()}";
```

## Check Collision/Trigger Counts via Tag

```csharp
using System.Linq;

string tag = "Enemy";
GameObject[] enemies = GameObject.FindGameObjectsWithTag(tag);
return $"Found {enemies.Length} objects with tag '{tag}': {string.Join(", ", enemies.Select(e => $"{e.name} at {e.transform.position}"))}";
```

## Inspect Particle System State

```csharp
using System.Text;
using System.Linq;

ParticleSystem[] particles = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
StringBuilder sb = new StringBuilder();
sb.AppendLine($"Found {particles.Length} ParticleSystems:");

foreach (ParticleSystem ps in particles)
{
    sb.AppendLine($"  {ps.gameObject.name}: playing={ps.isPlaying}, count={ps.particleCount}, duration={ps.main.duration:F1}s");
}
return sb.ToString();
```

---

# Tool Combination Workflows for Inspection

## Run Action → Inspect State → Verify

**Step 1**: Perform an action (e.g., click a button)

```csharp
using UnityEngine.UI;

Button btn = GameObject.Find("Canvas/AttackButton")?.GetComponent<Button>();
if (btn == null) return "AttackButton not found";

btn.onClick.Invoke();
return "Clicked AttackButton";
```

**Step 2**: Read game state via reflection to verify the action had effect

```csharp
using System.Reflection;

MonoBehaviour script = GameObject.Find("Enemy")?.GetComponent("EnemyController") as MonoBehaviour;
if (script == null) return "EnemyController not found";

FieldInfo hpField = script.GetType().GetField("hp", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
if (hpField == null) return "hp field not found";

return $"Enemy hp = {hpField.GetValue(script)}";
```

**Step 3**: Capture screenshot and check logs

```bash
uloop screenshot --window-name Game
uloop get-logs --log-type Log --search-text "damage"
```
