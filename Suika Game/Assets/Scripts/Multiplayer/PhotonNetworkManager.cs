using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Photon PUN2 연결 및 룸 관리 싱글톤
/// MultiModeScene에 빈 GameObject "PhotonNetworkManager"에 부착
/// </summary>
public class PhotonNetworkManager : MonoBehaviourPunCallbacks
{
    public static PhotonNetworkManager Instance { get; private set; }

    public const byte MAX_PLAYERS = 2;
    public const string GAME_VERSION = "1.0";

    // 연결 상태를 외부에서 구독할 수 있는 이벤트
    public System.Action<string> OnStatusChanged;
    public System.Action<System.Collections.Generic.List<RoomInfo>> OnRoomListUpdated;
    public System.Action<string> OnError;

    // 룸 목록 캐시 (씬 전환 후에도 유지)
    public System.Collections.Generic.List<RoomInfo> CachedRoomList { get; private set; }
        = new System.Collections.Generic.List<RoomInfo>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = GAME_VERSION;
    }

    // ─────────────────────────────────────────────
    // 공개 메서드
    // ─────────────────────────────────────────────

    /// <summary>Photon 마스터 서버에 연결</summary>
    public void Connect()
    {
        if (PhotonNetwork.IsConnected)
        {
            NotifyStatus("이미 연결됨");
            return;
        }
        NotifyStatus("연결 중...");
        PhotonNetwork.ConnectUsingSettings();
    }

    /// <summary>새 룸 생성 (2인 비공개)</summary>
    public void CreateRoom(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
        {
            roomName = "Room_" + Random.Range(1000, 9999);
        }

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = MAX_PLAYERS,
            IsVisible = true,
            IsOpen = true
        };

        NotifyStatus($"방 생성 중: {roomName}");
        PhotonNetwork.CreateRoom(roomName, options);
    }

    /// <summary>룸 이름으로 참가</summary>
    public void JoinRoom(string roomName)
    {
        NotifyStatus($"방 참가 중: {roomName}");
        PhotonNetwork.JoinRoom(roomName);
    }

    /// <summary>빠른 참가 (랜덤 룸)</summary>
    public void JoinRandomRoom()
    {
        NotifyStatus("빠른 참가 중...");
        PhotonNetwork.JoinRandomRoom();
    }

    /// <summary>룸/로비 나가기</summary>
    public void Disconnect()
    {
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
        else
            PhotonNetwork.Disconnect();
    }

    // ─────────────────────────────────────────────
    // Photon 콜백
    // ─────────────────────────────────────────────

    public override void OnConnectedToMaster()
    {
        ;  NotifyStatus("서버 연결 완료. 로비 참가 중...");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        NotifyStatus("로비 입장 완료");
    }

    public override void OnRoomListUpdate(System.Collections.Generic.List<RoomInfo> roomList)
    {
        Debug.Log($"[Photon] OnRoomListUpdate 호출됨 - 수신 방 수: {roomList.Count}");
        foreach (RoomInfo info in roomList)
        {
            Debug.Log($"[Photon]   방: {info.Name}, 삭제여부: {info.RemovedFromList}, 인원: {info.PlayerCount}/{info.MaxPlayers}");
            if (info.RemovedFromList)
                CachedRoomList.RemoveAll(r => r.Name == info.Name);
            else
            {
                int idx = CachedRoomList.FindIndex(r => r.Name == info.Name);
                if (idx >= 0)
                    CachedRoomList[idx] = info;
                else
                    CachedRoomList.Add(info);
            }
        }
        Debug.Log($"[Photon] 캐시 방 수: {CachedRoomList.Count}, 구독자 수: {OnRoomListUpdated?.GetInvocationList().Length ?? 0}");
        OnRoomListUpdated?.Invoke(CachedRoomList);
    }

    /// <summary>로비 재참가로 방 목록 강제 갱신</summary>
    public void RefreshRoomList()
    {
        if (!PhotonNetwork.IsConnected) return;
        CachedRoomList.Clear();
        PhotonNetwork.JoinLobby();
    }

    public override void OnCreatedRoom()
    {
        NotifyStatus($"방 생성 완료: {PhotonNetwork.CurrentRoom.Name}");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        OnError?.Invoke($"방 생성 실패: {message}");
    }

    public override void OnJoinedRoom()
    {
        NotifyStatus($"플레이어를 기다리는 중... ({PhotonNetwork.CurrentRoom.PlayerCount}/{MAX_PLAYERS})");

        // 방장이고 2명이 모이면 게임 씬으로 이동
        if (PhotonNetwork.IsMasterClient &&
            PhotonNetwork.CurrentRoom.PlayerCount == MAX_PLAYERS)
        {
            LoadMultiGameScene();
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        NotifyStatus($"플레이어 입장 ({PhotonNetwork.CurrentRoom.PlayerCount}/{MAX_PLAYERS})");

        if (PhotonNetwork.IsMasterClient &&
            PhotonNetwork.CurrentRoom.PlayerCount == MAX_PLAYERS)
        {
            LoadMultiGameScene();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        NotifyStatus("다른 플레이어가 나갔습니다.");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        OnError?.Invoke($"방 참가 실패: {message}");
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        // 빠른 참가 실패 시 새 룸 생성
        NotifyStatus("방 생성 중...");
        CreateRoom("");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        NotifyStatus($"온라인 해제: {cause}");
    }

    // ─────────────────────────────────────────────
    // 내부 헬퍼
    // ─────────────────────────────────────────────

    private void LoadMultiGameScene()
    {
        // AutomaticallySyncScene = true 이므로 방장이 호출하면 모두 이동
        PhotonNetwork.LoadLevel("MultiModeScene");
    }

    private void NotifyStatus(string message)
    {
        Debug.Log($"[Photon] {message}");
        OnStatusChanged?.Invoke(message);
    }
}
