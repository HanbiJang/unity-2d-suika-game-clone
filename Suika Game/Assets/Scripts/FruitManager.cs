
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public enum EffectSound
{
    Drop, Merge,
}
public class FruitManager : MonoBehaviour
{
    [SerializeField] private GameObject gameOverCanvas;

    public GameObject FruitPrefab; //������ ������
    GameObject FruitParent; //���� �θ�
    public GameObject newFruitGameObject { get; set; } //�� ����
    GroundControl groundControl; //�� ��ũ��Ʈ ( �Ҹ� ��� ) 

    Vector2 ClickPoint; //Ŭ���� ��ǥ
    List<Fruit> fruits; //������ ����Ʈ
    bool isClicked; //Ŭ���Ǿ�����. true�� Ŭ�� ������ ���´�
    public bool isReady; //true�� �ð� 1�� �������� Ȯ��
    float speed = 10f;
    Vector3 target; //��ǥ ����

    public const int minLevel = 1; //�ּ� ���� (����)
    public int fruitMaxLevel = 1; //������ ���� �ִ� ����
    public const int maxLevel = 11; //���� �ִ� ���� 

    //���� ��������Ʈ
    public List<Sprite> fruitSprite; //�ν����Ϳ��� �Ҵ�

    //��ƼŬ
    GameObject EffectParent;
    public GameObject MergeEffectGameObject; //���ӿ�����Ʈ, �ν����� �Ҵ�
    public GameObject ScoreEffectGameObject; //���ӿ�����Ʈ, �ν����� �Ҵ�

    //����
    public AudioClip[] audioClips; //�ν����� �Ҵ�
    public AudioSource[] SoundChannels; //�ν����� �Ҵ�
    int channelNum = 0;

    //����
    public int userScore = 0;
    Text ScoreText;
    Transform ScoreTextsPosition;

    //��
    GameObject Wall_left;
    GameObject Wall_right;
    GameObject Ground;
    GameObject GameOverLine; //���� ���� ���
    GameObject NextFruitParent;

    public GameFlowManager GameFlowManager;

    /// <summary>
    /// 멀티 씬처럼 이름 중복이 있는 경우 여기에 직접 연결.
    /// 비워두면 GameObject.Find() 자동 탐색 (싱글 씬 호환).
    /// </summary>
    [Header("Direct References (멀티 씬에서 직접 연결)")]
    [SerializeField] private GameObject ref_FruitParent;
    [SerializeField] private GameObject ref_Ground;
    [SerializeField] private Text       ref_ScoreText;
    [SerializeField] private GameObject ref_EffectParent;
    [SerializeField] private GameObject ref_Wall_left;
    [SerializeField] private GameObject ref_Wall_right;
    [SerializeField] private GameObject ref_GameOverLine;
    [SerializeField] private GameObject ref_NextFruitParent;
    [SerializeField] private CursorControl ref_Cursor;

    // Cursor 캐싱 (Update에서 매 프레임 Find 방지)
    private CursorControl cachedCursorControl;

    float SizeUp = 0.2f;

    bool isGameRun = false;
    bool isGameOver = false;
    int nextFruitLevel = 1;
    GameObject nextFruitModel;

    // 멀티플레이어 동기화 이벤트
    public System.Action<int> OnNextFruitLevelChanged;              // 다음 과일 레벨 변경 시
    public System.Action<int, float, float, float> OnFruitDropped;  // 드롭 시 (level, localX, localY, rotation)
    public System.Action<int> OnScoreUpdated;                       // 점수 변경 시
    public System.Action OnGameOverTriggered;                        // 게임오버 시

    Vector3 cursorPos; //���콺 Ŀ�� ��ġ, ������ ���ϸ��� ����ٴ�

    public float rightBorder;
    public float leftBorder;
    float gap = 0.25f;
    bool isSimulated = false; //�� ������ ������ �޴����� ���� bool
    float targetSize;

    public GameObject rankingPageGameObject; // RankingPage GameObject to activate

    // 1) 랭킹 등록 완료 후 호출 (완료 버튼에 연결)
    public void OnRankingUploadComplete()
    {
        StartCoroutine(WaitAndOpenGameOver(1.0f));
    }

    // 2) 등록 조건 미달로 바로 게임 오버 시 호출
    public void OnSimpleGameOver()
    {
        StartCoroutine(WaitAndOpenGameOver(1.0f));
    }

