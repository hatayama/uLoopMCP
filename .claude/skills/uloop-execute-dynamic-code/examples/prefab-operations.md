# Prefab Operations

Code examples for Prefab operations using `execute-dynamic-code`.

## Create a Prefab from GameObject

```csharp
using UnityEditor;

GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
cube.name = "MyCube";
string path = "Assets/Prefabs/MyCube.prefab";
PrefabUtility.SaveAsPrefabAsset(cube, path);
Object.DestroyImmediate(cube);
return $"Prefab created at {path}";
```

## Instantiate a Prefab

```csharp
using UnityEditor;

string prefabPath = "Assets/Prefabs/MyCube.prefab";
GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
if (prefab == null)
{
    return $"Prefab not found at {prefabPath}";
}

GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
instance.transform.position = new Vector3(0, 1, 0);
return $"Instantiated {instance.name}";
```

## Add Component to Prefab

```csharp
using UnityEditor;

string prefabPath = "Assets/Prefabs/MyCube.prefab";
GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
string assetPath = AssetDatabase.GetAssetPath(prefab);

using (PrefabUtility.EditPrefabContentsScope scope = new PrefabUtility.EditPrefabContentsScope(assetPath))
{
    GameObject root = scope.prefabContentsRoot;
    if (root.GetComponent<Rigidbody>() == null)
    {
        root.AddComponent<Rigidbody>();
    }
}
return "Added Rigidbody to prefab";
```

## Modify Prefab Properties

```csharp
using UnityEditor;

string prefabPath = "Assets/Prefabs/MyCube.prefab";
GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

using (PrefabUtility.EditPrefabContentsScope scope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
{
    GameObject root = scope.prefabContentsRoot;
    root.transform.localScale = new Vector3(2, 2, 2);

    MeshRenderer renderer = root.GetComponent<MeshRenderer>();
    if (renderer != null)
    {
        renderer.sharedMaterial.color = Color.red;
    }
}
return "Modified prefab properties";
```

## Find All Prefab Instances in Scene

```csharp
using UnityEditor;
using System.Collections.Generic;

string prefabPath = "Assets/Prefabs/MyCube.prefab";
GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
if (prefab == null)
{
    return $"Prefab not found at {prefabPath}";
}

List<GameObject> instances = new List<GameObject>();

foreach (GameObject obj in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
{
    if (PrefabUtility.GetCorrespondingObjectFromSource(obj) == prefab)
    {
        instances.Add(obj);
    }
}
return $"Found {instances.Count} instances of {prefab.name}";
```

## Apply Prefab Overrides

```csharp
using UnityEditor;

GameObject selected = Selection.activeGameObject;
if (selected == null)
{
    return "No GameObject selected";
}

if (!PrefabUtility.IsPartOfPrefabInstance(selected))
{
    return "Selected object is not a prefab instance";
}

PrefabUtility.ApplyPrefabInstance(selected, InteractionMode.UserAction);
return $"Applied overrides from {selected.name} to prefab";
```
