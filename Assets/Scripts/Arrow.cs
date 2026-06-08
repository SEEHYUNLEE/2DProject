using System;
using Unity.Netcode;
using UnityEngine;

public class Arrow : NetworkBehaviour
{
    private Vector3 startPos;
    private Vector3 targetPos;
    private int damage; // int로 변경 (보통 데미지는 정수)
    private int shooterTeamIndex;
    private float flightTime = 0.8f;
    private float timer = 0f;
    private float arcHeight = 3.0f;
    [SerializeField] private float targetYOffset = 1.0f;

    public void Setup(Transform target, int damage, int shooterTeamIndex)
    {
        this.damage = damage;
        this.shooterTeamIndex = shooterTeamIndex;
        this.startPos = transform.position;

        // 🟢 targetPos의 Y축에 targetYOffset을 더해 화살이 높게 날아가게 합니다.
        if (target != null)
            this.targetPos = target.position + new Vector3(0, targetYOffset, 0);
        else
            this.targetPos = transform.position + transform.right;
    }

    void Update()
    {
        if (!IsServer) return;

        Vector3 lastPos = transform.position;

        timer += Time.deltaTime / flightTime;

        // 1. 포물선 위치 계산
        Vector3 currentPos = Vector3.Lerp(startPos, targetPos, timer);
        float height = Mathf.Sin(timer * Mathf.PI) * arcHeight;
        Vector3 newPos = currentPos + new Vector3(0, height, 0);

        transform.position = newPos;

        Vector3 moveDir = newPos - lastPos;
        if (moveDir != Vector3.zero)
        {
            // 2D 게임 기준: Z축 회전 적용
            float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        var allUnits = FindObjectsByType<UnitStats>(FindObjectsSortMode.None);
        foreach (var unit in allUnits)
        {
            var warrior = unit.GetComponent<Warrior>();
            if (warrior != null && warrior.teamIndex.Value != this.shooterTeamIndex)
            {
                float dist = Vector2.Distance(transform.position, unit.transform.position);
                if (dist < 0.5f)
                {
                    unit.TakeDamage(damage);
                    GetComponent<NetworkObject>().Despawn(true);
                    return;
                }
            }
        }

        var allBases = FindObjectsByType<Base>(FindObjectsSortMode.None);
        foreach (var b in allBases)
        {
            // 내 팀의 기지가 아니면 데미지
            if (b.teamIndex != this.shooterTeamIndex)
            {
                if (Vector2.Distance(transform.position, b.transform.position) < 1.5f) // 기지는 크니까 반경을 좀 더 크게
                {
                    b.TakeDamage(damage); // 기지 데미지 함수
                    GetComponent<NetworkObject>().Despawn(true);
                    return;
                }
            }
        }

        if (timer >= 1.5f)
        {
            GetComponent<NetworkObject>().Despawn(true);
        }
    }
}