using UnityEngine;
using Unity.Netcode;

public class Archer : Warrior
{
    public GameObject arrowPrefab;
    public Transform shootPoint;

    void Awake() { attackRange = 8.0f; }

    // 🟢 애니메이션 이벤트가 호출할 함수
    public new void DealDamage()
    {
        if (!IsServer) return;

        // 궁수 본인이 죽었는지 확인
        var stats = GetComponent<UnitStats>();
        if (stats != null && stats.currentHp.Value <= 0) return;

        // 타겟 결정
        Transform attackTarget = isAttackingBase ? (targetBase != null ? targetBase.transform : null)
                                               : (currentEnemy != null ? currentEnemy.transform : null);

        // 타겟이 살아있는지 확인 후 발사
        if (attackTarget != null && attackTarget.GetComponent<NetworkObject>() != null)
        {
            GameObject arrowObj = Instantiate(arrowPrefab, shootPoint.position, Quaternion.identity);
            arrowObj.GetComponent<NetworkObject>().Spawn();
            arrowObj.GetComponent<Arrow>().Setup(attackTarget, damage, this.teamIndex.Value);
        }
    }
}