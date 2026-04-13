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

    /// <summary>현재 머지 애니메이션 실행 중인지. 스냅샷 보정 스킵 여부 판단에 사용.</summary>
    public bool IsMerging => isMerge;

    /// <summary>
    /// 드롭 순서 고유 ID. 네트워크 상에서 같은 과일을 특정하기 위해 사용.
    /// FruitManager.CreateFruit()에서 할당, MultiGameManager.CreateOtherFruit()에서 복사.
    /// </summary>
    [HideInInspector] public int dropId = -1;

    /// <summary>
    /// true면 상대방 표시용 과일.
    /// - 로컬 점수/fruitMaxLevel에 영향 없음
    /// - OnCollisionEnter2D에서 로컬 머지 실행 안 함 (네트워크 이벤트로만 머지)
    /// </summary>
    [HideInInspector] public bool isOtherPlayerFruit = false;

    int[] scoreStandard = { 0, 1, 3, 6, 10, 15, 21, 28, 36, 45, 55, 66 };

    private void Start()
    {
        fruitManager = GameObject.Find("FruitManager").GetComponent<FruitManager>();
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

    // other 파라미터 추가: dropId 참조 및 네트워크 이벤트 전송용
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
    /// MultiGameManager에서 네트워크 머지 이벤트 수신 시 호출.
    /// 애니메이션 포함한 머지를 실행하고 상대 과일을 제거.
    /// </summary>
    public void ApplyNetworkMerge(Fruit otherFruit, int newLevel)
    {
        if (isMerge) return;
        isMerge = true;
        otherFruit.isMerge = true;
        StartCoroutine(NetworkMergeAnim(otherFruit, newLevel));
    }

    IEnumerator NetworkMergeAnim(Fruit otherFruit, int newLevel)
    {
        if (otherFruit == null || otherFruit.FruitGameObject == null)
        {
            // 조기 종료 시 isMerge 리셋 → 이 과일이 이후 머지에 다시 참여 가능
            isMerge = false;
            yield break;
        }

        Transform Tother = otherFruit.FruitGameObject.transform;
        Rigidbody2D Rother = otherFruit.FruitGameObject.GetComponent<Rigidbody2D>();

        Rother.simulated = false;
        this.GetComponent<Rigidbody2D>().simulated = false;

        int frameCnt = 0;
        while (frameCnt < 20)
        {
            if (Tother == null) break;
            frameCnt++;
            Tother.position = Vector3.Lerp(Tother.position, this.transform.position, 0.1f);
            yield return new WaitForSeconds(0.025f);
        }

        if (Tother != null)
        {
            Tother.gameObject.SetActive(false);
            Destroy(Tother.gameObject);
        }

        this.GetComponent<Rigidbody2D>().simulated = true;
        this.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
        this.GetComponent<Rigidbody2D>().angularVelocity = 0f;

        if (fruitManager != null)
        {
            fruitManager.PlayEffect(gameObject.transform, fruitManager.MergeEffectGameObject);
            fruitManager.EffectSoundPlay(EffectSound.Merge);
        }

        if (this.level != FruitManager.maxLevel)
        {
            if (fruitManager != null)
                gameObject.GetComponent<SpriteRenderer>().sprite = fruitManager.fruitSprite[newLevel - 1];
            float SizeUp = 0.2f;
            this.transform.localScale += new Vector3(SizeUp, SizeUp, SizeUp);
            this.level = newLevel;
        }

        isMerge = false;
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
