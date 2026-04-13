using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

/// <summary>
/// MultiModeScene 전용 멀티플레이어 동기화 매니저
/// GameManagers 오브젝트에 부착
///
/// 동기화 항목:
///   1. 다음 과일 (NextOneImg_Other) ✓
///   2. 과일 드롭 (FruitParent_Other) ✓
///   3. 점수 (ScoreText_Other)         - 추후
///   4. 게임오버                        - 추후
/// </summary>
public class MultiGameManager : MonoBehaviour, IOnEventCallback
{
    // ─── Photon 이벤트 코드 ───────────────────────────────────────
    const byte EVT_NEXT_FRUIT   = 101;  // 다음 과일 레벨 동기화
    const byte EVT_FRUIT_DROP   = 102;  // 과일 드롭 동기화
    const byte EVT_SCORE_UPDATE = 103;  // 점수 동기화
    const byte EVT_GAME_OVER    = 104;  // 게임오버 동기화

    // ─── Inspector 연결 필드 ──────────────────────────────────────
    [Header("Local Player")]
    [SerializeField] private FruitManager localFruitManager;

    [Header("Other Player Display")]
    [Tooltip("NextOneImg_Other의 자식 NextFruitParent Transform")]
    [SerializeField] private Transform nextFruitParent_Other;

    [Tooltip("GameObjects_Other 하위의 FruitParent Transform")]
    [SerializeField] private Transform fruitParent_Other;

    // ─── 내부 상태 ────────────────────────────────────────────────
    private GameObject otherNextFruitModel;

    // ─────────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    private void Start()
    {
        if (localFruitManager == null)
        {
            Debug.LogError("[MultiGameManager] localFruitManager가 할당되지 않았습니다.");
            return;
        }

        // FruitManager 이벤트 구독
        localFruitManager.OnNextFruitLevelChanged += SendNextFruitLevel;
        localFruitManager.OnFruitDropped          += SendFruitDrop;
        localFruitManager.OnScoreUpdated          += SendScoreUpdate;
        localFruitManager.OnGameOverTriggered     += SendGameOver;
    }

    private void OnDestroy()
    {
        if (localFruitManager == null) return;

        localFruitManager.OnNextFruitLevelChanged -= SendNextFruitLevel;
        localFruitManager.OnFruitDropped          -= SendFruitDrop;
        localFruitManager.OnScoreUpdated          -= SendScoreUpdate;
        localFruitManager.OnGameOverTriggered     -= SendGameOver;
    }

    // ─────────────────────────────────────────────────────────────
    // 송신: 로컬 이벤트 → 상대방에게 RaiseEvent
    // ─────────────────────────────────────────────────────────────

    private void SendNextFruitLevel(int level)
    {
        if (!PhotonNetwork.IsConnected) return;

        object[] data = { (byte)level };
        PhotonNetwork.RaiseEvent(
            EVT_NEXT_FRUIT,
            data,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            SendOptions.SendReliable);
    }

    private void SendFruitDrop(int level, float localX, float localY, float rotation)
    {
        if (!PhotonNetwork.IsConnected) return;

        // byte 변환: int level → byte (과일 레벨은 1~11 이내)
        object[] data = { (byte)level, localX, localY, rotation };
        PhotonNetwork.RaiseEvent(
            EVT_FRUIT_DROP,
            data,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            SendOptions.SendReliable);
    }

    private void SendScoreUpdate(int score)
    {
        if (!PhotonNetwork.IsConnected) return;

        object[] data = { score };
        PhotonNetwork.RaiseEvent(
            EVT_SCORE_UPDATE,
            data,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            SendOptions.SendReliable);
    }

    private void SendGameOver()
    {
        if (!PhotonNetwork.IsConnected) return;

        PhotonNetwork.RaiseEvent(
            EVT_GAME_OVER,
            null,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            SendOptions.SendReliable);
    }

    // ─────────────────────────────────────────────────────────────
    // 수신: 상대방 이벤트 처리
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

            case EVT_FRUIT_DROP:
            {
                object[] data = (object[])photonEvent.CustomData;
                int level    = (int)(byte)data[0];
                float localX = (float)data[1];
                float localY = (float)data[2];
                float rotation = (float)data[3];
                CreateOtherFruit(level, localX, localY, rotation);
                break;
            }

            case EVT_SCORE_UPDATE:
            case EVT_GAME_OVER:
                // 추후 단계에서 구현
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // NextOneImg_Other 업데이트
    // ─────────────────────────────────────────────────────────────

    private void UpdateOtherNextFruitDisplay(int level)
    {
        if (nextFruitParent_Other == null)
        {
            Debug.LogWarning("[MultiGameManager] nextFruitParent_Other가 할당되지 않았습니다.");
            return;
        }

        // 처음이면 프리팹 생성
        if (otherNextFruitModel == null)
        {
            otherNextFruitModel = Instantiate(
                localFruitManager.FruitPrefab,
                nextFruitParent_Other,
                false);

            // 물리 비활성화 (표시 전용)
            otherNextFruitModel.GetComponent<Rigidbody2D>().simulated = false;
        }

        // 레벨에 맞게 크기 및 스프라이트 갱신 (FruitManager.ShowNextFruitModel 동일 로직)
        float size = 0.2f * (level - 1) + 1f;
        otherNextFruitModel.transform.localScale = new Vector3(size, size, size);
        otherNextFruitModel.GetComponent<SpriteRenderer>().sprite =
            localFruitManager.fruitSprite[level - 1];
    }

    // ─────────────────────────────────────────────────────────────
    // 상대방 과일 드롭 재현
    // ─────────────────────────────────────────────────────────────

    private void CreateOtherFruit(int level, float localX, float localY, float rotation)
    {
        if (fruitParent_Other == null)
        {
            Debug.LogWarning("[MultiGameManager] fruitParent_Other가 할당되지 않았습니다.");
            return;
        }

        // FruitPrefab을 FruitParent_Other 하위에 생성
        GameObject go = Instantiate(
            localFruitManager.FruitPrefab,
            fruitParent_Other,
            false);

        // 로컬 좌표 그대로 적용 → 상대방 박스 내 동일 위치에 재현
        go.transform.localPosition = new Vector3(localX, localY, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);

        // 레벨에 맞게 크기/스프라이트 설정
        float size = 0.2f * (level - 1) + 1f;
        go.transform.localScale = new Vector3(size, size, size);
        go.GetComponent<SpriteRenderer>().sprite = localFruitManager.fruitSprite[level - 1];

        // Fruit 컴포넌트 설정
        Fruit fruit = go.GetComponent<Fruit>();
        fruit.FruitGameObject = go;        // 머지 시 상대 과일 참조에 필요
        fruit.level = level;
        fruit.isOtherPlayerFruit = true;   // 점수/fruitMaxLevel 영향 차단

        // 물리 활성화 → 자연스럽게 낙하 및 충돌
        go.GetComponent<Rigidbody2D>().simulated = true;
    }
}
