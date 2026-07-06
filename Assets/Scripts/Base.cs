using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class Base : NetworkBehaviour
{
    // 1: A팀, 2: B팀
    public int teamIndex;

    public int maxHp = 1000;
    // 체력은 서버만 수정, 읽기는 모두 가능
    public NetworkVariable<int> currentHp = new NetworkVariable<int>(1000,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("슬라이더, 사운드")]
    [SerializeField] private Slider hpSlider;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hitSound;

    public override void OnNetworkSpawn()
    {
        audioSource = GetComponent<AudioSource>();
        // 최대 체력으로 초기화
        if (IsServer)
        {
            currentHp.Value = maxHp;
        }

        // 체력이 바뀔 때마다 이벤트 등록
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
        if (MultiplayerManager.Instance != null && MultiplayerManager.Instance.isGameOver) return;

        currentHp.Value = Mathf.Max(0, currentHp.Value - damage);

        PlayHitSoundClientRpc();

        if (currentHp.Value <= 0)
        {
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
            hpSlider.value = (float)hp / maxHp;
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