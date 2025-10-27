using UnityEngine;

public class MakeDontDestroryOnLoadScene : MonoBehaviour
{
    private void Start()
    {
        DontDestroyOnLoad(gameObject);
    }
}
