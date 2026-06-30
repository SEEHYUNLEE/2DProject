using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI; // UI 사용용

public class Base : NetworkBehaviour
{
    // 1: A팀 기지, 2: B팀 기지
    public int teamIndex;

    public int maxHp = 1000;
    // 🔴 체력은 서버만 수정 가능, 클라이언트는 읽기만 가능하도록 동기화
    public NetworkVariable<int> currentHp = new NetworkVariable<int>(1000,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("UI 세팅")]
    [SerializeField] private Slider hpSlider; // 에디터에서 기지의 Slider를 드래그 앤 드롭 하세요.
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hitSound;

    public override void OnNetworkSpawn()
    {
        audioSource = GetComponent<AudioSource>();
        // 서버가 처음 켜질 때 최대 체력으로 초기화
        if (IsServer)
        {
            currentHp.Value = maxHp;
        }

        // 체력이 바뀔 때마다 Slider를 업데이트하는 이벤트 등록
        currentHp.OnValueChanged += OnHpChanged;
        UpdateHpUI(currentHp.Value);
    }

    public override void OnNetworkDespawn()
    {
        currentHp.OnValueChanged -= OnHpChanged;
    }

    public void TakeDamage(int damage)
    {
        if (!IsServer) return;

        // 이미 게임이 끝났다면 체력을 더 깎지 않음
        if (MultiplayerManager.Instance != null && MultiplayerManager.Instance.isGameOver) return;

        currentHp.Value = Mathf.Max(0, currentHp.Value - damage);

        PlayHitSoundClientRpc();

        if (currentHp.Value <= 0)
        {
            // 🟢 내 기지가 터졌음을 매니저에게 제보 (teamIndex: 1 또는 2)
            if (MultiplayerManager.Instance != null)
            {
                MultiplayerManager.Instance.OnBaseDestroyed(teamIndex);
            }
        }
    }

    private void OnHpChanged(int previousValue, int newValue)
    {
        UpdateHpUI(newValue);
    }

    private void UpdateHpUI(int hp)
    {
        if (hpSlider != null)
        {
            hpSlider.value = (float)hp / maxHp; // 0 ~ 1 사이 비율로 변환
        }
    }

    [ClientRpc]
    private void PlayHitSoundClientRpc()
    {
        if (audioSource != null && hitSound != null)
        {
            audioSource.PlayOneShot(hitSound);
        }
    }
}