# Reference Wiring Examples

Examples of setting references in Inspector fields using SerializedObject.

## Set Material to Prefab's MeshRenderer

```csharp
using UnityEditor;
using UnityEngine;

// Create material
Material mat = new Material(Shader.Find("Standard"));
mat.color = Color.red;
mat.name = "RedMaterial";
AssetDatabase.CreateAsset(mat, "Assets/Samples/RedMaterial.mat");

// Set material to prefab
string prefabPath = "Assets/Samples/MyCube.prefab";
using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
{
    GameObject root = scope.prefabContentsRoot;
    MeshRenderer renderer = root.GetComponent<MeshRenderer>();
    if (renderer != null)
    {
        renderer.sharedMaterial = mat;
    }
}
AssetDatabase.SaveAssets();
return "Material reference set";
```

## Add Component and Set Reference to Child Object

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

string prefabPath = "Assets/Prefabs/MyUI.prefab";
using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
{
    GameObject root = scope.prefabContentsRoot;

    // Add component if not exists
    MyComponent comp = root.GetComponent<MyComponent>();
    if (comp == null)
    {
        comp = root.AddComponent<MyComponent>();
    }

    // Find child object
    Transform child = root.transform.Find("Panel/Button/Icon");
    if (child == null) return "Child not found";

    Image childImage = child.GetComponent<Image>();
    if (childImage == null) return "Image component not found";

    // Set reference using SerializedObject
    SerializedObject so = new SerializedObject(comp);
    SerializedProperty prop = so.FindProperty("_targetImage");
    prop.objectReferenceValue = childImage;
    so.ApplyModifiedProperties();
}
return "Reference set successfully";
```

## Set Multiple References at Once

```csharp
using UnityEditor;
using UnityEngine;

string prefabPath = "Assets/Prefabs/Player.prefab";
using (var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
{
    GameObject root = scope.prefabContentsRoot;
    PlayerController controller = root.GetComponent<PlayerController>();

    SerializedObject so = new SerializedObject(controller);

    // Set Transform reference
    SerializedProperty headProp = so.FindProperty("_head");
    headProp.objectReferenceValue = root.transform.Find("Model/Head");

    // Set AudioSource reference
    SerializedProperty audioProp = so.FindProperty("_audioSource");
    audioProp.objectReferenceValue = root.GetComponent<AudioSource>();

    // Set Animator reference
    SerializedProperty animProp = so.FindProperty("_animator");
    animProp.objectReferenceValue = root.GetComponentInChildren<Animator>();

    so.ApplyModifiedProperties();
}
return "Multiple references set";
```

## Set Reference to External Asset

```csharp
using UnityEditor;
using UnityEngine;

// Load external assets
GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Jump.wav");
Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Player.mat");

// Set references to ScriptableObject
string configPath = "Assets/Data/GameConfig.asset";
ScriptableObject config = AssetDatabase.LoadAssetAtPath<ScriptableObject>(configPath);

SerializedObject so = new SerializedObject(config);

SerializedProperty prefabProp = so.FindProperty("playerPrefab");
prefabProp.objectReferenceValue = playerPrefab;

SerializedProperty clipProp = so.FindProperty("jumpSound");
clipProp.objectReferenceValue = clip;

SerializedProperty matProp = so.FindProperty("playerMaterial");
matProp.objectReferenceValue = mat;

so.ApplyModifiedProperties();
EditorUtility.SetDirty(config);
AssetDatabase.SaveAssets();

return "External asset references set";
```

## Key Points

1. **Use `EditPrefabContentsScope`** - Changes inside this scope are automatically saved to the prefab
2. **Use `SerializedObject` for private fields** - Access `[SerializeField] private` fields via `FindProperty()`
3. **Call `ApplyModifiedProperties()`** - Required to commit changes made through SerializedProperty
4. **Use `sharedMaterial` not `material`** - `material` creates an instance copy, `sharedMaterial` modifies the asset directly
5. **Call `AssetDatabase.SaveAssets()`** - Ensure changes are written to disk for non-prefab assets
