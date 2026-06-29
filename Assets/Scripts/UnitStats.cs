using System.Diagnostics;
using Unity.Netcode;
using UnityEngine;

public class UnitStats : NetworkBehaviour
{
    public UnitType unitType;
    public int maxHp;

    // 모든 클라이언트가 읽을 수 있고, 서버만 수정 가능한 네트워크 변수
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
        // 체력이 변경될 때 실행될 함수 등록 (UI 업데이트 등에 활용)
        currentHp.OnValueChanged += OnHpChanged;
    }

    private void OnHpChanged(int previousValue, int newValue)
    {
        if (newValue <= 0)
        {
            // 클라이언트 측에서도 사망 연출 등이 필요하면 여기서 처리
            UnityEngine.Debug.Log($"{gameObject.name}이(가) 사망했습니다.");
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
        // 서버에서 NetworkObject를 Despawn하면 모든 클라이언트에서 파괴됨
        if (IsServer)
        {
            GetComponent<NetworkObject>().Despawn();
        }
    }

    public override void OnNetworkDespawn()
    {
        currentHp.OnValueChanged -= OnHpChanged;
    }
}