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

public class MultiplayerManager : MonoBehaviour
{
    [SerializeField] Button hostButton;
    [SerializeField] Button clientButton;
    [SerializeField] TMP_InputField codeInput;
    [SerializeField] TMP_Text displayCodeText;

    [SerializeField] int maxPlayers = 2; // 최대 인원 설정

    async void Awake()
    {
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

        // [서버 전용] 접속 승인 함수 등록
        NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;

        // [클라이언트 전용] 연결 해제(차단 포함) 시 실행될 함수 등록
        NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnect;
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
            UnityEngine.Debug.LogWarning("인원 초과로 새로운 플레이어 접속을 거절했습니다.");
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

            NetworkManager.Singleton.StartHost();

            UnityEngine.Debug.Log($"방 생성 완료! 코드: {joinCode}");
            // 화면에 코드를 표시하고 싶다면 여기서 joinCode를 UI에 띄워주세요.
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"방 생성 에러: {e.Message}");
        }
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
            UnityEngine.Debug.Log("서버 접속 성공");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"참가 에러: {e.Message}");
        }
    }
}