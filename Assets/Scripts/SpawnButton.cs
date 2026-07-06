using UnityEngine;
using UnityEngine.UI;

public class SpawnButton : MonoBehaviour
{
    [SerializeField] private UnitType unitType;

    void Start()
    {
        Button btn = GetComponent<Button>();

        if (btn != null)
        {
            // 버튼에 설정한 unitType을 넘겨줌
            btn.onClick.AddListener(() => {
                if (MultiplayerManager.Instance != null)
                {
                    // MultiplayerManager의 함수를 호출합니다.
                    MultiplayerManager.Instance.OnSpawnButtonClicked(unitType);
                }
            });
        }
    }
}