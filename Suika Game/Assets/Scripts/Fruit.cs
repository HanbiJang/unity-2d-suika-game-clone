using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Fruit : MonoBehaviour
{
    public GameObject FruitGameObject { get; set; } //���� ���� ������Ʈ
    public int level { get; set; } //������ ����
    public int FruitIndex { get; set; } //������ ����Ʈ �� �ε���

    FruitManager fruitManager;
    bool isMerge;
    bool isSounded = false;

    /// <summary>
    /// true면 상대방 표시용 과일 → 로컬 점수/fruitMaxLevel에 영향 없음
    /// MultiGameManager에서 생성 시 설정
    /// </summary>
    [HideInInspector] public bool isOtherPlayerFruit = false;

    int[] scoreStandard = { 0, 1, 3, 6, 10, 15, 21, 28, 36, 45, 55, 66 };

    private void Start()
    {
        fruitManager = GameObject.Find("FruitManager").GetComponent<FruitManager>();
    }

    //������ ó�� ��������� �� �ʱ�ȭ�Ѵ�
    public void InitFruit(List<Fruit> fruits, GameObject fruitGameObject, int level)
    {
        this.FruitGameObject = fruitGameObject;
        this.FruitIndex = fruits.Count - 1;
        this.level = level;
        this.isMerge = false;
    }

    //������ �浹
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag == "Fruit")
        {
            Fruit other = collision.gameObject.GetComponent<Fruit>(); //�浹�� ������Ʈ

            FruitSoundPlay();

            if (other.level == this.level && !isMerge && !other.isMerge)
            {
                //Debug.Log("�浹!");
                Transform Tother = other.FruitGameObject.transform;
                Rigidbody2D Rother = other.FruitGameObject.GetComponent<Rigidbody2D>();

                //����, �������̸� => ���ʿ� �ִ� �ְ� ��� ������
                //��, �Ʒ��� => �Ʒ��� �ִ� �ְ� ��� ������
                if (this.transform.position.x < Tother.position.x) //���� ����
                {
                    isMerge = true;
                    other.isMerge = true;
                    //��� ������� & level �� or Hp ���           
                    StartCoroutine(MergeOther(Tother, Rother));

                }

                else if (this.transform.position.x == Tother.position.x && this.transform.position.y < Tother.position.y) //x��ǥ�� ���� ���� �Ʒ�
                {
                    isMerge = true;
                    other.isMerge = true;
                    //��� ������� & level �� & Hp ���
                    StartCoroutine(MergeOther(Tother, Rother));

                }

            }
        }
    }

    IEnumerator MergeOther(Transform Tother, Rigidbody2D Rother)
    {
        Rother.simulated = false;
        this.GetComponent<Rigidbody2D>().simulated = false;
        int frameCnt = 0;

        //20�����ӵ��� �����Ѵ�
        while (frameCnt < 20)
        {
            frameCnt++;
            Tother.position = Vector3.Lerp(Tother.position, this.transform.position, 0.1f);
            yield return new WaitForSeconds(0.025f); //1�����ӿ� while�� 20�� ������ ���ϰ� �Ѵ�
        }
        //�ʱ�ȭ�ϱ�
        Rother.simulated = true;
        Rother.velocity = Vector2.zero;
        Rother.angularVelocity = 0f;
        Tother.gameObject.SetActive(false);
        Destroy(Tother.gameObject);
        this.GetComponent<Rigidbody2D>().simulated = true;
        this.GetComponent<Rigidbody2D>().velocity = Vector2.zero;        //�ӵ�, ������ �ʱ�ȭ
        this.GetComponent<Rigidbody2D>().angularVelocity = 0f;


        //��ƼŬ ����Ʈ ���
        fruitManager.PlayEffect(gameObject.transform, fruitManager.MergeEffectGameObject);

        //���� ���
        fruitManager.EffectSoundPlay(EffectSound.Merge);

        //���� ���� (���ʹ� ǥ�ý��� ����̹Ƿ� ������ ����)
        if (!isOtherPlayerFruit)
            fruitManager.UpdateScore(this.level * scoreStandard[this.level]);

        //���� ������
        if (this.level != FruitManager.maxLevel)
        {
            //����, ũ�� ����
            gameObject.GetComponent<SpriteRenderer>().sprite = fruitManager.fruitSprite[(level + 1) - 1];
            float SizeUp = 0.2f;
            this.transform.localScale += new Vector3(SizeUp, SizeUp, SizeUp);
            this.level += 1;
            //�ִ� ���� ���� ���� (���ʹ� ������ ����)
            if (!isOtherPlayerFruit)
                fruitManager.fruitMaxLevel = Mathf.Max(this.level - 1, fruitManager.fruitMaxLevel);
        }

        //�ʱ�ȭ
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
