using System;
using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MultiplayerManager : NetworkBehaviour
{
    public static MultiplayerManager Instance;

    // [SerializeField] = private 기능이지만 유니티 에디터에서 변경 가능
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
    [SerializeField] Button backToHomeButton;

    [SerializeField] GameObject warriorPrefab;
    [SerializeField] GameObject archerPrefab;
    [SerializeField] GameObject greatwarriorPrefab;
    [SerializeField] GameObject greatarcherPrefab;
    [SerializeField] GameObject alienPrefab;

    [SerializeField] GameObject BlueWins;
    [SerializeField] GameObject RedWins;
    [SerializeField] GameObject GameUi;
    [SerializeField] GameObject MenuUi;

    [SerializeField] int maxPlayers = 2;
    [SerializeField] string gameSceneName = "GameScene";

    public Transform teamASpawnPoint;
    public Transform teamBSpawnPoint;

    bool isRejected = false;

    // 값을 변경하는 것은 이 스크립트에서만 가능
    public bool isGameOver { get; private set; } = false;

    // async, await = 비동기 방식으로 구현
    // 유니티 클라우드 접속
    async void Awake()
    {
        // MultiplayerManager 중복될 경우 처리
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 완료될 동안 대기
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            // 익명 로그인
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnect;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnServerClientDisconnect;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // StartScene 초기화
        if (scene.name == "StartScene")
        {
            // 시간축 복구
            Time.timeScale = 1f;

            mainMenuPanel = GameObject.Find("MainMenu");
            lobbyPanel = GameObject.Find("Lobby");

            GameObject inputObj = GameObject.Find("CodeInputField");
            if (inputObj != null)
            {
                codeInput = inputObj.GetComponent<TMP_InputField>();
            }

            GameObject codeTextObj = GameObject.Find("DisplayCodeText");
            if (codeTextObj != null)
            {
                displayCodeText = codeTextObj.GetComponent<TMP_Text>();
            }

            GameObject hostBtnObj = GameObject.Find("HostButton");
            if (hostBtnObj != null)
            {
                hostButton = hostBtnObj.GetComponent<Button>();
                hostButton.onClick.RemoveAllListeners();
                hostButton.onClick.AddListener(CreateRoom);
                hostButton.interactable = true;
            }

            GameObject clientBtnObj = GameObject.Find("ClientButton");
            if (clientBtnObj != null)
            {
                clientButton = clientBtnObj.GetComponent<Button>();
                clientButton.onClick.RemoveAllListeners();
                clientButton.onClick.AddListener(JoinRoom);
                clientButton.interactable = true;
            }

            GameObject backBtnObj = GameObject.Find("BackToMenuButton");
            if (backBtnObj != null) backBtnObj.GetComponent<Button>().onClick.AddListener(LeaveLobby);


            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
            if (lobbyPanel != null) lobbyPanel.SetActive(false);
        }
        // GameScene 초기화
        else if (scene.name == gameSceneName)
        {
            isGameOver = false;

            GameObject homeBtnObj = GameObject.Find("HomeButton");
            if (homeBtnObj != null) homeBtnObj.GetComponent<Button>().onClick.AddListener(OnHomeButtonClicked);

            BlueWins = GameObject.Find("BlueWins");
            RedWins = GameObject.Find("RedWins");
            GameUi = GameObject.Find("GameUi");
            MenuUi = GameObject.Find("MenuUi");

            if (BlueWins != null) BlueWins.SetActive(false);
            if (RedWins != null) RedWins.SetActive(false);
            if (GameUi != null) GameUi.SetActive(true);
            if (MenuUi != null) MenuUi.SetActive(true);

            GameObject spawnPointA = GameObject.Find("SpawnPointA");
            GameObject spawnPointB = GameObject.Find("SpawnPointB");
            if (spawnPointA != null) teamASpawnPoint = spawnPointA.transform;
            if (spawnPointB != null) teamBSpawnPoint = spawnPointB.transform;
        }
    }

    // 초기화면에 돌아올 때 MultiplayerManager 중복해서 존재
    // 그러므로 Awake에서 Destroy할 때 메모리 누수 방지
    public override void OnDestroy()
    {
        base.OnDestroy();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnect;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnServerClientDisconnect;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // 서버에서만 실행
        if (!NetworkManager.Singleton.IsServer) return;

        // 호스트 포함 현재 접속된 사람 수가 maxPlayers에 도달하면
        if (NetworkManager.Singleton.ConnectedClientsList.Count == maxPlayers)
        {
            // 씬 전환 진행할 것이므로 미리 정리
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

            // Netcode 전용 SceneManager를 이용해 호스트+클라이언트 전원을 이동
            // 두 번째 매개변수 Single = 기존 화면 지우기, Additive = 더하기(맵 확장, 팝업창)
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
                // 플레이어가 서버와 다이렉트로 통신
                myPlayer.RequestSpawn(type);
            }
        }
    }

    // 서버에서 접속 인원을 체크
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        bool approve = NetworkManager.Singleton.ConnectedClientsList.Count < maxPlayers;

        response.Approved = approve;
        response.CreatePlayerObject = approve;

        if (!approve)
        {
            response.Reason = "방이 가득 찼습니다.";
        }
    }

    // 클라이언트 접속 거절 사유 출력
    private void OnDisconnect(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            // 서버가 보낸 거절 사유
            string reason = NetworkManager.Singleton.DisconnectReason;

            if (!string.IsNullOrEmpty(reason))
            {
                isRejected = true;

                if (displayCodeText != null)
                {
                    displayCodeText.text = "The room is full";
                }

            }
            else
            {
                if (lobbyPanel != null) lobbyPanel.SetActive(false);
                if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
            }
        }
    }

    async void CreateRoom()
    {
        // 더블클릭 방지
        hostButton.interactable = false;
        clientButton.interactable = false;

        try
        {
            // 이전 연결 정보가 남아있을 수 있으므로 정리
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
                // Shutdown은 비동기 작업일 수 있으므로 잠깐 대기
                await Task.Delay(500);
            }

            // Relay로 서버 연결
            Allocation alloc = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

            displayCodeText.text = joinCode;

            // 네트워크망 구축, dtls = 암호화된 보안 UDP 통신
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(alloc, "dtls"));

            // 중복 구독 방지
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            NetworkManager.Singleton.StartHost();

            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (lobbyPanel != null) lobbyPanel.SetActive(true);
        }
        catch (Exception)
        {
            hostButton.interactable = true;
            clientButton.interactable = true;
        }
    }

    void LeaveLobby()
    {
        hostButton.interactable = true;
        clientButton.interactable = true;

        if (NetworkManager.Singleton != null)
        {
            // 끄기 전에 이벤트 먼저 해제
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

            NetworkManager.Singleton.Shutdown();
        }

        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (codeInput != null) codeInput.text = "";
    }

    async void JoinRoom()
    {
        string code = codeInput.text;
        if (string.IsNullOrEmpty(code)) return;

        // 더블클릭 방지
        hostButton.interactable = false;
        clientButton.interactable = false;

        try
        {
            isRejected = false;

            // 로비 창으로 일단 진입
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (lobbyPanel != null) lobbyPanel.SetActive(true);
            if (displayCodeText != null) displayCodeText.text = "Loading...";

            // 릴레이 참가 정보 가져오기
            var joinAlloc = await RelayService.Instance.JoinAllocationAsync(code);

            // 네트워크 트랜스포트 설정
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAlloc, "dtls"));

            NetworkManager.Singleton.StartClient();

            StartCoroutine(CheckGhostRoomTimeout());
        }
        catch (Exception)
        {
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

        // 3초가 지났는데도 연결 상태가 아니라면 유령방
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsConnectedClient)
        {
            // 넷코드 종료
            NetworkManager.Singleton.Shutdown();

            // 메인메뉴로 복귀
            if (lobbyPanel != null) lobbyPanel.SetActive(false);
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        }
    }

    // Player에서 서버를 통해 소환 확정시키면 실행
    public void SpawnUnit(ulong clientId, UnitType type)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        // 호스트는 항상 0번
        int team = (clientId == 0) ? 1 : 2;
        float moveDir = (team == 1) ? 1f : -1f;
        Transform selectedPoint = (team == 1) ? teamASpawnPoint : teamBSpawnPoint;

        // 타입에 따른 프리팹 선택
        GameObject prefabToSpawn = type switch
        {
            UnitType.Warrior => warriorPrefab,
            UnitType.Archer => archerPrefab,
            UnitType.GreatWarrior => greatwarriorPrefab,
            UnitType.GreatArcher => greatarcherPrefab,
            UnitType.Alien => alienPrefab,
            _ => warriorPrefab // 기본값
        };

        float yOffset = type switch
        {
            UnitType.Warrior => 0.1f,
            UnitType.Archer => 0.2f,
            UnitType.GreatWarrior => 0.3f,
            UnitType.GreatArcher => 0.3f,
            _ => 0.1f  // 기본값
        };
        Vector3 spawnPosition = new Vector3(selectedPoint.position.x, selectedPoint.position.y - yOffset, selectedPoint.position.z);

        // 선택된 프리팹으로 생성
        GameObject unit = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);

        // Spawn 전에 프리팹 설정 변경
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

        // Warrior, Alien 스크립트 제어
        var warrior = unit.GetComponent<Warrior>();
        if (warrior != null)
        {
            warrior.teamIndex.Value = team;
            warrior.direction.Value = (int)moveDir;
        }

        var alien = unit.GetComponent<Alien>();
        if (alien != null)
        {
            alien.teamIndex.Value = team;
            alien.direction.Value = (int)moveDir;
        }
    }

    public void OnHomeButtonClicked()
    {     
        if (NetworkManager.Singleton.IsServer)
        {
            StartCoroutine(ShutdownSequence());
        }
        else if (NetworkManager.Singleton.IsClient)
        {
            // 클라이언트가 나간 것도 서버로 보내기
            RequestReturnToMenuServerRpc();
        }
    }

    private IEnumerator ShutdownSequence()
    {
        // 서버가 호출하면 모두 강제 이동
        NetworkManager.Singleton.SceneManager.LoadScene("StartScene", LoadSceneMode.Single);

        // 잠시 대기
        yield return new WaitForSeconds(0.5f);

        // 서버 종료
        NetworkManager.Singleton.Shutdown();
    }

    private void OnServerClientDisconnect(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            // 클라이언트가 나가고 호스트는 아직 게임 씬에 있다면
            if (SceneManager.GetActiveScene().name == gameSceneName)
            {
                // 서버를 종료합니다.
                StartCoroutine(ShutdownSequence());
            }
        }
    }

    public void OnBaseDestroyed(int failedTeamIndex)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        // 중복 실행 방지
        if (isGameOver) return;

        int winnerTeam = (failedTeamIndex == 1) ? 2 : 1;

        StopGameClientRpc(winnerTeam);
    }

    // 서버가 클라이언트 제어
    [ClientRpc]
    private void StopGameClientRpc(int winnerTeam)
    {
        isGameOver = true;

        // 유니티 시간 정지
        Time.timeScale = 0f;

        if (winnerTeam == 1)
        {
            if (BlueWins != null) BlueWins.SetActive(true);
        }
        else
        {
            if (RedWins != null) RedWins.SetActive(true);
        }
        if (GameUi != null) GameUi.SetActive(false);
        if (MenuUi != null) MenuUi.SetActive(false);
        
        StartCoroutine(ReturnToMenuAfterDelay(5f));
    }

    private IEnumerator ReturnToMenuAfterDelay(float delay)
    {
        // Time.timeScale = 0을 사용하여 unscaledTime 사용
        float start = Time.unscaledTime;
        while (Time.unscaledTime - start < delay)
        {
            yield return null;
        }

        // 메뉴로 보내는 함수 실행
        OnHomeButtonClicked();
    }

    // OnSceneLoaded 구독 및 해제(메모리 누수 방지)
    private void OnEnable()
    {
        // 씬 로드 이벤트 구독
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        // 씬 로드 이벤트 구독 해제
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 클라이언트가 나가면 서버에게 알리기
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestReturnToMenuServerRpc()
    {
        StartCoroutine(ShutdownSequence());
    }
}
