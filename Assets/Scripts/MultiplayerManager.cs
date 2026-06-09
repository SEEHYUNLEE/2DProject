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
    [SerializeField] Button spawnArcherButton;
    [SerializeField] Button spawnRandomButton;
    [SerializeField] Button backToMenuButton;

    [SerializeField] GameObject warriorPrefab;  // 기존 unitPrefab의 이름을 알아보기 쉽게 변경 권장
    [SerializeField] GameObject archerPrefab;
    [SerializeField] GameObject greatwarriorPrefab;
    [SerializeField] GameObject greatarcherPrefab;

    [SerializeField] int maxPlayers = 2; // 최대 인원 설정
    [SerializeField] string gameSceneName = "GameScene";

    public Transform teamASpawnPoint;
    public Transform teamBSpawnPoint;

    bool isRejected = false;

    public bool isGameOver { get; private set; } = false;

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

        if (spawnWarriorButton != null) spawnWarriorButton.onClick.AddListener(() => OnSpawnButtonClicked(UnitType.Warrior));
        if (spawnArcherButton != null) spawnArcherButton.onClick.AddListener(() => OnSpawnButtonClicked(UnitType.Archer));
        if (spawnRandomButton != null) spawnRandomButton.onClick.AddListener(() => OnSpawnButtonClicked(UnitType.Random));

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

    public void OnSpawnButtonClicked(UnitType type)
    {
        if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            var myPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<Player>();
            if (myPlayer != null)
            {
                // 플레이어에게 어떤 유닛을 뽑을지 요청
                myPlayer.RequestSpawn(type);
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
            response.Reason = "방이 가득 찼습니다.";
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

                isRejected = true;

                if (displayCodeText != null)
                {
                    displayCodeText.text = "The room is full"; // "방이 가득 찼습니다."가 화면에 표시됨
                }

            }
            else
            {
                UnityEngine.Debug.Log("서버와의 연결이 끊겼습니다.");

                if (lobbyPanel != null) lobbyPanel.SetActive(false);
                if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
            }
        }
    }

    async void CreateRoom()
    {
        try
        {
            // 릴레이 서버 할당 (2인용)
            Allocation alloc = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
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
            // ★ [순서 변경] 넷코드를 끄기 전에 이벤트를 '먼저' 안전하게 해제합니다.
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

            // 호스트든 클라이언트든 방을 나갈 때는 Shutdown() 하나로 처리가 가능합니다.
            // 호스트가 Shutdown하면 방이 터지고, 클라이언트가 하면 본인만 연결이 끊깁니다.
            NetworkManager.Singleton.Shutdown();
        }

        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (codeInput != null) codeInput.text = "";
    }

    async void JoinRoom()
    {
        try
        {
            string code = codeInput.text;
            if (string.IsNullOrEmpty(code)) return;

            isRejected = false;

            // 패널 전환 (로비 창으로 일단 진입)
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (lobbyPanel != null) lobbyPanel.SetActive(true);
            if (displayCodeText != null) displayCodeText.text = "Loading...";

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
            if (displayCodeText != null)
            {
                displayCodeText.text = "Code Not Found";
            }
        }
    }

    private IEnumerator CheckGhostRoomTimeout()
    {
        // 3초 동안 호스트가 응답해서 나를 완전히 연결해 주는지 기다립니다.
        yield return new WaitForSeconds(3.0f);

        if (isRejected) yield break;

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

    public void SpawnUnit(ulong clientId, UnitType type)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        // 1. 팀 및 방향 설정 (기존 코드 유지)
        if (teamASpawnPoint == null || teamBSpawnPoint == null)
        {
            GameObject spawnPointA = GameObject.Find("SpawnPointA");
            GameObject spawnPointB = GameObject.Find("SpawnPointB");
            if (spawnPointA != null) teamASpawnPoint = spawnPointA.transform;
            if (spawnPointB != null) teamBSpawnPoint = spawnPointB.transform;
        }

        int team = (clientId == 0) ? 1 : 2;
        float moveDir = (team == 1) ? 1f : -1f;
        Transform selectedPoint = (team == 1) ? teamASpawnPoint : teamBSpawnPoint;

        // 2. [추가] 타입에 따른 프리팹 선택 (랜덤의 경우 서버에서 최종 결정된 유닛이 들어옴)
        GameObject prefabToSpawn = type switch
        {
            UnitType.Warrior => warriorPrefab,
            UnitType.Archer => archerPrefab,
            UnitType.GreatWarrior => greatwarriorPrefab,
            UnitType.GreatArcher => greatarcherPrefab,
            _ => warriorPrefab // 정의되지 않은 경우 기본값
        };

        float yOffset = type switch
        {
            UnitType.Warrior => 0.1f,
            UnitType.Archer => 0.2f,
            UnitType.GreatWarrior => 0.3f, // 원하는 값으로 수정하세요
            UnitType.GreatArcher => 0.15f, // 원하는 값으로 수정하세요
            _ => 0.1f  // 기본값 (default)
        };
        Vector3 spawnPosition = new Vector3(selectedPoint.position.x, selectedPoint.position.y - yOffset, selectedPoint.position.z);

        // 선택된 프리팹으로 생성
        GameObject unit = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);

        Vector3 originalScale = unit.transform.localScale;
        unit.transform.localScale = new Vector3(
            Mathf.Abs(originalScale.x) * moveDir,
            originalScale.y,
            originalScale.z
        );

        var networkObj = unit.GetComponent<NetworkObject>();
        if (networkObj != null)
        {
            networkObj.Spawn();
        }

        // Warrior 스크립트(또는 공통 Unit 스크립트) 컴포넌트 제어
        // (궁수와 전사가 같은 베이스 컴포넌트를 쓴다고 가정하거나 각각 겟컴포넌트 하셔야 합니다)
        var warrior = unit.GetComponent<Warrior>();
        if (warrior != null)
        {
            warrior.teamIndex.Value = team;
            warrior.direction.Value = (int)moveDir;
        }
    }

    public void OnBaseDestroyed(int failedTeamIndex)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        // 이미 게임오버 처리가 되었다면 중복 실행 방지
        if (isGameOver) return;

        int winnerTeam = (failedTeamIndex == 1) ? 2 : 1;
        UnityEngine.Debug.Log($"🚨 [서버] {failedTeamIndex}팀 기지 파괴! 승자: {winnerTeam}팀. 게임을 정지합니다.");

        // 모든 클라이언트에게 게임을 멈추라고 명령
        StopGameClientRpc(winnerTeam);
    }

    [ClientRpc]
    private void StopGameClientRpc(int winnerTeam)
    {
        isGameOver = true;

        // 1. 유니티 물리 및 시간축 정지 (이동, 애니메이션 등 일시정지)
        Time.timeScale = 0f;

        // 2. UI 텍스트나 알림창을 띄우고 싶다면 여기에 작성하세요.
        if (displayCodeText != null)
        {
            displayCodeText.text = $"GAME OVER\nWinner: Team {winnerTeam}";
        }

        UnityEngine.Debug.Log($"[클라이언트] 게임 정지 완료. 승리팀: {winnerTeam}");
    }
}
