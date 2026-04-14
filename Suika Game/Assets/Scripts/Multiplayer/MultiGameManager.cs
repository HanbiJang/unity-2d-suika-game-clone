using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

/// <summary>
/// MultiModeScene 전용 멀티플레이어 동기화 매니저.
///
/// ★ 핵심 설계: 상대방 과일은 물리(Rigidbody2D) 완전 비활성.
/// 위치는 오직 스냅샷 데이터 → Lerp 보간으로만 결정.
/// 머지는 즉시 처리 (애니메이션 없음, 충돌 없음).
/// </summary>
public class MultiGameManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    // ─── Photon 이벤트 코드 ───────────────────────────────────────
    const byte EVT_NEXT_FRUIT   = 101;
    const byte EVT_FRUIT_DROP   = 102;
    const byte EVT_SCORE_UPDATE = 103;
    const byte EVT_GAME_OVER    = 104;
    const byte EVT_FRUIT_MERGE  = 105;
    const byte EVT_FRUIT_CREATE = 106;
    const byte EVT_FRUIT_STATE  = 107;

    /// <summary>스냅샷 전송 간격 (초). 수신측 Lerp 보간이 있으므로 0.1초면 충분히 부드러움.</summary>
    const float SNAPSHOT_INTERVAL = 0.1f;

    // ─── Inspector 연결 필드 ──────────────────────────────────────
    [Header("Local Player")]
    [SerializeField] private FruitManager localFruitManager;
    /// <summary>로컬 플레이어의 최상위 컨테이너 (GameObjects). 게임오버 시 회색 처리에 사용.</summary>
    [SerializeField] private Transform localGameObjects;

    [Header("Other Player Display")]
    [SerializeField] private Transform nextFruitParent_Other;
    [SerializeField] private Transform fruitParent_Other;
    [SerializeField] private Text otherScoreText;
    /// <summary>상대방 표시 최상위 컨테이너 (GameObjects_Other). 상대 게임오버 시 회색 처리에 사용.</summary>
    [SerializeField] private Transform otherGameObjects;

    [Header("Multi Result UI")]
    [SerializeField] private GameObject multiResultCanvas;
    [SerializeField] private Text resultText;

    // ─── 내부 상태 ────────────────────────────────────────────────
    private GameObject otherNextFruitModel;
    private bool gameEnded = false;
    private Dictionary<int, Fruit> otherFruits = new Dictionary<int, Fruit>();

    // 두 플레이어 모두 GameOverLine에 닿아야 게임 종료
    private bool localGamedOver  = false;
    private bool otherGamedOver  = false;
    private int  otherFinalScore = 0;

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

    private void SendFruitCreate(int level, float localX, float localY, float rotation, int dropId)
    {
        if (!PhotonNetwork.IsConnected) return;
        PhotonNetwork.RaiseEvent(EVT_FRUIT_CREATE,
            new object[] { (byte)level, localX, localY, rotation, dropId },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

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

    private void SendGameOver(int score)
    {
        if (!PhotonNetwork.IsConnected) return;
        PhotonNetwork.RaiseEvent(EVT_GAME_OVER, new object[] { score },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    // ─────────────────────────────────────────────────────────────
    // 주기적 위치 스냅샷 (0.1초마다)
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

        // 포맷: [ count, dropId0, x0, y0, rot0, dropId1, x1, y1, rot1, ... ]
        const int FIELDS = 4; // dropId, x, y, rot
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
        }

        // Unreliable: 유실돼도 다음 패킷이 바로 옴 (0.1초 간격). 지연 최소화.
        PhotonNetwork.RaiseEvent(EVT_FRUIT_STATE, data,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            SendOptions.SendUnreliable);
    }

    // ─────────────────────────────────────────────────────────────
    // 로컬 이벤트 핸들러
    // ─────────────────────────────────────────────────────────────

    private void OnLocalGameOver()
    {
        if (localGamedOver) return;
        localGamedOver = true;
        int myScore = localFruitManager.userScore;
        Debug.Log($"[MultiGameManager] OnLocalGameOver called, score={myScore}");

        // 상대방에게 내 최종 점수 전송
        SendGameOver(myScore);

        // 로컬 GameObjects 전체 스프라이트를 회색으로
        GrayOutLocalGameObjects();

        // 상대방이 이미 게임오버 상태라면 지금 바로 결과 표시
        if (otherGamedOver)
        {
            ShowResultByScore(myScore, otherFinalScore);
        }
        // else: 상대방이 아직 플레이 중 → 내 플레이만 정지(FruitManager가 이미 처리),
        //       상대방이 GameOverLine에 닿을 때까지 대기
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
                CreateOtherFruit(level, localX, localY, rotation, dropId);
                break;
            }

            case EVT_FRUIT_DROP:
            {
                object[] data = (object[])photonEvent.CustomData;
                int   dropId   = (int)data[0];
                float localX   = (float)data[1];
                float localY   = (float)data[2];
                float rotation = (float)data[3];
                OnOtherFruitDropped(dropId, localX, localY, rotation);
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
            {
                otherFinalScore = (int)((object[])photonEvent.CustomData)[0];
                otherGamedOver  = true;
                Debug.Log($"[MultiGameManager] Received EVT_GAME_OVER from other, score={otherFinalScore}");

                // 상대방 화면을 회색으로
                GrayOutOtherGameObjects();

                if (localGamedOver)
                {
                    // 나도 이미 게임오버 → 지금 바로 결과 표시
                    ShowResultByScore(localFruitManager.userScore, otherFinalScore);
                }
                // else: 상대방이 먼저 게임오버 → 내 플레이는 계속, 나중에 내가 게임오버되면 결과 표시
                break;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 결과 UI
    // ─────────────────────────────────────────────────────────────

    static readonly Color GrayColor = new Color(0x9A / 255f, 0x9A / 255f, 0x9A / 255f, 1f);

    /// <summary>지정 Transform 하위의 모든 SpriteRenderer를 회색(#9A9A9A)으로 변경. 알파=0인 개체는 스킵.</summary>
    private static void GrayOutSprites(Transform root)
    {
        if (root == null) return;
        foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.color.a == 0f) continue;
            sr.color = GrayColor;
        }
    }

    private void GrayOutLocalGameObjects() => GrayOutSprites(localGameObjects);
    private void GrayOutOtherGameObjects() => GrayOutSprites(otherGameObjects);

    /// <summary>내 점수와 상대 점수를 비교해 결과를 표시.</summary>
    private void ShowResultByScore(int myScore, int theirScore)
    {
        bool isWin = myScore > theirScore;
        // 동점은 패배 처리 (필요하면 변경 가능)
        Debug.Log($"[MultiGameManager] ShowResultByScore my={myScore} their={theirScore} win={isWin}");
        ShowResult(isWin);
    }

    private void ShowResult(bool isWin)
    {
        Debug.Log($"[MultiGameManager] ShowResult isWin={isWin}, gameEnded={gameEnded}");
        if (gameEnded) return;
        gameEnded = true;

        // 결과 화면이 뜨는 순간 로컬 플레이 완전 정지 (승리 시에도 조작 불가)
        localFruitManager?.FreezeGame();

        if (multiResultCanvas != null)
            multiResultCanvas.SetActive(true);
        else
            Debug.LogError("[MultiGameManager] multiResultCanvas가 할당되지 않았습니다!");

        if (resultText != null)
            resultText.text = isWin ? "승리!" : "패배...";
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
    // 상대방 과일: 생성 (물리 항상 OFF)
    // ─────────────────────────────────────────────────────────────

    private void CreateOtherFruit(int level, float localX, float localY, float rotation, int dropId)
    {
        if (fruitParent_Other == null) return;

        GameObject go = Instantiate(localFruitManager.FruitPrefab, fruitParent_Other, false);
        go.transform.localPosition = new Vector3(localX, localY, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);

        float size = 0.2f * (level - 1) + 1f;
        go.transform.localScale = new Vector3(size, size, size);
        go.GetComponent<SpriteRenderer>().sprite = localFruitManager.fruitSprite[level - 1];

        // ★ 물리 완전 비활성. Collider도 꺼서 어떤 충돌도 발생하지 않음.
        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        rb.simulated = false;

        Collider2D col = go.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Fruit fruit = go.GetComponent<Fruit>();
        fruit.FruitGameObject    = go;
        fruit.level              = level;
        fruit.dropId             = dropId;
        fruit.isOtherPlayerFruit = true;

        otherFruits[dropId] = fruit;
    }

    // ─────────────────────────────────────────────────────────────
    // 상대방 과일: 드롭 (위치만 갱신, 물리 여전히 OFF)
    // ─────────────────────────────────────────────────────────────

    private void OnOtherFruitDropped(int dropId, float localX, float localY, float rotation)
    {
        if (!otherFruits.TryGetValue(dropId, out Fruit fruit)) return;
        if (fruit == null || fruit.FruitGameObject == null) return;

        // 드롭 위치를 보간 타겟으로 설정 → Fruit.Update에서 부드럽게 이동
        fruit.networkTargetLocalPos = new Vector3(localX, localY, 0f);
        fruit.networkTargetRot      = rotation;
        fruit.hasNetworkTarget      = true;
    }

    // ─────────────────────────────────────────────────────────────
    // 상대방 과일: 머지 (즉시 처리, 애니메이션/물리 없음)
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

        // 딕셔너리에서 제거
        otherFruits.Remove(otherDropId);

        // 즉시 머지: 애니메이션 없음, 물리 없음, 위치는 스냅샷이 처리
        survivor.ApplyNetworkMerge(other, newLevel);
    }

    // ─────────────────────────────────────────────────────────────
    // 스냅샷 수신 → 보간 타겟 갱신 (물리 없음, 위치만)
    // ─────────────────────────────────────────────────────────────

    private void ApplyFruitSnapshot(object[] data)
    {
        int count = (int)data[0];
        const int FIELDS = 4;

        for (int i = 0; i < count; i++)
        {
            int b = 1 + i * FIELDS;

            int   dropId = (int)data[b + 0];
            float x      = (float)data[b + 1];
            float y      = (float)data[b + 2];
            float rot    = (float)data[b + 3];

            if (!otherFruits.TryGetValue(dropId, out Fruit fruit)) continue;
            if (fruit == null || fruit.FruitGameObject == null) continue;

            // Lerp 보간 타겟 설정 → Fruit.Update()에서 부드럽게 이동
            fruit.networkTargetLocalPos = new Vector3(x, y, 0f);
            fruit.networkTargetRot      = rot;
            fruit.hasNetworkTarget      = true;
        }
    }
}
