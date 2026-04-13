using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Realtime;

/// <summary>
/// 로비의 룸 목록 한 줄 아이템
/// 프리팹 구성: TMP_Text(TxtRoomName) + Button
/// </summary>
public class RoomListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text txtRoomName;
    [SerializeField] private Button btnJoin;

    private System.Action<string> onJoin;
    private string roomName;

    private void Awake()
    {
        if (txtRoomName == null) txtRoomName = GetComponentInChildren<TMP_Text>();
        if (btnJoin == null) btnJoin = GetComponentInChildren<Button>();
    }

    public void Setup(RoomInfo info, System.Action<string> callback)
    {
        roomName = info.Name;
        onJoin = callback;
        txtRoomName.text = $"{info.Name}  ({info.PlayerCount}/{info.MaxPlayers})";
        btnJoin.interactable = info.PlayerCount < info.MaxPlayers;
        btnJoin.onClick.AddListener(() => onJoin?.Invoke(roomName));
    }
}
