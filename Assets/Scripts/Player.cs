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

    public NetworkVariable<int> coin = new NetworkVariable<int>(500,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private TMP_Text coinText;

    public override void OnNetworkSpawn()
    {
        // 코인 값 변경 이벤트 등록
        coin.OnValueChanged += OnCoinChanged;

        if (IsOwner)
        {
            RequestAssignTeamServerRpc();

            // 씬 전환 직후 텍스트 바로 찾기
            FindCoinTextObject();
            UpdateCoinUI(coin.Value);
        }

        if (IsServer)
        {
            coin.Value = 500;
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
                coin.Value += 50;
            }
        }
    }

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
        if (IsOwner)
        {
            UpdateCoinUI(newValue);
        }
    }

    private void UpdateCoinUI(int currentCoin)
    {
        // 텍스트 못찾은 경우 방지
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

    // ------------- 유닛 소환 검증 후 실행 -------------
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

        // 서버에서 최종 돈 차감됐는지 검증
        if (ConsumeCoin(cost))
        {
            UnitType finalSpawnType = type;
            if (type == UnitType.Random)
            {
                int rand = UnityEngine.Random.Range(0, 100);

                if (rand < 25) finalSpawnType = UnitType.Warrior;
                else if (rand < 50) finalSpawnType = UnitType.Archer;
                else if (rand < 70) finalSpawnType = UnitType.GreatWarrior;
                else if (rand < 90) finalSpawnType = UnitType.GreatArcher;
                else finalSpawnType = UnitType.Alien;
            }
            else if (type == UnitType.Warrior)
            {
                int rand = UnityEngine.Random.Range(0, 100);
                if (rand == 0)
                {
                    finalSpawnType = UnitType.GreatWarrior;
                }
            }
            else if (type == UnitType.Archer)
            {
                int rand = UnityEngine.Random.Range(0, 100);
                if (rand == 0)
                {
                    finalSpawnType = UnitType.GreatArcher;
                }
            }

            MultiplayerManager.Instance.SpawnUnit(rpcParams.Receive.SenderClientId, finalSpawnType);
        }
    }

    private int GetUnitCost(UnitType type)
    {
        return type switch
        {
            UnitType.Warrior => 100,
            UnitType.Archer => 100,
            UnitType.Random => 1000,
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