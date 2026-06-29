using System.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public enum UnitType
{
    Warrior,
    Archer,
    Random,
    GreatWarrior,
    GreatArcher,
    Alien
}

public class Player : NetworkBehaviour
{
    public NetworkVariable<int> teamIndex = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> coin = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private TMP_Text coinText;

    public override void OnNetworkSpawn()
    {
        // 코인 값 변경 이벤트 등록 (모든 클라이언트가 들음)
        coin.OnValueChanged += OnCoinChanged;

        if (IsOwner)
        {
            RequestAssignTeamServerRpc();

            // 씬 전환 직후 텍스트 컴포넌트 바로 찾아두기
            FindCoinTextObject();
            UpdateCoinUI(coin.Value);
        }

        if (IsServer)
        {
            StartCoroutine(GiveCoinEverySecond());
        }
    }

    public override void OnNetworkDespawn()
    {
        coin.OnValueChanged -= OnCoinChanged;
    }

    private IEnumerator GiveCoinEverySecond()
    {
        while (true)
        {
            yield return new WaitForSeconds(1.0f);

            if (SceneManager.GetActiveScene().name == "GameScene")
            {
                coin.Value += 2000; // 1초에 50G씩 증가
            }
        }
    }

    // 씬에서 코인 텍스트 오브젝트를 찾는 별도 메서드 (실패 대비용)
    private void FindCoinTextObject()
    {
        if (coinText != null) return;

        GameObject textObj = GameObject.Find("CoinText");
        if (textObj != null)
        {
            coinText = textObj.GetComponent<TMP_Text>();
        }
    }

    private void OnCoinChanged(int previousValue, int newValue)
    {
        // 내 캐릭터의 코인이 바뀐 경우에만 UI 업데이트
        if (IsOwner)
        {
            UpdateCoinUI(newValue);
        }
    }

    private void UpdateCoinUI(int currentCoin)
    {
        // 혹시 Start 시점에 못 찾았다면 갱신할 때 다시 한번 찾음
        if (coinText == null)
        {
            FindCoinTextObject();
        }

        if (coinText != null)
        {
            coinText.text = $"{currentCoin}G";
        }
    }

    public bool CanAfford(int amount)
    {
        return coin.Value >= amount;
    }

    public bool ConsumeCoin(int amount)
    {
        if (!IsServer) return false;

        if (coin.Value >= amount)
        {
            coin.Value -= amount;
            return true;
        }
        return false;
    }

    // --- [중요] 소환 요청 및 검증 흐름 ---
    public void RequestSpawn(UnitType type)
    {
        if (MultiplayerManager.Instance != null && MultiplayerManager.Instance.isGameOver) return;

        if (!IsOwner || teamIndex.Value == 0) return;

        int cost = GetUnitCost(type);

        if (CanAfford(cost))
        {
            SpawnUnitServerRpc(type);
        }
    }

    [ServerRpc]
    private void SpawnUnitServerRpc(UnitType type, ServerRpcParams rpcParams = default)
    {
        int cost = GetUnitCost(type);

        // [치트 방지] 서버에서 최종 돈 차감 검증
        if (ConsumeCoin(cost))
        {
            UnitType finalSpawnType = type;
            if (type == UnitType.Random)
            {
                int rand = UnityEngine.Random.Range(0, 100); // 0 ~ 99

                if (rand < 30) finalSpawnType = UnitType.Warrior;
                else if (rand < 60) finalSpawnType = UnitType.Archer;
                else if (rand < 75) finalSpawnType = UnitType.GreatWarrior;
                else if (rand < 90) finalSpawnType = UnitType.GreatArcher;
                else finalSpawnType = UnitType.Alien;
            }

            MultiplayerManager.Instance.SpawnUnit(rpcParams.Receive.SenderClientId, finalSpawnType);
        }
    }

    private int GetUnitCost(UnitType type)
    {
        return type switch
        {
            UnitType.Warrior => 100,
            UnitType.Archer => 150,
            UnitType.Random => 300,
            _ => 0
        };
    }

    [ServerRpc]
    private void RequestAssignTeamServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        teamIndex.Value = (clientId == 0) ? 1 : 2;
    }
}