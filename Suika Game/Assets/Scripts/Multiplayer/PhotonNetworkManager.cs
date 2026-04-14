using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Photon 연결과 룸 매칭을 관리하는 매니저.
/// LobbyScene에서 생성되어 씬 전환 후에도 유지된다.
/// </summary>
public class PhotonNetworkManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public static PhotonNetworkManager Instance { get; private set; }

    public const byte MAX_PLAYERS = 2;
    public const string GAME_VERSION = "1.0";
    private const string MultiModeSceneName = "MultiModeScene";

    private const byte EVT_MATCH_START = 200;
    private const string ROOM_PROP_MATCH_STARTED = "MatchStarted";

    public System.Action<string> OnStatusChanged;
    public System.Action<System.Collections.Generic.List<RoomInfo>> OnRoomListUpdated;
    public System.Action<string> OnError;
    public System.Action OnMatchFound;

    public System.Collections.Generic.List<RoomInfo> CachedRoomList { get; private set; }
        = new System.Collections.Generic.List<RoomInfo>();

    private Coroutine delayedRaiseEventCoroutine;
    private bool hasHandledMatchStart;

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
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDestroy()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

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

    public void JoinRoom(string roomName)
    {
        NotifyStatus($"방 참가 중: {roomName}");
        PhotonNetwork.JoinRoom(roomName);
    }

    public void JoinRandomRoom()
    {
        NotifyStatus("빠른 참가 중...");
        PhotonNetwork.JoinRandomRoom();
    }

    public void Disconnect()
    {
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
        else
            PhotonNetwork.Disconnect();
    }

    public override void OnConnectedToMaster()
    {
        NotifyStatus("서버 연결 완료. 로비 참가 중...");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        NotifyStatus("로비 입장 완료");
    }

    public override void OnRoomListUpdate(System.Collections.Generic.List<RoomInfo> roomList)
    {
        Debug.Log($"[Photon] OnRoomListUpdate 수신 - 방 수: {roomList.Count}");

        foreach (RoomInfo info in roomList)
        {
            Debug.Log($"[Photon] 방: {info.Name}, 제거여부: {info.RemovedFromList}, 인원: {info.PlayerCount}/{info.MaxPlayers}");

            if (info.RemovedFromList)
            {
                CachedRoomList.RemoveAll(r => r.Name == info.Name);
                continue;
            }

            int idx = CachedRoomList.FindIndex(r => r.Name == info.Name);
            if (idx >= 0)
                CachedRoomList[idx] = info;
            else
                CachedRoomList.Add(info);
        }

        OnRoomListUpdated?.Invoke(CachedRoomList);
    }

    public void RefreshRoomList()
    {
        if (!PhotonNetwork.IsConnected)
            return;

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
        hasHandledMatchStart = false;
        NotifyStatus($"플레이어 대기 중... ({PhotonNetwork.CurrentRoom.PlayerCount}/{MAX_PLAYERS})");

        if (TryHandleMatchStartFromRoomState())
            return;

        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.PlayerCount == MAX_PLAYERS)
            ScheduleMatchStart();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        NotifyStatus($"플레이어 입장 ({PhotonNetwork.CurrentRoom.PlayerCount}/{MAX_PLAYERS})");

        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.PlayerCount == MAX_PLAYERS)
            ScheduleMatchStart();
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
        NotifyStatus("빠른 참가 실패. 방을 생성합니다...");
        CreateRoom("");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        NotifyStatus($"연결 해제: {cause}");
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged != null && propertiesThatChanged.ContainsKey(ROOM_PROP_MATCH_STARTED))
        {
            Debug.Log($"[Photon] MatchStarted 룸 프로퍼티 변경 감지: {propertiesThatChanged[ROOM_PROP_MATCH_STARTED]}");
            TryHandleMatchStartFromRoomState();
        }
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != EVT_MATCH_START)
            return;

        Debug.Log("[Photon] EVT_MATCH_START 수신 -> OnMatchFound 호출");
        TriggerMatchFound();
    }

    public void TryLoadMultiModeScene()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[Photon] 방에 없는 상태라 MultiModeScene 로드를 건너뜁니다.");
            return;
        }

        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[Photon] 참가자는 방장의 PhotonNetwork.LoadLevel 동기화를 기다립니다.");
            return;
        }

        Debug.Log("[Photon] 방장이 PhotonNetwork.LoadLevel(MultiModeScene)을 호출합니다.");
        PhotonNetwork.LoadLevel(MultiModeSceneName);
    }

    private void ScheduleMatchStart()
    {
        if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom)
            return;

        if (IsMatchAlreadyStarted())
        {
            Debug.Log("[Photon] 이미 시작된 방입니다. 로컬 매치 시작만 처리합니다.");
            TriggerMatchFound();
            return;
        }

        if (delayedRaiseEventCoroutine != null)
        {
            Debug.Log("[Photon] 매치 시작 코루틴이 이미 예약되어 있습니다.");
            return;
        }

        delayedRaiseEventCoroutine = StartCoroutine(DelayedRaiseEvent());
    }

    private IEnumerator DelayedRaiseEvent()
    {
        yield return new WaitForSeconds(0.5f);
        delayedRaiseEventCoroutine = null;

        if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[Photon] 매치 시작 발신 시점에 방장 또는 방 상태가 유효하지 않습니다.");
            yield break;
        }

        if (PhotonNetwork.CurrentRoom.PlayerCount < MAX_PLAYERS)
        {
            Debug.LogWarning($"[Photon] 플레이어 수 부족으로 매치 시작 취소 ({PhotonNetwork.CurrentRoom.PlayerCount}/{MAX_PLAYERS})");
            yield break;
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { ROOM_PROP_MATCH_STARTED, true }
        });

        bool raised = PhotonNetwork.RaiseEvent(
            EVT_MATCH_START,
            null,
            new RaiseEventOptions
            {
                Receivers = ReceiverGroup.All,
                CachingOption = EventCaching.AddToRoomCache
            },
            SendOptions.SendReliable);

        Debug.Log($"[Photon] EVT_MATCH_START 발신 결과: {raised}");

        if (!raised)
        {
            Debug.LogWarning("[Photon] RaiseEvent 실패. 룸 프로퍼티 기반으로 로컬 매치 시작을 진행합니다.");
            TriggerMatchFound();
        }
    }

    private bool TryHandleMatchStartFromRoomState()
    {
        if (!PhotonNetwork.InRoom || !IsMatchAlreadyStarted())
            return false;

        Debug.Log("[Photon] 룸 상태에서 이미 매치 시작됨을 확인했습니다.");
        TriggerMatchFound();
        return true;
    }

    private bool IsMatchAlreadyStarted()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom?.CustomProperties == null)
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROOM_PROP_MATCH_STARTED, out object value))
            return false;

        return value is bool started && started;
    }

    private void TriggerMatchFound()
    {
        if (hasHandledMatchStart)
            return;

        hasHandledMatchStart = true;
        OnMatchFound?.Invoke();
    }

    private void NotifyStatus(string message)
    {
        Debug.Log($"[Photon] {message}");
        OnStatusChanged?.Invoke(message);
    }
}
