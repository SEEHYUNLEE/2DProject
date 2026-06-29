using UnityEngine;

public class PersistentObject : MonoBehaviour
{
    void Awake()
    {
        // 씬에 이미 NetworkManager가 있다면(DontDestroyOnLoad 된 것), 나는 죽는다.
        // 태그를 활용하는 것이 가장 쉽습니다.
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
