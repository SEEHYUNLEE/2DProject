using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class Alien : NetworkBehaviour
{
    public float moveSpeed = 6f;
    public float jumpWaitTime = 4f;  // 🟢 3초 대기
    public float peakHeight = 4f;
    public float jumpDuration = 1f;

    private Animator anim;

    public GameObject lazerPrefab;
    public Transform lazerPoint;
    public int damage = 100;
    private bool isLaserSpawned = false;

    public NetworkVariable<int> teamIndex = new NetworkVariable<int>(0);
    public NetworkVariable<int> direction = new NetworkVariable<int>(1);

    public override void OnNetworkSpawn()
    {
        anim = GetComponent<Animator>();

        if (IsServer)
        {
            // 서버에서 생성 직후 3초 카운트다운 시작
            StartCoroutine(MovementRoutine());
        }
    }

    IEnumerator MovementRoutine()
    {
        float elapsed = 0f;

        // 1. 3초 동안 이동
        while (elapsed < jumpWaitTime)
        {
            transform.position += Vector3.right * direction.Value * moveSpeed * Time.deltaTime;
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 2. 3초 후 점프 시작
        StartCoroutine(JumpRoutine());
    }

    IEnumerator JumpRoutine()
    {
        TriggerJumpClientRpc();

        Vector3 startPos = transform.position;
        Vector3 peakPos = startPos + new Vector3(direction.Value * 1f, peakHeight, 0);
        float elapsed = 0;

        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / jumpDuration;

            float newX = Mathf.Lerp(startPos.x, peakPos.x, t);
            float newY = Mathf.Lerp(startPos.y, peakPos.y, t) + Mathf.Sin(t * Mathf.PI) * 1f;

            transform.position = new Vector3(newX, newY, transform.position.z);
            yield return null;
        }
    }

    public void EndJump()
    {
        if (IsServer)
        {
            TriggerAttackClientRpc();
            StartCoroutine(Attack());
        }
    }

    public void Lazer()
    {
        // 1. 레이저 이펙트 생성 (서버에서 생성 후 NetworkObject로 Spawn)
        if (!isLaserSpawned && lazerPrefab != null && lazerPoint != null)
        {
            // 팀 방향에 따른 회전 보정
            Quaternion spawnRotation = lazerPoint.rotation;
            if (teamIndex.Value == 2)
            {
                spawnRotation *= Quaternion.Euler(0, 180, 0); // 180도 회전
            }

            GameObject lazer = Instantiate(lazerPrefab, lazerPoint.position, spawnRotation);
            var netObj = lazer.GetComponent<NetworkObject>();
            if (netObj != null) netObj.Spawn();

            isLaserSpawned = true; // 생성 완료 플래그 설정
        }
    }

    [ClientRpc] private void TriggerJumpClientRpc() => anim.SetTrigger("jump");
    [ClientRpc] private void TriggerAttackClientRpc() => anim.SetTrigger("attack");

    IEnumerator Attack()
    {
        yield return new WaitForSeconds(1.0f);
        while (true)
        {
            Base[] bases = FindObjectsByType<Base>(FindObjectsSortMode.None);
            foreach (var b in bases)
            {
                // 자신의 팀이 아닌 기지 찾기
                if (b.teamIndex != this.teamIndex.Value)
                {
                    b.TakeDamage(damage); // 🟢 기지의 데미지 처리 함수 호출
                }
            }

            yield return new WaitForSeconds(1.0f);
        }
    }
}