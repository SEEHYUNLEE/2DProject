using System;
using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class Arrow : NetworkBehaviour
{
    private Vector3 startPos;
    private Vector3 targetPos;
    protected int damage; // int로 변경 (보통 데미지는 정수)
    protected int shooterTeamIndex;
    protected float flightTime = 0.8f;
    protected float timer = 0f;
    protected float arcHeight = 2.0f;
    [SerializeField] protected float targetYOffset = 1.0f;

    protected SpriteRenderer spriteRenderer;

    [SerializeField] private AudioClip hitSound;

    void Start()
    {
        // 🟢 컴포넌트 가져오기
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 🟢 생성 직후 투명하게 만들고 코루틴 시작
        if (spriteRenderer != null)
        {
            StartCoroutine(FadeInEffect());
        }
    }

    IEnumerator FadeInEffect()
    {
        // 잠시 투명하게 설정 (알파값 0)
        Color color = spriteRenderer.color;
        color.a = 0f;
        spriteRenderer.color = color;

        yield return new WaitForSeconds(0.15f);

        // 다시 불투명하게 설정 (알파값 1)
        color.a = 1f;
        spriteRenderer.color = color;
    }

    public void Setup(Transform target, int damage, int shooterTeamIndex)
    {
        this.damage = damage;
        this.shooterTeamIndex = shooterTeamIndex;
        this.startPos = transform.position;

        // 🟢 targetPos의 Y축에 targetYOffset을 더해 화살이 높게 날아가게 합니다.
        if (target != null)
        {
            this.targetPos = target.position + new Vector3(0, targetYOffset, 0);
        }
        else
        {
            Vector3 shootDirection = (shooterTeamIndex == 1) ? Vector3.right : Vector3.left;
            // 타겟이 없으면 현재 화살이 바라보는 방향으로 멀리 날아가도록 설정
            this.targetPos = transform.position + (shootDirection * 8.0f);
        }
    }

    void Update()
    {
        if (!IsServer) return;

        Vector3 lastPos = transform.position;

        timer += Time.deltaTime / flightTime;

        Vector3 currentPos = Vector3.Lerp(startPos, targetPos, timer);
        float height = Mathf.Sin(timer * Mathf.PI) * arcHeight;
        transform.position = currentPos + new Vector3(0, height, 0);

        Vector3 moveDir = transform.position - lastPos;
        if (moveDir != Vector3.zero)
        {
            // 2D 게임 기준: Z축 회전 적용
            float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, 0.2f); // 범위는 적절히 조절
        foreach (var hit in hitColliders)
        {
            var unit = hit.GetComponent<UnitStats>();
            var w = hit.GetComponent<Warrior>();
            if (unit != null && w.teamIndex.Value != shooterTeamIndex)
            {
                unit.TakeDamage(damage);
                DespawnArrow(); // 🟢 적을 맞추면 즉시 사라짐
                return; // 로직 종료
            }

            var b = hit.GetComponent<Base>();
            if (b != null && b.teamIndex != shooterTeamIndex)
            {
                b.TakeDamage(damage);
                DespawnArrow(); // 🟢 기지를 맞추면 즉시 사라짐
                return;
            }
        }

        // 3. [도착 판정] - 끝까지 날아갔을 때
        if (timer >= 1.2f)
        {
            DespawnArrow();
        }
    }

    private void DespawnArrow()
    {
        PlayHitSoundClientRpc(transform.position);

        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn(true);
        }
    }

    [ClientRpc]
    private void PlayHitSoundClientRpc(Vector3 position)
    {
        if (hitSound != null)
        {
            Vector3 adjustedPosition = new Vector3(position.x, position.y, -10f);
            //화살이 있던 위치에서 z축은 카메라로 소리 재생
            AudioSource.PlayClipAtPoint(hitSound, adjustedPosition);
        }
    }
}