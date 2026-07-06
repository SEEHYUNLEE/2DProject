using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class Alien : NetworkBehaviour
{
    public float moveSpeed = 6f;
    public float jumpWaitTime = 4f;
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
            StartCoroutine(MovementRoutine());
        }
    }

    IEnumerator MovementRoutine()
    {
        float elapsed = 0f;

        // 4초 동안 이동
        while (elapsed < jumpWaitTime)
        {
            transform.position += Vector3.right * direction.Value * moveSpeed * Time.deltaTime;
            elapsed += Time.deltaTime;
            yield return null;
        }

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
        if (!isLaserSpawned && lazerPrefab != null && lazerPoint != null)
        {
            // 팀 방향에 따라 회전
            Quaternion spawnRotation = lazerPoint.rotation;
            if (teamIndex.Value == 2)
            {
                spawnRotation *= Quaternion.Euler(0, 180, 0); // 180도 회전
            }

            GameObject lazer = Instantiate(lazerPrefab, lazerPoint.position, spawnRotation);
            var netObj = lazer.GetComponent<NetworkObject>();
            if (netObj != null) netObj.Spawn();

            isLaserSpawned = true;
        }
    }

    [ClientRpc] private void TriggerJumpClientRpc() => anim.SetTrigger("jump");
    [ClientRpc] private void TriggerAttackClientRpc() => anim.SetTrigger("attack");

    IEnumerator Attack()
    {
        yield return new WaitForSeconds(1.0f);
        Base[] bases = FindObjectsByType<Base>(FindObjectsSortMode.None);
        while (true)
        {
            foreach (var b in bases)
            {
                if (b == null) continue;

                if (b.teamIndex != this.teamIndex.Value)
                {
                    b.TakeDamage(damage);
                }
            }

            yield return new WaitForSeconds(1.0f);
        }
    }
}