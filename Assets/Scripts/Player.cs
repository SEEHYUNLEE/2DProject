using System.Diagnostics;
using Unity.Netcode;
using UnityEngine;

public class Player : NetworkBehaviour
{
    // 1: A팀(호스트), 2: B팀(클라이언트)
    public NetworkVariable<int> teamIndex = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        // 내 로컬 플레이어 오브젝트가 생성되었을 때만 실행
        if (IsOwner)
        {
            // 서버에 내가 접속했음을 알리고 팀 배정을 요청
            RequestAssignTeamServerRpc();
        }
    }

    [ServerRpc]
    private void RequestAssignTeamServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        // 호스트는 1팀, 나머지는 2팀
        teamIndex.Value = (clientId == 0) ? 1 : 2;
        UnityEngine.Debug.Log($"[서버] 플레이어 {clientId} 팀 배정 완료: {teamIndex.Value}");
    }

    public void RequestSpawn()
    {
        if (IsOwner && teamIndex.Value != 0) // 팀이 정해진 후에만 소환 가능
        {
            SpawnUnitServerRpc();
        }
    }

    [ServerRpc]
    private void SpawnUnitServerRpc(ServerRpcParams rpcParams = default)
    {
        MultiplayerManager.Instance.SpawnUnit(rpcParams.Receive.SenderClientId);
    }
}