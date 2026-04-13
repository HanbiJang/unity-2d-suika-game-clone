using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

/// <summary>
/// MultiModeScene 전용 멀티플레이어 동기화 매니저
/// GameManagers 오브젝트에 부착
/// </summary>
public class MultiGameManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    // ─── Photon 이벤트 코드 ───────────────────────────────────────
    const byte EVT_NEXT_FRUIT   = 101;
    const byte EVT_FRUIT_DROP   = 102;
    const byte EVT_SCORE_UPDATE = 103;
    const byte EVT_GAME_OVER    = 104;
    const byte EVT_FRUIT_MERGE  = 105;
    const byte EVT_FRUIT_CREATE = 106;  // 과일 생성(들기)
    const byte EVT_FRUIT_STATE  = 107;  // 전체 위치 스냅샷 (물리 보정용)

    /// <summary>스냅샷 전송 간격 (초). 낮을수록 정확하나 트래픽 증가.</summary>
    const float SNAPSHOT_INTERVAL = 0.3f;

    // ─── Inspector 연결 필드 ──────────────────────────────────────
    [Header("Local Player")]
    [SerializeField] private FruitManager localFruitManager;

    [Header("Other Player Display")]
    [Tooltip("NextOneImg_Other > NextFruitParent")]
    [SerializeField] private Transform nextFruitParent_Other;

    [Tooltip("GameObjects_Other > FruitParent")]
    [SerializeField] private Transform fruitParent_Other;

    [Tooltip("ScoreImg_Other 하위의 ScoreText (Text 컴포넌트)")]
    [SerializeField] private Text otherScoreText;

    [Header("Multi Result UI")]
    [Tooltip("승/패 결과를 보여줄 Canvas (기본 비활성화)")]
    [SerializeField] private GameObject multiResultCanvas;

    [Tooltip("'승리!' / '패배...' 를 표시할 Text")]
    [SerializeField] private Text resultText;

    // ─── 내부 상태 ────────────────────────────────────────────────
    private GameObject otherNextFruitModel;
    private bool gameEnded = false;

    /// <summary>dropId → 상대방 박스에 생성된 Fruit 컴포넌트 매핑</summary>
    private Dictionary<int, Fruit> otherFruits = new Dictionary<int, Fruit>();

    // ─────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (localFruitManager == null)
        {
            Debug.LogError("[MultiGameManager] localFruitManager가 할당되지 않았습니다.");
            return;
        }

        localFruitManager.suppressGameOverUI = true;

        localFruitManager.OnNextFruitLevelChanged += SendNextFruitLevel;
        localFruitManager.OnFruitCreated          += SendFruitCreate;
        localFruitManager.OnFruitDropped          += SendFruitDrop;
        localFruitManager.OnFruitMerged           += SendFruitMerge;
        localFruitManager.OnScoreUpdated          += SendScoreUpdate;
        localFruitManager.OnGameOverTriggered     += OnLocalGameOver;

        StartCoroutine(SnapshotLoop());
    }

    private void OnDestroy()
    {
        if (localFruitManager == null) return;

        localFruitManager.suppressGameOverUI = false;
        localFruitManager.OnNextFruitLevelChanged -= SendNextFruitLevel;
        localFruitManager.OnFruitCreated          -= SendFruitCreate;
        localFruitManager.OnFruitDropped          -= SendFruitDrop;
        localFruitManager.OnFruitMerged           -= SendFruitMerge;
        localFruitManager.OnScoreUpdated          -= SendScoreUpdate;
        localFruitManager.OnGameOverTriggered     -= OnLocalGameOver;
    }

    // ─────────────────────────────────────────────────────────────
    // 송신
    // ─────────────────────────────────────────────────────────────

    private void SendNextFruitLevel(int level)
    {
        if (!PhotonNetwork.IsConnected) return;
        PhotonNetwork.RaiseEvent(EVT_NEXT_FRUIT, new object[] { (byte)level },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    /// <summary>과일 생성(들기): 수신측은 physics OFF 상태로 과일 표시</summary>
    private void SendFruitCreate(int level, float localX, float localY, float rotation, int dropId)
    {
        if (!PhotonNetwork.IsConnected) return;
        PhotonNetwork.RaiseEvent(EVT_FRUIT_CREATE,
            new object[] { (byte)level, localX, localY, rotation, dropId },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    /// <summary>과일 드롭(놓기): 수신측은 기존 과일 위치 갱신 후 physics ON</summary>
    private void SendFruitDrop(int dropId, float localX, float localY, float rotation)
    {
        if (!PhotonNetwork.IsConnected) return;
        PhotonNetwork.RaiseEvent(EVT_FRUIT_DROP,
            new object[] { dropId, localX, localY, rotation },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    private void SendFruitMerge(int survivorDropId, int otherDropId, int newLevel)
    {
        if (!PhotonNetwork.IsConnected) return;
        PhotonNetwork.RaiseEvent(EVT_FRUIT_MERGE,
            new object[] { survivorDropId, otherDropId, (byte)newLevel },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    private void SendScoreUpdate(int score)
    {
        if (!PhotonNetwork.IsConnected) return;
        PhotonNetwork.RaiseEvent(EVT_SCORE_UPDATE, new object[] { score },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    private void SendGameOver()
    {
        if (!PhotonNetwork.IsConnected) return;
        PhotonNetwork.RaiseEvent(EVT_GAME_OVER, null,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    // ─────────────────────────────────────────────────────────────
    // 주기적 위치 스냅샷
    // ─────────────────────────────────────────────────────────────

    private IEnumerator SnapshotLoop()
    {
        while (!gameEnded)
        {
            yield return new WaitForSeconds(SNAPSHOT_INTERVAL);
            SendFruitStateSnapshot();
        }
    }

    private void SendFruitStateSnapshot()
    {
        if (!PhotonNetwork.IsConnected) return;
        var states = localFruitManager.GetFruitStates();
        if (states.Length == 0) return;

        // 포맷: [ count, dropId0, x0, y0, rot0, vx0, vy0, angVel0, simulated0, dropId1, ... ]
        const int FIELDS = 8; // dropId + x + y + rot + vx + vy + angVel + simulated
        object[] data = new object[1 + states.Length * FIELDS];
        data[0] = states.Length;

        for (int i = 0; i < states.Length; i++)
        {
            int b = 1 + i * FIELDS;
            var s = states[i];
            data[b + 0] = s.dropId;
            data[b + 1] = s.x;
            data[b + 2] = s.y;
            data[b + 3] = s.rot;
            data[b + 4] = s.vx;
            data[b + 5] = s.vy;
            data[b + 6] = s.angVel;
            data[b + 7] = s.simulated;
        }

        // EVT_FRUIT_MERGE(Reliable)보다 먼저 도착하면 안 되므로 Reliable로 전송
        // → 같은 채널에서 순서 보장: 머지 이벤트 먼저, 스냅샷 나중에 도착
        PhotonNetwork.RaiseEvent(EVT_FRUIT_STATE, data,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            SendOptions.SendReliable);
    }

    // ─────────────────────────────────────────────────────────────
    // 로컬 이벤트 핸들러
    // ─────────────────────────────────────────────────────────────

    private void OnLocalGameOver()
    {
        Debug.Log("[MultiGameManager] OnLocalGameOver called → ShowResult(false)");
        SendGameOver();
        ShowResult(false);
    }

    // ─────────────────────────────────────────────────────────────
    // Photon 룸 콜백
    // ─────────────────────────────────────────────────────────────

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        ShowResult(true);
    }

    public void OnReturnToLobbyClicked()
    {
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("LobbyScene");
    }

    // ─────────────────────────────────────────────────────────────
    // 수신 (IOnEventCallback)
    // ─────────────────────────────────────────────────────────────

    public void OnEvent(EventData photonEvent)
    {
        switch (photonEvent.Code)
        {
            case EVT_NEXT_FRUIT:
            {
                int level = (int)(byte)((object[])photonEvent.CustomData)[0];
                UpdateOtherNextFruitDisplay(level);
                break;
            }

            case EVT_FRUIT_CREATE:
            {
                object[] data = (object[])photonEvent.CustomData;
                int   level    = (int)(byte)data[0];
                float localX   = (float)data[1];
                float localY   = (float)data[2];
                float rotation = (float)data[3];
                int   dropId   = (int)data[4];
                CreateOtherFruitHeld(level, localX, localY, rotation, dropId);
                break;
            }

            case EVT_FRUIT_DROP:
            {
                object[] data = (object[])photonEvent.CustomData;
                int   dropId   = (int)data[0];
                float localX   = (float)data[1];
                float localY   = (float)data[2];
                float rotation = (float)data[3];
                DropOtherFruit(dropId, localX, localY, rotation);
                break;
            }

            case EVT_FRUIT_MERGE:
            {
                object[] data      = (object[])photonEvent.CustomData;
                int survivorDropId = (int)data[0];
                int otherDropId    = (int)data[1];
                int newLevel       = (int)(byte)data[2];
                ApplyOtherMerge(survivorDropId, otherDropId, newLevel);
                break;
            }

            case EVT_SCORE_UPDATE:
            {
                int score = (int)((object[])photonEvent.CustomData)[0];
                if (otherScoreText != null)
                    otherScoreText.text = string.Format("{0:00000}", score);
                break;
            }

            case EVT_FRUIT_STATE:
            {
                ApplyFruitSnapshot((object[])photonEvent.CustomData);
                break;
            }

            case EVT_GAME_OVER:
                ShowResult(true);
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 결과 UI
    // ─────────────────────────────────────────────────────────────

    private void ShowResult(bool isWin)
    {
        Debug.Log($"[MultiGameManager] ShowResult called. isWin={isWin}, gameEnded={gameEnded}, multiResultCanvas={multiResultCanvas}");

        if (gameEnded) return;
        gameEnded = true;

        if (multiResultCanvas != null)
        {
            multiResultCanvas.SetActive(true);
            Debug.Log("[MultiGameManager] multiResultCanvas activated.");
        }
        else
        {
            Debug.LogError("[MultiGameManager] multiResultCanvas가 Inspector에 할당되지 않았습니다!");
        }

        if (resultText != null)
            resultText.text = isWin ? "승리!" : "패배...";
        else
            Debug.LogWarning("[MultiGameManager] resultText가 Inspector에 할당되지 않았습니다.");
    }

    // ─────────────────────────────────────────────────────────────
    // 상대방 다음 과일 미리보기
    // ─────────────────────────────────────────────────────────────

    private void UpdateOtherNextFruitDisplay(int level)
    {
        if (nextFruitParent_Other == null) return;

        if (otherNextFruitModel == null)
        {
            otherNextFruitModel = Instantiate(localFruitManager.FruitPrefab, nextFruitParent_Other, false);
            otherNextFruitModel.GetComponent<Rigidbody2D>().simulated = false;
        }

        float size = 0.2f * (level - 1) + 1f;
        otherNextFruitModel.transform.localScale = new Vector3(size, size, size);
        otherNextFruitModel.GetComponent<SpriteRenderer>().sprite = localFruitManager.fruitSprite[level - 1];
    }

    // ─────────────────────────────────────────────────────────────
    // 상대방 과일: 생성(들기) - physics OFF
    // ─────────────────────────────────────────────────────────────

    private void CreateOtherFruitHeld(int level, float localX, float localY, float rotation, int dropId)
    {
        if (fruitParent_Other == null) return;

        GameObject go = Instantiate(localFruitManager.FruitPrefab, fruitParent_Other, false);
        go.transform.localPosition = new Vector3(localX, localY, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);

        float size = 0.2f * (level - 1) + 1f;
        go.transform.localScale = new Vector3(size, size, size);
        go.GetComponent<SpriteRenderer>().sprite = localFruitManager.fruitSprite[level - 1];

        Fruit fruit = go.GetComponent<Fruit>();
        fruit.FruitGameObject    = go;
        fruit.level              = level;
        fruit.dropId             = dropId;
        fruit.isOtherPlayerFruit = true;

        go.GetComponent<Rigidbody2D>().simulated = false; // 들고 있는 상태 → physics OFF

        otherFruits[dropId] = fruit;
    }

    // ─────────────────────────────────────────────────────────────
    // 상대방 과일: 드롭(놓기) - 위치 갱신 후 physics ON
    // ─────────────────────────────────────────────────────────────

    private void DropOtherFruit(int dropId, float localX, float localY, float rotation)
    {
        if (!otherFruits.TryGetValue(dropId, out Fruit fruit))
        {
            Debug.LogWarning($"[MultiGameManager] DropOtherFruit: dropId={dropId} not found");
            return;
        }

        if (fruit == null || fruit.FruitGameObject == null) return;

        fruit.FruitGameObject.transform.localPosition = new Vector3(localX, localY, 0f);
        fruit.FruitGameObject.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        fruit.FruitGameObject.GetComponent<Rigidbody2D>().simulated = true;
    }

    // ─────────────────────────────────────────────────────────────
    // 상대방 과일: 머지 적용
    // ─────────────────────────────────────────────────────────────

    private void ApplyOtherMerge(int survivorDropId, int otherDropId, int newLevel)
    {
        if (!otherFruits.TryGetValue(survivorDropId, out Fruit survivor))
        {
            Debug.LogWarning($"[MultiGameManager] ApplyOtherMerge: survivor dropId={survivorDropId} not found");
            return;
        }
        if (!otherFruits.TryGetValue(otherDropId, out Fruit other))
        {
            Debug.LogWarning($"[MultiGameManager] ApplyOtherMerge: other dropId={otherDropId} not found");
            return;
        }

        if (survivor.IsMerging)
        {
            // survivor가 다른 머지 애니메이션 중 → 완료될 때까지 대기 후 재시도
            StartCoroutine(RetryMerge(survivorDropId, otherDropId, newLevel));
            return;
        }

        otherFruits.Remove(otherDropId);
        survivor.ApplyNetworkMerge(other, newLevel);
    }

    /// <summary>
    /// survivor가 다른 머지 애니메이션 중일 때 재시도.
    /// 연속 머지(ex. 포도A+B, 포도C+D, 오렌지A+C)가 빠르게 일어날 때 이벤트가 무시되는 버그 방지.
    /// </summary>
    private IEnumerator RetryMerge(int survivorDropId, int otherDropId, int newLevel, int attempt = 0)
    {
        if (attempt >= 20)
        {
            Debug.LogWarning($"[MultiGameManager] RetryMerge: 재시도 초과, dropId={survivorDropId}");
            yield break;
        }

        yield return new WaitForSeconds(0.05f); // 50ms 후 재시도

        if (!otherFruits.TryGetValue(survivorDropId, out Fruit survivor)) yield break;
        if (!otherFruits.TryGetValue(otherDropId, out Fruit other))       yield break;

        if (survivor.IsMerging)
        {
            // 아직 바쁨 → 다시 대기
            StartCoroutine(RetryMerge(survivorDropId, otherDropId, newLevel, attempt + 1));
            yield break;
        }

        otherFruits.Remove(otherDropId);
        survivor.ApplyNetworkMerge(other, newLevel);
    }

    // ─────────────────────────────────────────────────────────────
    // 스냅샷 수신 → 상대방 과일 위치/속도 강제 보정
    // ─────────────────────────────────────────────────────────────

    private void ApplyFruitSnapshot(object[] data)
    {
        int count = (int)data[0];
        const int FIELDS = 8;

        for (int i = 0; i < count; i++)
        {
            int b = 1 + i * FIELDS;

            int   dropId    = (int)data[b + 0];
            float x         = (float)data[b + 1];
            float y         = (float)data[b + 2];
            float rot       = (float)data[b + 3];
            float vx        = (float)data[b + 4];
            float vy        = (float)data[b + 5];
            float angVel    = (float)data[b + 6];
            bool  simulated = (bool)data[b + 7];

            if (!otherFruits.TryGetValue(dropId, out Fruit fruit)) continue;
            if (fruit == null || fruit.FruitGameObject == null) continue;

            // 머지 애니메이션 중인 과일은 스냅샷으로 덮어쓰지 않음
            // → 애니메이션이 끝난 뒤 다음 스냅샷에서 자연스럽게 보정됨
            if (fruit.IsMerging) continue;

            var go = fruit.FruitGameObject;
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb == null) continue;

            go.transform.localPosition = new Vector3(x, y, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rot);

            if (simulated)
            {
                rb.simulated      = true;
                rb.velocity        = new Vector2(vx, vy);
                rb.angularVelocity = angVel;
            }
            // simulated=false(들고 있는 과일)는 위치만 갱신, 속도 불필요
        }
    }
}
