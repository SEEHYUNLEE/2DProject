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

        // 1. 타겟 결정 (null일 수 있음)
        Transform attackTarget = isAttackingBase ? (targetBase != null ? targetBase.transform : null)
                                                : (currentEnemy != null ? currentEnemy.transform : null);

        PlayAttackSoundClientRpc();

        // 2. 화살 생성 (타겟 유무와 상관없이 발사)
        GameObject arrowObj = Instantiate(arrowPrefab, shootPoint.position, shootPoint.rotation);
        arrowObj.GetComponent<NetworkObject>().Spawn();

        // 3. Arrow 컴포넌트에 타겟 전달
        // Arrow 내부에서 null 체크를 통해 타겟이 없으면 직진하도록 처리합니다.
        arrowObj.GetComponent<Arrow>().Setup(attackTarget, damage, this.teamIndex.Value);
    }
}