    private IEnumerator WaitAndOpenGameOver(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (gameOverCanvas != null)
        {
            gameOverCanvas.SetActive(true);
        }
    }
    public void makeGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;
        isGameRun = false;
        OnGameOverTriggered?.Invoke();
        GameOver();
    }
    public bool isGameRun_func()
    {
        return isGameRun;
    }

    void Awake()
    {
        target = Vector3.zero;
        isReady = true;
        fruits = new List<Fruit>();
        ClickPoint = Vector2.zero;

        try
        {
            FruitParent     = ref_FruitParent     ?? GameObject.Find("FruitParent");
            groundControl   = (ref_Ground         ?? GameObject.Find("Ground")).GetComponent<GroundControl>();
            ScoreText       = ref_ScoreText        ?? GameObject.Find("ScoreText").GetComponent<Text>();
            ScoreTextsPosition = ScoreText.transform;
            EffectParent    = ref_EffectParent    ?? GameObject.Find("EffectParent");
            Wall_left       = ref_Wall_left       ?? GameObject.Find("Wall_left");
            Wall_right      = ref_Wall_right      ?? GameObject.Find("Wall_right");
            Ground          = ref_Ground          ?? GameObject.Find("Ground");
            GameOverLine    = ref_GameOverLine    ?? GameObject.Find("GameOverLine");
            NextFruitParent = ref_NextFruitParent ?? GameObject.Find("NextFruitParent");
            cachedCursorControl = ref_Cursor      ?? GameObject.Find("Cursor")?.GetComponent<CursorControl>();
        }
        catch
        {
            Debug.LogError("Please assign the correct game object");
        }
    }

    private void Start()
    {
        //���� �̸�����
        nextFruitModel = Instantiate(FruitPrefab, NextFruitParent.transform, false);
        nextFruitModel.GetComponent<Rigidbody2D>().simulated = false; //�����ۿ� ����

        CreateFruit(); //ȭ�鿡�� â������.
        StopRigidSim(); //������ �ٵ� ��� �����
        isGameRun = true;
    }

    void Update()
    {
        float step = speed * Time.deltaTime;

        //���콺�� ������ �� true�� �����
        SetIsClicked();

        if (isGameRun)
        {
            if (newFruitGameObject)
            {
                if (isClicked) //Ŭ���� �Ǹ� ����߸�
                {
                    isClicked = false;

                    //��� ������ Ground ���¶��
                    if (isReady)
                    {
                        isReady = false;
                        SetInitialCoroutine(); //1�� �� ���� ���� �ʱ�ȭ : isGround, target
                    }
                    PlayRigidSim(); //����߸���
                }

                if (!isSimulated)
                {
                    //�������� ���߿��� ����ٴ��� ����
                    //������ Ŀ���� ����ٴ�
                    cursorPos = cachedCursorControl != null
                        ? cachedCursorControl.cursorPos
                        : GameObject.Find("Cursor").GetComponent<CursorControl>().cursorPos;
                    if (newFruitGameObject)
                    {
                        // 내 박스 경계(leftBorder~rightBorder) 안으로 클램핑
                        float clampedX = Mathf.Clamp(cursorPos.x, leftBorder, rightBorder);
                        newFruitGameObject.transform.position = new Vector3(clampedX, newFruitGameObject.transform.position.y, newFruitGameObject.transform.position.z);
                    }
                }
            }
            
        }
    }

    //��ƼŬ ���
    public void PlayEffect(Transform trans, GameObject EffectGameObject)
    {
        GameObject newEffect;
        try
        {
            newEffect = Instantiate(EffectGameObject, EffectParent.transform, false);
            newEffect.transform.position = new Vector3(trans.position.x, trans.position.y, trans.position.z);
            newEffect.GetComponent<ParticleSystem>().Play();//��� (�˾Ƽ� ������)
        }
        catch
        {
            Debug.Log("effect null");
        }
    }

    //���콺�� ������ �� true�� �����
    private void SetIsClicked()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isClicked = true;
        }
    }

    void ShowNextFruitModel()
    {
        //����
        nextFruitLevel = (int)UnityEngine.Random.Range(minLevel, fruitMaxLevel + 1);
        OnNextFruitLevelChanged?.Invoke(nextFruitLevel);

        //������ �´� ũ�� ���� (�̴� ������)
        float size = SizeUp * (nextFruitLevel - 1);

        nextFruitModel.transform.localScale = new Vector3(0.5f + size, 0.5f + size, 0.5f + size);

        //��������Ʈ�� n-1��° ��������Ʈ�� �ٲ�����
        nextFruitModel.GetComponent<SpriteRenderer>().sprite = fruitSprite[nextFruitLevel - 1];
    }

    /// <summary>
    /// ȭ�鿡�� Ŭ��(��) x��ǥ�� �޾Ƽ� (�� x��ǥ, createPoint y��ǥ) ���� â������
    /// </summary>
    void CreateFruit()
    {
        Fruit newFruit;
        //����
        int randomLevel = nextFruitLevel; /*(int)Random.Range(minLevel, fruitMaxLevel + 1);*/
        newFruitGameObject = Instantiate(FruitPrefab, FruitParent.transform, false) as GameObject;
        float randRotation = UnityEngine.Random.Range(-180f, 180f);
        newFruitGameObject.transform.localRotation = Quaternion.Euler(0, 0, randRotation);

        //������ �´� ũ�� ���� & �ִϸ��̼� �߰�
        targetSize = SizeUp * (randomLevel - 1) + 1;
        StartCoroutine(SizeUpAnim(targetSize));

        //�������� ����
        rightBorder = -gap + Wall_right.transform.position.x - targetSize / 2;
        leftBorder = +gap + Wall_left.transform.position.x + targetSize / 2;

        //��������Ʈ�� n-1��° ��������Ʈ�� �ٲ�����
        newFruitGameObject.GetComponent<SpriteRenderer>().sprite = fruitSprite[randomLevel - 1];

        //���� ����Ʈ�� �߰��Ѵ�
        newFruit = newFruitGameObject.GetComponent<Fruit>();
        newFruit.InitFruit(fruits, newFruitGameObject, randomLevel);
        fruits.Add(newFruit);

        //���� ���
        EffectSoundPlay(EffectSound.Merge);

        //���� ���� �����ֱ�
        ShowNextFruitModel();
    }

    IEnumerator SizeUpAnim(float size)
    {
        newFruitGameObject.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

        while (newFruitGameObject.transform.localScale.x < size)
        {
            newFruitGameObject.transform.localScale += new Vector3(0.1f, 0.1f, 0.1f);
            yield return new WaitForSeconds(0.025f);
        }
        yield return null;
    }

    //�ð��� 2f ������ ���� ������ �ʱ�ȭ �Ѵ�
    void SetInitialCoroutine()
    {
        StartCoroutine(InitTime1f());
    }

    /// <summary>
    /// 2�� �ð�
    /// </summary>
    /// <returns></returns>
    IEnumerator InitTime1f()
    {
        yield return new WaitForSeconds(1f);

        //�ʱ�ȭ
        isReady = true;
        target = Vector3.zero;

        //�ٽ� ����
        CreateFruit();
        StopRigidSim();//������ �ٵ� ��� �����
        groundControl.isPlayed = false;
    }

    public RankingNameInputUI rankingNameInputUI; // Name input UI script

    void GameOver()
    {
        Debug.Log("GameOver called with score: " + userScore);
        isReady = false;
        isGameRun = false;
        target = Vector3.zero;
        
        // Disable all fruits' physics instead of Time.timeScale = 0
        // This ensures UI animations still work correctly
        GameObject fruitParent = GameObject.Find("FruitParent");
        if (fruitParent != null)
        {
            Rigidbody2D[] rbs = fruitParent.GetComponentsInChildren<Rigidbody2D>();
            foreach (Rigidbody2D rb in rbs)
            {
                rb.simulated = false;
            }
        }

        if (groundControl != null)
        {
            groundControl.isPlayed = false;
        }

        // Check if current score is within top 3
        bool isInTop3 = RankingManager.IsInTop3(userScore);
        Debug.Log("Is in top 3: " + isInTop3);

        if (isInTop3)
        {
            // Show Name Input UI
            if (rankingNameInputUI != null)
            {
                rankingNameInputUI.Show(userScore);
                Debug.Log("Showing name input UI");
            }
            else
            {
                Debug.LogError("RankingNameInputUI not assigned in FruitManager! Showing ranking page as fallback.");
                if (rankingPageGameObject != null) rankingPageGameObject.SetActive(true);
            }
        }
        else
        {
            // Just show ranking page if not in top 3
            if (rankingPageGameObject != null)
            {
                rankingPageGameObject.SetActive(true);
                Debug.Log("Showing ranking page");
            }
            GameFlowManager.OnSimpleGameOver();
        }

        // If no UI was shown at all, we might need a fallback or just don't pause the game
        if (rankingNameInputUI == null && rankingPageGameObject == null)
        {
            Debug.LogError("No Game Over UI assigned! The game will just stop.");
        }
    }

    void StopRigidSim()
    {
        isSimulated = false;
        if (newFruitGameObject != null)
        {
            newFruitGameObject.GetComponent<Rigidbody2D>().simulated = false;
        }
    }

    void PlayRigidSim()
    {
        isSimulated = true;
        newFruitGameObject.GetComponent<Rigidbody2D>().simulated = true;

        // 드롭 이벤트 발생 (멀티플레이어 동기화용)
        // localPosition 사용: 상대방 박스(FruitParent_Other)에서도 같은 로컬 좌표로 재현 가능
        int droppedLevel = newFruitGameObject.GetComponent<Fruit>().level;
        float localX = newFruitGameObject.transform.localPosition.x;
        float localY = newFruitGameObject.transform.localPosition.y;
        float rotation = newFruitGameObject.transform.localRotation.eulerAngles.z;
        OnFruitDropped?.Invoke(droppedLevel, localX, localY, rotation);
    }

    public void EffectSoundPlay(EffectSound effectSound)
    {

        switch (effectSound)
        {
            case EffectSound.Drop:
                SoundChannels[channelNum].clip = audioClips[0];
                SoundChannels[channelNum].Play();
                break;
            case EffectSound.Merge: //1�� 2�߿� �����ϰ� ���
                SoundChannels[channelNum].clip = audioClips[(int)UnityEngine.Random.Range(1f, 2.1f)];
                SoundChannels[channelNum].Play();
                break;
        }

        channelNum = (channelNum + 1) % SoundChannels.Length;

    }

    //���� ����
    public void UpdateScore(int addedScore)
    {
        userScore += addedScore;
        ScoreText.text = string.Format("{0:00000}", userScore);
        PlayEffect(ScoreTextsPosition, ScoreEffectGameObject);
        OnScoreUpdated?.Invoke(userScore);
    }
}
