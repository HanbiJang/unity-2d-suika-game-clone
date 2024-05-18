
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
    public GameObject FruitPrefab; //과일의 프리펩
    GameObject FruitParent; //과일 부모
    public GameObject newFruitGameObject { get; set; } //새 과일
    GroundControl groundControl; //땅 스크립트 ( 소리 기능 ) 

    Vector2 ClickPoint; //클릭된 좌표
    List<Fruit> fruits; //과일의 리스트
    bool isClicked; //클릭되었는지. true면 클릭 동작을 막는다
    public bool isReady; //true면 시간 1초 지났음을 확인
    float speed = 10f;
    Vector3 target; //목표 지점

    public const int minLevel = 1; //최소 레벨 (고정)
    public int fruitMaxLevel = 1; //씬상의 과일 최대 레벨
    public const int maxLevel = 11; //실제 최대 레벨 

    //과일 스프라이트
    public List<Sprite> fruitSprite; //인스펙터에서 할당

    //파티클
    GameObject EffectParent;
    public GameObject MergeEffectGameObject; //게임오브젝트, 인스펙터 할당
    public GameObject ScoreEffectGameObject; //게임오브젝트, 인스펙터 할당

    //사운드
    public AudioClip[] audioClips; //인스펙터 할당
    public AudioSource[] SoundChannels; //인스펙터 할당
    int channelNum = 0;

    //점수
    public int userScore = 0;
    Text ScoreText;
    Transform ScoreTextsPosition;

    //벽
    GameObject Wall_left;
    GameObject Wall_right;
    GameObject Ground;
    GameObject GameOverLine; //게임 오버 기능
    GameObject NextFruitParent;

    float SizeUp = 0.2f;

    bool isGameRun = false;
    bool isGameOver = false;
    int nextFruitLevel = 1;
    GameObject nextFruitModel;

    Vector3 cursorPos; //마우스 커서 위치, 생성된 과일만이 따라다님

    public float rightBorder;
    public float leftBorder;
    float gap = 0.25f;
    bool isSimulated = false; //새 과일이 물리력 받는지에 대한 bool
    float targetSize;

    PlayFabManager playFabManager;

    public void makeGameOver()
    {
        isGameRun = false;
    }
    public bool isGameRun_func()
    {
        return isGameRun;
    }

    void Awake()
    {
        target = Vector3.zero;
        isReady = true;
        fruits = new List<Fruit>(); //과일 리스트 동적할당
        ClickPoint = Vector2.zero;

        try
        {
            FruitParent = GameObject.Find("FruitParent");
            groundControl = GameObject.Find("Ground").GetComponent<GroundControl>();
            ScoreText = GameObject.Find("ScoreText").GetComponent<Text>();
            ScoreTextsPosition = ScoreText.transform;
            EffectParent = GameObject.Find("EffectParent");
            Wall_left = GameObject.Find("Wall_left");
            Wall_right = GameObject.Find("Wall_right");
            Ground = GameObject.Find("Ground");
            GameOverLine = GameObject.Find("GameOverLine");
            NextFruitParent = GameObject.Find("NextFruitParent");
           
        }
        catch
        {
            Debug.LogError("Please assign the correct game object");
        }
    }

    private void Start()
    {
        //다음 미리보기
        nextFruitModel = Instantiate(FruitPrefab, NextFruitParent.transform, false);
        nextFruitModel.GetComponent<Rigidbody2D>().simulated = false; //물리작용 끄기

        CreateFruit(); //화면에서 창조를함.
        StopRigidSim(); //리지드 바디를 잠시 멈춘다
        isGameRun = true;

        playFabManager = GameObject.Find("PlayFabManager").GetComponent<PlayFabManager>();
    }

    void Update()
    {
        float step = speed * Time.deltaTime;

        //마우스가 눌렸을 시 true로 만든다
        SetIsClicked();

        if (isGameRun)
        {
            if (newFruitGameObject)
            {
                if (isClicked) //클릭이 되면 떨어뜨림
                {
                    isClicked = false;

                    //모든 과일이 Ground 상태라면
                    if (isReady)
                    {
                        isReady = false;
                        SetInitialCoroutine(); //1초 후 게임 조건 초기화 : isGround, target
                    }
                    PlayRigidSim(); //떨어뜨리기
                }

                if (!isSimulated)
                {
                    //떨어지는 와중에는 따라다니지 않음
                    //과일이 커서를 따라다님
                    cursorPos = GameObject.Find("Cursor").GetComponent<CursorControl>().cursorPos;
                    if (newFruitGameObject)
                    {
                        newFruitGameObject.transform.position = new Vector3(cursorPos.x, newFruitGameObject.transform.position.y, newFruitGameObject.transform.position.z);
                    }
                }
            }
            
        }
        else //게임 오버 판단 - GameOverLine
        {
            if (!isGameOver)
            {
                GameOver();
                isGameOver = true; 
            }
        }

    }

    //파티클 재생
    public void PlayEffect(Transform trans, GameObject EffectGameObject)
    {
        GameObject newEffect;
        try
        {
            newEffect = Instantiate(EffectGameObject, EffectParent.transform, false);
            newEffect.transform.position = new Vector3(trans.position.x, trans.position.y, trans.position.z);
            newEffect.GetComponent<ParticleSystem>().Play();//재생 (알아서 삭제됨)
        }
        catch
        {
            Debug.Log("effect null");
        }
    }

    //마우스가 눌렸을 시 true로 만든다
    private void SetIsClicked()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isClicked = true;
        }
    }

    void ShowNextFruitModel()
    {
        //생성
        nextFruitLevel = (int)UnityEngine.Random.Range(minLevel, fruitMaxLevel + 1);

        //레벨에 맞는 크기 적용 (미니 사이즈)
        float size = SizeUp * (nextFruitLevel - 1);

        nextFruitModel.transform.localScale = new Vector3(0.5f + size, 0.5f + size, 0.5f + size);

        //스프라이트도 n-1번째 스프라이트로 바뀌어야함
        nextFruitModel.GetComponent<SpriteRenderer>().sprite = fruitSprite[nextFruitLevel - 1];
    }

    /// <summary>
    /// 화면에서 클릭(업) x좌표를 받아서 (그 x좌표, createPoint y좌표) 에서 창조를함
    /// </summary>
    void CreateFruit()
    {
        Fruit newFruit;
        //생성
        int randomLevel = nextFruitLevel; /*(int)Random.Range(minLevel, fruitMaxLevel + 1);*/
        newFruitGameObject = Instantiate(FruitPrefab, FruitParent.transform, false) as GameObject;
        float randRotation = UnityEngine.Random.Range(-180f, 180f);
        newFruitGameObject.transform.localRotation = Quaternion.Euler(0, 0, randRotation);

        //레벨에 맞는 크기 적용 & 애니메이션 추가
        targetSize = SizeUp * (randomLevel - 1) + 1;
        StartCoroutine(SizeUpAnim(targetSize));

        //보더라인 설정
        rightBorder = -gap + Wall_right.transform.position.x - targetSize / 2;
        leftBorder = +gap + Wall_left.transform.position.x + targetSize / 2;

        //스프라이트도 n-1번째 스프라이트로 바뀌어야함
        newFruitGameObject.GetComponent<SpriteRenderer>().sprite = fruitSprite[randomLevel - 1];

        //과일 리스트에 추가한다
        newFruit = newFruitGameObject.GetComponent<Fruit>();
        newFruit.InitFruit(fruits, newFruitGameObject, randomLevel);
        fruits.Add(newFruit);

        //사운드 재생
        EffectSoundPlay(EffectSound.Merge);

        //다음 과일 보여주기
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

    //시간이 2f 지나면 게임 조건을 초기화 한다
    void SetInitialCoroutine()
    {
        StartCoroutine(InitTime1f());
    }

    /// <summary>
    /// 2초 시간
    /// </summary>
    /// <returns></returns>
    IEnumerator InitTime1f()
    {
        yield return new WaitForSeconds(1f);

        //초기화
        isReady = true;
        target = Vector3.zero;

        //다시 생성
        CreateFruit();
        StopRigidSim();//리지드 바디를 잠시 멈춘다
        groundControl.isPlayed = false;
    }

    void GameOver()
    {
        isReady = false;
        target = Vector3.zero;
        StopRigidSim();//리지드 바디를 잠시 멈춘다
        groundControl.isPlayed = false;
        Time.timeScale = 0;

        //게임 결과 & 리플레이 버튼 띄우기
        //...

        //유저의 역대 최대 점수를 넘겼다면 플레이팹에 저장하기
        //최대 점수 비교
        //플레이팹에 점수 저장
        if (playFabManager.isLogOn == true)
        {     
            playFabManager.SetStat(userScore); //점수 저장
            if(userScore> playFabManager.playerBestScore)
                playFabManager.SetBestScore(userScore);
        }
        else {
            playFabManager.LoginToPlayFab(null);
            //점수 저장
            playFabManager.LoginToPlayFab(() =>
            {
                // 로그인 성공 후, 점수 저장 
                playFabManager.SetStat(userScore); //점수 저장
                if (userScore > playFabManager.playerBestScore)
                    playFabManager.SetBestScore(userScore);
            });
        }

    }

    void StopRigidSim()
    {
        isSimulated = false;
        newFruitGameObject.GetComponent<Rigidbody2D>().simulated = false;
    }

    void PlayRigidSim()
    {
        isSimulated = true;
        newFruitGameObject.GetComponent<Rigidbody2D>().simulated = true;
    }

    public void EffectSoundPlay(EffectSound effectSound)
    {

        switch (effectSound)
        {
            case EffectSound.Drop:
                SoundChannels[channelNum].clip = audioClips[0];
                SoundChannels[channelNum].Play();
                break;
            case EffectSound.Merge: //1과 2중에 랜덤하게 재생
                SoundChannels[channelNum].clip = audioClips[(int)UnityEngine.Random.Range(1f, 2.1f)];
                SoundChannels[channelNum].Play();
                break;
        }

        channelNum = (channelNum + 1) % SoundChannels.Length;

    }

    //점수 계산기
    public void UpdateScore(int addedScore)
    {
        userScore += addedScore;
        ScoreText.text = string.Format("{0:00000}", userScore);
        PlayEffect(ScoreTextsPosition, ScoreEffectGameObject);
    }
}
