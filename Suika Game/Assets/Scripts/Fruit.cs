using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Fruit : MonoBehaviour
{
    public GameObject FruitGameObject { get; set; }
    public int level { get; set; }
    public int FruitIndex { get; set; }

    FruitManager fruitManager;
    bool isMerge;
    bool isSounded = false;

    /// <summary>현재 머지 애니메이션 실행 중인지.</summary>
    public bool IsMerging => isMerge;

    /// <summary>
    /// 드롭 순서 고유 ID. 네트워크 상에서 같은 과일을 특정하기 위해 사용.
    /// FruitManager.CreateFruit()에서 할당, MultiGameManager에서 복사.
    /// </summary>
    [HideInInspector] public int dropId = -1;

    /// <summary>
    /// true면 상대방 표시용 과일.
    /// - 로컬 점수/fruitMaxLevel에 영향 없음
    /// - OnCollisionEnter2D에서 로컬 머지 실행 안 함
    /// - 물리 완전 비활성: 위치는 스냅샷으로만 결정
    /// </summary>
    [HideInInspector] public bool isOtherPlayerFruit = false;

    // ─── 네트워크 보간용 ─────────────────────────────────────────
    [HideInInspector] public Vector3 networkTargetLocalPos;
    [HideInInspector] public float   networkTargetRot;
    [HideInInspector] public bool    hasNetworkTarget = false;

    /// <summary>보간 속도. 클수록 빠르게 따라감.</summary>
    const float LERP_SPEED = 20f;

    int[] scoreStandard = { 0, 1, 3, 6, 10, 15, 21, 28, 36, 45, 55, 66 };

    private void Start()
    {
        fruitManager = GameObject.Find("FruitManager").GetComponent<FruitManager>();
    }

    private void Update()
    {
        // 상대방 과일: 스냅샷 목표 위치를 향해 부드럽게 보간
        if (isOtherPlayerFruit && hasNetworkTarget)
        {
            transform.localPosition = Vector3.Lerp(
                transform.localPosition, networkTargetLocalPos, Time.deltaTime * LERP_SPEED);

            float currentRot = transform.localRotation.eulerAngles.z;
            float newRot = Mathf.LerpAngle(currentRot, networkTargetRot, Time.deltaTime * LERP_SPEED);
            transform.localRotation = Quaternion.Euler(0f, 0f, newRot);
        }
    }

    public void InitFruit(List<Fruit> fruits, GameObject fruitGameObject, int level)
    {
        this.FruitGameObject = fruitGameObject;
        this.FruitIndex = fruits.Count - 1;
        this.level = level;
        this.isMerge = false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag == "Fruit")
        {
            Fruit other = collision.gameObject.GetComponent<Fruit>();

            FruitSoundPlay();

            // 상대방 과일은 로컬 머지 금지 → 네트워크 이벤트(EVT_FRUIT_MERGE)로만 머지
            if (isOtherPlayerFruit) return;

            if (other.level == this.level && !isMerge && !other.isMerge)
            {
                Transform Tother = other.FruitGameObject.transform;
                Rigidbody2D Rother = other.FruitGameObject.GetComponent<Rigidbody2D>();

                if (this.transform.position.x < Tother.position.x)
                {
                    isMerge = true;
                    other.isMerge = true;
                    StartCoroutine(MergeOther(other, Tother, Rother));
                }
                else if (this.transform.position.x == Tother.position.x &&
                         this.transform.position.y < Tother.position.y)
                {
                    isMerge = true;
                    other.isMerge = true;
                    StartCoroutine(MergeOther(other, Tother, Rother));
                }
            }
        }
    }

    IEnumerator MergeOther(Fruit otherFruit, Transform Tother, Rigidbody2D Rother)
    {
        // 머지 시작 즉시 네트워크에 알림 (애니메이션 시작과 동기화)
        int otherDropId = otherFruit.dropId;
        int newLevel = (this.level < FruitManager.maxLevel) ? this.level + 1 : FruitManager.maxLevel;
        fruitManager?.NotifyMerge(this.dropId, otherDropId, newLevel);

        Rother.simulated = false;
        this.GetComponent<Rigidbody2D>().simulated = false;
        int frameCnt = 0;

        while (frameCnt < 20)
        {
            frameCnt++;
            Tother.position = Vector3.Lerp(Tother.position, this.transform.position, 0.1f);
            yield return new WaitForSeconds(0.025f);
        }

        Rother.simulated = true;
        Rother.velocity = Vector2.zero;
        Rother.angularVelocity = 0f;
        Tother.gameObject.SetActive(false);
        Destroy(Tother.gameObject);
        this.GetComponent<Rigidbody2D>().simulated = true;
        this.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
        this.GetComponent<Rigidbody2D>().angularVelocity = 0f;

        fruitManager.PlayEffect(gameObject.transform, fruitManager.MergeEffectGameObject);
        fruitManager.EffectSoundPlay(EffectSound.Merge);
        fruitManager.UpdateScore(this.level * scoreStandard[this.level]);

        if (this.level != FruitManager.maxLevel)
        {
            gameObject.GetComponent<SpriteRenderer>().sprite = fruitManager.fruitSprite[(level + 1) - 1];
            float SizeUp = 0.2f;
            this.transform.localScale += new Vector3(SizeUp, SizeUp, SizeUp);
            this.level += 1;
            fruitManager.fruitMaxLevel = Mathf.Max(this.level - 1, fruitManager.fruitMaxLevel);
        }

        isMerge = false;
    }

    /// <summary>
    /// 상대방 과일 즉시 머지 (물리 없음, 애니메이션 없음).
    /// otherFruit을 즉시 파괴하고, 자신의 레벨/스프라이트/크기를 갱신.
    /// 위치는 다음 스냅샷에서 자연스럽게 보정.
    /// </summary>
    public void ApplyNetworkMerge(Fruit otherFruit, int newLevel)
    {
        // other 과일 즉시 제거
        if (otherFruit != null && otherFruit.FruitGameObject != null)
        {
            otherFruit.FruitGameObject.SetActive(false);
            Destroy(otherFruit.FruitGameObject);
        }

        // 자신 레벨업
        if (this.level != FruitManager.maxLevel && fruitManager != null)
        {
            gameObject.GetComponent<SpriteRenderer>().sprite = fruitManager.fruitSprite[newLevel - 1];
            float SizeUp = 0.2f;
            this.transform.localScale += new Vector3(SizeUp, SizeUp, SizeUp);
            this.level = newLevel;
        }
    }

    void FruitSoundPlay()
    {
        if (!isSounded)
        {
            isSounded = true;
            StartCoroutine(FruitSoundRoutine());
        }
    }

    IEnumerator FruitSoundRoutine()
    {
        fruitManager.EffectSoundPlay(EffectSound.Drop);
        yield return new WaitForSeconds(0.5f);
        isSounded = false;
    }
}
