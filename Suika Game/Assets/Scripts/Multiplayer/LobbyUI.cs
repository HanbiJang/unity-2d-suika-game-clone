using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

/// <summary>
/// 로비 씬 UI 컨트롤러
///
/// [씬 구성 예시]
/// Canvas
///   ├─ PanelConnect       (서버 미연결 상태)
///   │    └─ BtnConnect
///   ├─ PanelLobby         (로비 진입 후)
///   │    ├─ InputRoomName
///   │    ├─ BtnCreate
///   │    ├─ BtnQuickJoin
///   │    ├─ ScrollView > Content  (룸 목록)
///   │    └─ BtnBack
///   ├─ PanelWaiting       (룸 입장 후 대기)
///   │    ├─ TxtRoomName
///   │    ├─ TxtPlayerCount
///   │    └─ BtnLeave
///   └─ TxtStatus          (상태 메시지, 항상 표시)
/// </summary>
public class LobbyUI : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private GameObject panelLobby;
    [SerializeField] private GameObject panelWaiting;

    [Header("로비 패널")]
    [SerializeField] private TMP_InputField inputRoomName;
    [SerializeField] private Button btnCreate;
    [SerializeField] private Button btnJoinByName; // 방 이름으로 참가
    [SerializeField] private Button btnQuickJoin;
    [SerializeField] private Button btnBack;
    [SerializeField] private Transform roomListContent;
    [SerializeField] private GameObject roomItemPrefab; // RoomListItem 프리팹

    [Header("대기 패널")]
    [SerializeField] private TMP_Text txtRoomName;
    [SerializeField] private TMP_Text txtPlayerCount;
    [SerializeField] private Button btnLeave;

    [Header("공통")]
    [SerializeField] private TMP_Text txtStatus;

    private List<RoomInfo> cachedRoomList = new List<RoomInfo>();

    private void Start()
    {
        if (PhotonNetworkManager.Instance == null)
            new GameObject("PhotonNetworkManager").AddComponent<PhotonNetworkManager>();

        btnCreate.onClick.AddListener(OnClickCreate);
        if (btnJoinByName != null) btnJoinByName.onClick.AddListener(OnClickJoinByName);
        btnQuickJoin.onClick.AddListener(OnClickQuickJoin);
        btnBack.onClick.AddListener(OnClickBack);
        btnLeave.onClick.AddListener(OnClickLeave);

        PhotonNetworkManager.Instance.OnStatusChanged += UpdateStatus;
        PhotonNetworkManager.Instance.OnRoomListUpdated += RefreshRoomList;
        PhotonNetworkManager.Instance.OnError += ShowError;

        // 이미 연결된 상태면 로비 패널 바로 표시, 아니면 자동 연결
        if (PhotonNetwork.IsConnected && PhotonNetwork.InLobby)
            ShowPanel(panelLobby);
        else
        {
            ShowPanel(panelLobby);
            PhotonNetworkManager.Instance.Connect();
            StartCoroutine(WaitAndShowLobby());
        }
    }

    private void OnDestroy()
    {
        if (PhotonNetworkManager.Instance == null) return;
        PhotonNetworkManager.Instance.OnStatusChanged -= UpdateStatus;
        PhotonNetworkManager.Instance.OnRoomListUpdated -= RefreshRoomList;
        PhotonNetworkManager.Instance.OnError -= ShowError;
    }

    // ─────────────────────────────────────────────
    // 버튼 핸들러
    // ─────────────────────────────────────────────

    private System.Collections.IEnumerator WaitAndShowLobby()
    {
        yield return new WaitUntil(() => PhotonNetwork.InLobby);
        ShowPanel(panelLobby);
    }

    private void OnClickCreate()
    {
        PhotonNetworkManager.Instance.CreateRoom(inputRoomName.text.Trim());
        StartCoroutine(WaitAndShowWaiting());
    }

    private void OnClickJoinByName()
    {
        string roomName = inputRoomName.text.Trim();
        if (string.IsNullOrEmpty(roomName))
        {
            ShowError("방 이름을 입력해주세요.");
            return;
        }
        PhotonNetworkManager.Instance.JoinRoom(roomName);
        StartCoroutine(WaitAndShowWaiting());
    }

    private void OnClickQuickJoin()
    {
        PhotonNetworkManager.Instance.JoinRandomRoom();
        StartCoroutine(WaitAndShowWaiting());
    }

    private System.Collections.IEnumerator WaitAndShowWaiting()
    {
        yield return new WaitUntil(() => PhotonNetwork.InRoom);
        UpdateWaitingPanel();
        ShowPanel(panelWaiting);
    }

    private void OnClickBack()
    {
        PhotonNetworkManager.Instance.Disconnect();
        SceneManager.LoadScene("StartScene");
    }

    private void OnClickLeave()
    {
        PhotonNetwork.LeaveRoom();
        ShowPanel(panelLobby);
    }

    // ─────────────────────────────────────────────
    // UI 갱신
    // ─────────────────────────────────────────────

    private void UpdateWaitingPanel()
    {
        if (!PhotonNetwork.InRoom) return;
        txtRoomName.text = $"룸: {PhotonNetwork.CurrentRoom.Name}";
        txtPlayerCount.text = $"플레이어: {PhotonNetwork.CurrentRoom.PlayerCount} / {PhotonNetworkManager.MAX_PLAYERS}";
    }

    private void RefreshRoomList()
    {
        // 기존 항목 삭제
        foreach (Transform child in roomListContent)
            Destroy(child.gameObject);

        // 새 목록 생성
        foreach (RoomInfo info in cachedRoomList)
        {
            if (info.RemovedFromList) continue;

            GameObject item = Instantiate(roomItemPrefab, roomListContent);
            RoomListItem listItem = item.GetComponent<RoomListItem>();
            if (listItem != null)
                listItem.Setup(info, OnClickRoomItem);
        }
    }

    private void OnClickRoomItem(string roomName)
    {
        PhotonNetworkManager.Instance.JoinRoom(roomName);
        StartCoroutine(WaitAndShowWaiting());
    }

    private void ShowPanel(GameObject target)
    {
        panelLobby.SetActive(panelLobby == target);
        panelWaiting.SetActive(panelWaiting == target);
    }

    private void UpdateStatus(string message)
    {
        if (txtStatus != null)
            txtStatus.text = message;

        // 대기 패널에서 플레이어 수 실시간 갱신
        if (panelWaiting.activeSelf)
            UpdateWaitingPanel();
    }

    private void ShowError(string message)
    {
        if (txtStatus != null)
            txtStatus.text = $"<color=red>{message}</color>";
    }
}
