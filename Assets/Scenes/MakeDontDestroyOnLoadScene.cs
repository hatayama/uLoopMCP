using UnityEngine;

public class MakeDontDestroyOnLoadScene : MonoBehaviour
{
    private void Start()
    {
        DontDestroyOnLoad(gameObject);
    }
}
