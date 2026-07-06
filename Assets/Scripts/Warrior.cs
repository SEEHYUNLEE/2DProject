using UnityEngine;
using Unity.Netcode;

public class Warrior : NetworkBehaviour
{
    public float moveSpeed = 2f;
    public int damage = 20;
    public float attackRange = 1.5f;
    public LayerMask unitLayer;
    [SerializeField] private LayerMask baseLayer; // 🟢 기지 감지를 위해 레이어 추가!

    // 1: A팀(오른쪽 이동), 2: B팀(왼쪽 이동)
    public NetworkVariable<int> teamIndex = new NetworkVariable<int>(0);
    public NetworkVariable<int> direction = new NetworkVariable<int>(1);

    protected bool isAttacking = false;
    protected bool isAttackingBase = false; // 🟢 기지 공격 모드 스위치
    protected Collider2D currentEnemy;
    protected Base targetBase;               // 🟢 공격 타겟 기지 컴포넌트 저장
    protected Animator anim;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip attackSound;
    [SerializeField] protected float attackVolume = 0.5f;

    // 네트워크 상에 오브젝트가 생성되고 데이터가 준비되었을 때 호출
    public override void OnNetworkSpawn()
    {
        anim = GetComponent<Animator>();
        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (!IsServer) return;

        if (MultiplayerManager.Instance != null && MultiplayerManager.Instance.isGameOver) return;

        if (isAttacking) return;

        // -------------------------------------------------------------
        // 기지 공격 모드가 활성화되면 주변 유닛 탐지를 생략하고 기지만 타겟팅
        // -------------------------------------------------------------
        if (isAttackingBase && targetBase != null)
        {
            if (targetBase.currentHp.Value <= 0)
            {
                isAttackingBase = false;
                targetBase = null;
            }
            else
            {
                // 기지가 살아있다면 유닛을 무시
                StartAttackClientRpc();
                isAttacking = true;
                return;
            }
        }

        // 내 앞에 상대방 기지가 사거리 내에 있는지 검사
        Collider2D baseTarget = Physics2D.OverlapCircle(transform.position, attackRange, baseLayer);
        if (baseTarget != null)
        {
            Base detectedBase = baseTarget.GetComponent<Base>();
            // 팀 인덱스로 확인
            if (detectedBase != null && detectedBase.teamIndex != this.teamIndex.Value)
            {
                targetBase = detectedBase;
                isAttackingBase = true; // 기지 공격 상태
                StartAttackClientRpc();
                isAttacking = true;
                return;
            }
        }

        // 주변 유닛 탐지
        Collider2D[] targets = Physics2D.OverlapCircleAll(transform.position, attackRange, unitLayer);

        bool shouldStop = false;

        foreach (var target in targets)
        {
            if (target.gameObject == gameObject) continue;

            Warrior targetWarrior = target.GetComponent<Warrior>();
            if (targetWarrior == null) continue;

            // 적군 발견
            if (targetWarrior.teamIndex.Value != this.teamIndex.Value)
            {
                currentEnemy = target;
                StartAttackClientRpc();
                isAttacking = true;
                return;
            }

            // 아군이 바로 앞에 있는 경우 (길막 방지)
            else if (targetWarrior.teamIndex.Value == this.teamIndex.Value)
            {
                float relativePosX = target.transform.position.x - transform.position.x;

                if ((direction.Value > 0 && relativePosX > 0) || (direction.Value < 0 && relativePosX < 0))
                {
                    if (Mathf.Abs(relativePosX) < 2f)
                    {
                        shouldStop = true;
                    }
                }
            }
        }

        if (!shouldStop)
        {
            MoveForward();
        }
        else
        {
            UpdateWalkAnimationClientRpc(false);
        }
    }

    void MoveForward()
    {
        transform.Translate(Vector2.right * direction.Value * moveSpeed * Time.deltaTime);

        float targetXScale = Mathf.Abs(transform.localScale.x) * direction.Value;

        if (transform.localScale.x != targetXScale)
        {
            transform.localScale = new Vector3(targetXScale, transform.localScale.y, transform.localScale.z);
        }

        UpdateWalkAnimationClientRpc(true);
    }

    [ClientRpc]
    protected void StartAttackClientRpc()
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

    [ClientRpc]
    protected void PlayAttackSoundClientRpc()
    {
        if (audioSource != null && attackSound != null)
        {
            audioSource.volume = attackVolume;
            audioSource.PlayOneShot(attackSound);
        }
    }

    // 애니메이션 이벤트 넣어서 실행
    public void DealDamage()
    {
        if (!IsServer) return;

        PlayAttackSoundClientRpc();

        if (isAttackingBase && targetBase != null)
        {
            targetBase.TakeDamage(damage);
            return;
        }

        if (currentEnemy != null)
        {
            var stats = currentEnemy.GetComponent<UnitStats>();
            if (stats != null)
            {
                stats.TakeDamage(damage);
            }
        }
    }

    public void EndAttack()
    {
        if (IsServer)
        {
            isAttacking = false;
        }
    }
}