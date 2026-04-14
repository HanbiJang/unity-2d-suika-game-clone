using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

/// <summary>
/// 로비 UI 컨트롤러
/// </summary>
public class LobbyUI : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private GameObject panelLobby;
    [SerializeField] private GameObject panelWaiting;

    [Header("로비 패널")]
    [SerializeField] private TMP_InputField inputRoomName;
    [SerializeField] private Button btnCreate;
    [SerializeField] private Button btnJoinByName;
    [SerializeField] private Button btnQuickJoin;
    [SerializeField] private Button btnRefresh;
    [SerializeField] private Button btnBack;
    [SerializeField] private Transform roomListContent;
    [SerializeField] private GameObject roomItemPrefab;

    [Header("대기 패널")]
    [SerializeField] private TMP_Text txtRoomName;
    [SerializeField] private TMP_Text txtPlayerCount;
    [SerializeField] private Button btnLeave;

    [Header("공통")]
    [SerializeField] private TMP_Text txtStatus;

    [Header("씬 전환")]
    [SerializeField] private LoadingSceneController loadingSceneController;

    private void Start()
    {
        if (PhotonNetworkManager.Instance == null)
            new GameObject("PhotonNetworkManager").AddComponent<PhotonNetworkManager>();

        btnCreate.onClick.AddListener(OnClickCreate);
        if (btnJoinByName != null) btnJoinByName.onClick.AddListener(OnClickJoinByName);
        btnQuickJoin.onClick.AddListener(OnClickQuickJoin);
        if (btnRefresh != null) btnRefresh.onClick.AddListener(OnClickRefresh);
        btnBack.onClick.AddListener(OnClickBack);
        btnLeave.onClick.AddListener(OnClickLeave);

        PhotonNetworkManager.Instance.OnStatusChanged += UpdateStatus;
        PhotonNetworkManager.Instance.OnRoomListUpdated += OnRoomListUpdated;
        PhotonNetworkManager.Instance.OnError += ShowError;
        PhotonNetworkManager.Instance.OnMatchFound += OnMatchFound;

        ShowPanel(panelLobby);

        if (PhotonNetwork.IsConnected && PhotonNetwork.InLobby)
        {
            RefreshRoomList(PhotonNetworkManager.Instance.CachedRoomList);
        }
        else
        {
            PhotonNetworkManager.Instance.Connect();
            StartCoroutine(WaitAndShowLobby());
        }
    }

    private void OnDestroy()
    {
        if (PhotonNetworkManager.Instance == null) return;
        PhotonNetworkManager.Instance.OnStatusChanged -= UpdateStatus;
        PhotonNetworkManager.Instance.OnRoomListUpdated -= OnRoomListUpdated;
        PhotonNetworkManager.Instance.OnError -= ShowError;
        PhotonNetworkManager.Instance.OnMatchFound -= OnMatchFound;
    }

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

    private void OnMatchFound()
    {
        Debug.Log("[LobbyUI] OnMatchFound -> 커튼 자동 전환 시작");

        if (loadingSceneController == null)
        {
            Debug.LogError("[LobbyUI] loadingSceneController가 할당되지 않았습니다. Inspector에서 연결하세요.");
            PhotonNetworkManager.Instance.TryLoadMultiModeScene();
            return;
        }

        loadingSceneController.StartAutoTransitionToMulti(() =>
        {
            PhotonNetworkManager.Instance.TryLoadMultiModeScene();
        });
    }

    private void OnClickRefresh()
    {
        PhotonNetworkManager.Instance.RefreshRoomList();
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

    private void UpdateWaitingPanel()
    {
        if (!PhotonNetwork.InRoom) return;
        txtRoomName.text = $"{PhotonNetwork.CurrentRoom.Name}";
        if (txtPlayerCount != null)
            txtPlayerCount.text = $"{PhotonNetwork.CurrentRoom.PlayerCount} / {PhotonNetworkManager.MAX_PLAYERS}";
    }

    private void OnRoomListUpdated(List<RoomInfo> roomList)
    {
        Debug.Log($"[LobbyUI] OnRoomListUpdated 수신 - 방 수: {roomList.Count}");
        RefreshRoomList(roomList);
    }

    private void RefreshRoomList(List<RoomInfo> roomList)
    {
        Debug.Log($"[LobbyUI] RefreshRoomList 호출 - 방 수: {roomList.Count}");

        if (roomListContent == null)
        {
            Debug.LogError("[LobbyUI] roomListContent가 비어 있습니다. Inspector에서 연결하세요.");
            return;
        }

        if (roomItemPrefab == null)
        {
            Debug.LogError("[LobbyUI] roomItemPrefab가 비어 있습니다. Inspector에서 연결하세요.");
            return;
        }

        foreach (Transform child in roomListContent)
            Destroy(child.gameObject);

        foreach (RoomInfo info in roomList)
        {
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

        if (panelWaiting.activeSelf)
            UpdateWaitingPanel();
    }

    private void ShowError(string message)
    {
        if (txtStatus != null)
            txtStatus.text = $"<color=red>{message}</color>";
    }
}
