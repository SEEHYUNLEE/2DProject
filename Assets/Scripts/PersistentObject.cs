using UnityEngine;

public class PersistentObject : MonoBehaviour
{
    void Awake()
    {
        // 씬에 이미 NetworkManager가 있다면 Destroy
        if (GameObject.FindGameObjectsWithTag("NetworkManager").Length > 1)
        {
            Destroy(gameObject);
        }
        else
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}
