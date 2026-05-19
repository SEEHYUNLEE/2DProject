using System;
using System.Diagnostics;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MultiplayerManager : MonoBehaviour
{
    public static MultiplayerManager Instance;

    [SerializeField] GameObject mainMenuPanel;
    [SerializeField] GameObject lobbyPanel;

    [SerializeField] Button hostButton;
    [SerializeField] Button clientButton;
    [SerializeField] TMP_InputField codeInput;
    [SerializeField] TMP_Text displayCodeText;
    [SerializeField] Button spawnWarriorButton;
    [SerializeField] Button backToMenuButton;

    [SerializeField] int maxPlayers = 2; // 최대 인원 설정
    [SerializeField] GameObject unitPrefab;
    [SerializeField] string gameSceneName = "GameScene";

    public Transform teamASpawnPoint;
    public Transform teamBSpawnPoint;

    async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        //if (Instance == null) { Instance = this; }

        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    void Start()
    {
        hostButton.onClick.AddListener(CreateRoom);
        clientButton.onClick.AddListener(JoinRoom);

        //spawnWarriorButton.onClick.AddListener(OnSpawnButtonClicked);

        backToMenuButton.onClick.AddListener(LeaveLobby);

        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (lobbyPanel != null) lobbyPanel.SetActive(false);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnect;
        }
    }

    private void OnDestroy()
    {
        // 메모리 누수 방지를 위한 이벤트 해제
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnect;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        // 호스트 포함 현재 접속된 클라이언트 수가 maxPlayers(2명)에 도달하면
        if (NetworkManager.Singleton.ConnectedClientsList.Count == maxPlayers)
        {
            UnityEngine.Debug.Log("모든 플레이어가 접속했습니다. 게임 씬으로 이동합니다.");

            // 씬 전환이 일어날 때 이 콜백이 계속 남아 방해하지 않도록 해제해 줍니다.
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

            // ★ 중요: Netcode 전용 SceneManager를 이용해 호스트+클라이언트 전원을 이동시킵니다.
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    public void OnSpawnButtonClicked()
    {
        // 씬에 있는 내 Player 오브젝트를 찾아서 ServerRpc 실행
        if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            var myPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<Player>();
            if (myPlayer != null)
            {
                myPlayer.RequestSpawn();
            }
        }
    }

    // 1. 서버(호스트)가 접속 인원을 체크하는 로직
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // 현재 접속 인원(호스트 포함)이 최대치보다 적은지 확인
        bool approve = NetworkManager.Singleton.ConnectedClientsList.Count < maxPlayers;

        response.Approved = approve;
        response.CreatePlayerObject = approve;

        if (!approve)
        {
            // 🔥 이 메시지가 클라이언트에게 전달됩니다.
            response.Reason = "방이 가득 찼습니다.";
            //UnityEngine.Debug.LogWarning("인원 초과로 새로운 플레이어 접속을 거절했습니다.");
        }
    }

    // 2. 클라이언트가 접속 거절 사유를 출력하는 로직
    private void OnDisconnect(ulong clientId)
    {
        // 내(클라이언트) 연결이 끊겼을 때만 처리
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            // 서버가 보낸 거절 사유를 가져옴
            string reason = NetworkManager.Singleton.DisconnectReason;

            if (!string.IsNullOrEmpty(reason))
            {
                // 여기서 클라이언트의 디버그 창에 빨간색으로 사유가 뜹니다.
                UnityEngine.Debug.LogError($"[접속 실패 사유]: {reason}");
            }
            else
            {
                UnityEngine.Debug.Log("서버와의 연결이 끊겼습니다.");
            }
        }
    }

    async void CreateRoom()
    {
        try
        {
            // 릴레이 서버 할당 (2인용)
            Allocation alloc = await RelayService.Instance.CreateAllocationAsync(2);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

            displayCodeText.text = joinCode;

            // 네트워크 트랜스포트 설정
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(alloc, "dtls"));

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            NetworkManager.Singleton.StartHost();

            //UnityEngine.Debug.Log($"방 생성 완료! 코드: {joinCode}");
            // 화면에 코드를 표시하고 싶다면 여기서 joinCode를 UI에 띄워주세요.

            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (lobbyPanel != null) lobbyPanel.SetActive(true);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"방 생성 에러: {e.Message}");
        }
    }

    void LeaveLobby()
    {
        UnityEngine.Debug.Log("로비를 취소하고 메인 메뉴로 돌아갑니다.");

        if (NetworkManager.Singleton != null)
        {
            // 1. 내가 호스트였다면, 연결된 다른 사람들을 모두 끊어버립니다.
            if (NetworkManager.Singleton.IsServer)
            {
                // 호스트가 나가면 방 자체가 완전히 폭파되도록 셔트다운을 먼저 합니다.
                NetworkManager.Singleton.Shutdown();
            }
            else
            {
                // 일반 클라이언트라면 그냥 나만 나갑니다.
                NetworkManager.Singleton.Shutdown();
            }

            // 2. 이벤트 구독 해제
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }

        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);

        //displayCodeText.text = "Waiting for Code...";
        //codeInput.text = "";
    }

    async void JoinRoom()
    {
        try
        {
            string code = codeInput.text;
            if (string.IsNullOrEmpty(code)) return;

            // 릴레이 참가 정보 가져오기
            var joinAlloc = await RelayService.Instance.JoinAllocationAsync(code);

            // 네트워크 트랜스포트 설정
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAlloc, "dtls"));

            NetworkManager.Singleton.StartClient();
            UnityEngine.Debug.Log("서버 접속 시도 중");

            StartCoroutine(CheckGhostRoomTimeout());
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"참가 에러: {e.Message}");
        }
    }

    private IEnumerator CheckGhostRoomTimeout()
    {
        // 패널 전환 (로비 창으로 일단 진입)
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (lobbyPanel != null) lobbyPanel.SetActive(true);
        if (displayCodeText != null) displayCodeText.text = "Loading...";

        // 3초 동안 호스트가 응답해서 나를 완전히 연결해 주는지 기다립니다.
        yield return new WaitForSeconds(3.0f);

        // 3초가 지났는데도 나(클라이언트)가 서버와 '진짜 연결' 상태가 아니라면 유령방입니다!
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsConnectedClient)
        {
            UnityEngine.Debug.LogError("🚨 [유령 방 발견] 호스트가 존재하지 않습니다! 강제 퇴장 처리합니다.");

            // 넷코드 클라이언트 종료
            NetworkManager.Singleton.Shutdown();

            // UI 메인메뉴로 원상복구
            if (lobbyPanel != null) lobbyPanel.SetActive(false);
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        }
    }

    public void SpawnUnit(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        if (teamASpawnPoint == null || teamBSpawnPoint == null)
        {
            UnityEngine.Debug.Log("⚠️ 스폰 포인트가 비어있어 실시간으로 재연동을 시도합니다.");

            GameObject spawnPointA = GameObject.Find("SpawnPointA");
            GameObject spawnPointB = GameObject.Find("SpawnPointB");

            if (spawnPointA != null) teamASpawnPoint = spawnPointA.transform;
            if (spawnPointB != null) teamBSpawnPoint = spawnPointB.transform;
        }

        int team = (clientId == 0) ? 1 : 2;
        float moveDir = (team == 1) ? 1f : -1f;
        Transform selectedPoint = (team == 1) ? teamASpawnPoint : teamBSpawnPoint;

        float yOffset = 0.1f;
        Vector3 spawnPosition = new Vector3(selectedPoint.position.x, selectedPoint.position.y - yOffset, selectedPoint.position.z);
        GameObject unit = Instantiate(unitPrefab, spawnPosition, Quaternion.identity);

        var networkObj = unit.GetComponent<NetworkObject>();
        if (networkObj != null)
        {
            networkObj.Spawn();
        }

        var warrior = unit.GetComponent<Warrior>();
        if (warrior != null)
        {
            warrior.teamIndex.Value = team;
            warrior.direction.Value = (int)moveDir;
        }
    }
}
