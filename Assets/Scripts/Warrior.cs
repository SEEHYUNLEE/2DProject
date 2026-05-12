using UnityEngine;
using Unity.Netcode;

public class Warrior : NetworkBehaviour
{
    public float moveSpeed = 4f;
    public int damage = 20;
    public float attackRange = 1.5f;
    public LayerMask unitLayer;

    // 1: A팀(오른쪽 이동), 2: B팀(왼쪽 이동)
    public NetworkVariable<int> teamIndex = new NetworkVariable<int>(0);
    public NetworkVariable<int> direction = new NetworkVariable<int>(1);

    private bool isAttacking = false;
    private Collider2D currentEnemy;
    private Animator anim;

    // 네트워크 상에 오브젝트가 생성되고 데이터가 준비되었을 때 호출
    public override void OnNetworkSpawn()
    {
        // 프리팹의 원래 스케일을 가져옵니다.
        Vector3 prefabScale = transform.localScale;

        // direction.Value에 따라 X축만 반전시키고 Y, Z는 프리팹 설정을 유지합니다.
        // Mathf.Abs를 사용하는 이유는 프리팹 자체가 이미 마이너스 스케일일 경우를 대비해서입니다.
        transform.localScale = new Vector3(
            Mathf.Abs(prefabScale.x) * direction.Value, 
            prefabScale.y, 
            prefabScale.z
        );

        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (!IsServer) return;
        if (isAttacking) return;

        // 1. 주변 유닛 탐지 (공격 범위 내)
        Collider2D[] targets = Physics2D.OverlapCircleAll(transform.position, attackRange, unitLayer);

        bool shouldStop = false;

        foreach (var target in targets)
        {
            // 자기 자신은 제외
            if (target.gameObject == gameObject) continue;

            Warrior targetWarrior = target.GetComponent<Warrior>();
            if (targetWarrior == null) continue;

            // A. 적군을 발견한 경우
            if (targetWarrior.teamIndex.Value != this.teamIndex.Value)
            {
                currentEnemy = target;
                StartAttackClientRpc();
                isAttacking = true;
                return; // 공격 모드로 진입하며 즉시 종료
            }

            // B. 아군이 바로 앞에 있는 경우 (길막 방지)
            else if (targetWarrior.teamIndex.Value == this.teamIndex.Value)
            {
                // 내 진행 방향(direction)에 아군이 있는지 좌표로 비교
                float relativePosX = target.transform.position.x - transform.position.x;

                // direction이 1(오른쪽)일 때 상대가 오른쪽에 있거나,
                // direction이 -1(왼쪽)일 때 상대가 왼쪽에 있으면 내 앞에 있는 것
                if ((direction.Value > 0 && relativePosX > 0) || (direction.Value < 0 && relativePosX < 0))
                {
                    // 너무 멀리 있는 아군은 무시하고, 일정 거리 안에 있을 때만 멈춤
                    if (Mathf.Abs(relativePosX) < 2f)
                    {
                        shouldStop = true;
                    }
                }
            }
        }

        // 적도 없고 아군에게 막히지도 않았을 때만 이동
        if (!shouldStop)
        {
            MoveForward();
        }
        else
        {
            // 멈춰있을 때는 걷기 애니메이션을 끔
            UpdateWalkAnimationClientRpc(false);
        }
    }

    void MoveForward()
    {
        // 1. 위치 이동
        transform.Translate(Vector2.right * direction.Value * moveSpeed * Time.deltaTime);

        // 2. 방향 반전 (서버에서 실행)
        // 프리팹의 기본 크기를 유지하면서 direction에 따라 X축만 뒤집습니다.
        float targetXScale = Mathf.Abs(transform.localScale.x) * direction.Value;

        // 값이 다를 때만 업데이트 (매 프레임 할당 방지)
        if (transform.localScale.x != targetXScale)
        {
            transform.localScale = new Vector3(targetXScale, transform.localScale.y, transform.localScale.z);
        }

        UpdateWalkAnimationClientRpc(true);
    }

    [ClientRpc]
    void StartAttackClientRpc()
    {
        if (anim != null)
        {
            anim.SetBool("walk", false);
            anim.SetTrigger("attack");
        }
    }

    [ClientRpc]
    void UpdateWalkAnimationClientRpc(bool isWalking)
    {
        if (anim != null)
        {
            anim.SetBool("walk", isWalking);
        }
    }

    // 👉 애니메이션 이벤트(타격 시점)에서 서버가 호출
    public void DealDamage()
    {
        if (!IsServer || currentEnemy == null) return;

        var stats = currentEnemy.GetComponent<UnitStats>();
        if (stats != null)
        {
            stats.TakeDamage(damage);
        }
    }

    // 👉 애니메이션 이벤트(끝 시점)에서 서버가 호출
    public void EndAttack()
    {
        if (IsServer)
        {
            isAttacking = false;
        }
    }
}