using System.Diagnostics;
using Unity.Netcode;
using UnityEngine;

public class UnitStats : NetworkBehaviour
{
    public UnitType unitType;
    public int maxHp;

    // NetworkVariable = 서버가 값을 변경하면 모든 클라이언트에 자동으로 동기화해 주는 네트워크 전용 변수
    public NetworkVariable<int> currentHp = new NetworkVariable<int>(100,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (unitType == UnitType.Warrior)
            {
                maxHp = 100;
            }
            else if (unitType == UnitType.Archer)
            {
                maxHp = 80;
            }
            else if (unitType == UnitType.GreatWarrior)
            {
                maxHp = 250;
            }
            else if (unitType == UnitType.GreatArcher)
            {
                maxHp = 160;
            }
            else
            {
                maxHp = 50; // 기본값
            }

            currentHp.Value = maxHp;
        }
    }

    // 서버에서만 호출되어야 하는 데미지 처리 함수
    public void TakeDamage(int damage)
    {
        if (!IsServer) return;

        currentHp.Value -= damage;

        if (currentHp.Value <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        // 서버에서 NetworkObject를 Despawn하면 모든 클라이언트에서 파괴
        if (IsServer)
        {
            GetComponent<NetworkObject>().Despawn();
        }
    }
}