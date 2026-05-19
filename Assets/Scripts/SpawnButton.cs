using UnityEngine;
using UnityEngine.UI;

public class SpawnButton : MonoBehaviour
{
    void Start()
    {
        // 1. 이 버튼 컴포넌트를 가져옵니다.
        Button btn = GetComponent<Button>();

        if (btn != null)
        {
            // 2. UI 씬에서 살아서 건너온 멀티플레이어 매니저의 함수를 
            // 클릭 이벤트로 자동으로 연결해 줍니다!
            btn.onClick.AddListener(() => {
                UnityEngine.Debug.Log("1. 스폰 버튼 클릭됨!");
                if (MultiplayerManager.Instance != null)
                {
                    UnityEngine.Debug.Log("2. 멀티플레이어 매니저 인스턴스 존재함!");
                    MultiplayerManager.Instance.OnSpawnButtonClicked();
                }
            });
        }
    }
}