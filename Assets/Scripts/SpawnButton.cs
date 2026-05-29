using UnityEngine;
using UnityEngine.UI;

public class SpawnButton : MonoBehaviour
{
    // 🟢 유니티 인스펙터 창에서 이 버튼이 어떤 유닛을 소환할지 선택할 수 있게 합니다.
    [SerializeField] private UnitType unitType;

    void Start()
    {
        // 1. 이 버튼 컴포넌트를 가져옵니다.
        Button btn = GetComponent<Button>();

        if (btn != null)
        {
            // 2. 클릭 이벤트 연결 (내가 설정한 unitType을 인자로 넘겨줌)
            btn.onClick.AddListener(() => {
                UnityEngine.Debug.Log($"[버튼 클릭] {unitType} 스폰 버튼 클릭됨!");

                if (MultiplayerManager.Instance != null)
                {
                    // 새로 수정한 MultiplayerManager의 인자 있는 함수를 호출합니다.
                    MultiplayerManager.Instance.OnSpawnButtonClicked(unitType);
                }
                else
                {
                    UnityEngine.Debug.LogError("MultiplayerManager 인스턴스를 찾을 수 없습니다!");
                }
            });
        }
    }
}