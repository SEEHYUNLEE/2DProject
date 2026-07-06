using UnityEngine;
using Unity.Netcode;

public class Archer : Warrior
{
    public GameObject arrowPrefab;
    public Transform shootPoint;

    void Awake()
    {
        attackRange = 8.0f;
        attackVolume = 1.0f;
    }

    public new void DealDamage()
    {
        if (!IsServer) return;

        // 타겟 설정
        Transform attackTarget = isAttackingBase ? (targetBase != null ? targetBase.transform : null)
                                                : (currentEnemy != null ? currentEnemy.transform : null);

        PlayAttackSoundClientRpc();

        // 화살 생성
        GameObject arrowObj = Instantiate(arrowPrefab, shootPoint.position, shootPoint.rotation);
        arrowObj.GetComponent<NetworkObject>().Spawn();

        // Arrow에 타겟 전달
        arrowObj.GetComponent<Arrow>().Setup(attackTarget, damage, this.teamIndex.Value);
    }
}