using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class RankingPage : MonoBehaviour
{
    List<RankingEntry> rankingDataList;
    public GameObject RankingContent; //�ν����� �Ҵ�
    public static int rankingCnt = 3; //������ ���� ��Ŀ�� ��
    public GameObject RankingContentPrefab;
    List<GameObject> RankingContentChild;
    public ScrollRect scrollRect; //��ũ�Ѻ� ��ġ ����
    private float[] pagePositions;

    private void Awake()
    {
        rankingDataList = new List<RankingEntry>();
        if (RankingContent == null || RankingContentPrefab == null)
        {
            Debug.LogError("RankingContent�� �Ҵ��ϼ���");
        }
        RankingContentChild = new List<GameObject>();
    }
    //Ȱ��ȭ �ÿ� ����
    private void OnEnable()
    {
        UpdateRankingUI();
    }

    public void UpdateRankingUI()
    {
        rankingDataList = RankingManager.GetRanking();
        Debug.Log("Ranking UI Updated. Count: " + rankingDataList.Count);
        ShowWorldRanking();
    }

    public void ShowWorldRanking()
    {
        // 1. 데이터 정렬 (내림차순)
        rankingDataList = rankingDataList.OrderByDescending(x => x.score).ToList();

        Debug.Log("Showing Ranking. Count to display: " + rankingCnt);

        for (int i = 0; i < rankingCnt; i++)
        {
            GameObject curContent;

            // 2. 프리팹 생성 또는 기존 객체 재사용
            if (RankingContentChild.Count <= i)
            {
                curContent = Instantiate(RankingContentPrefab, RankingContent.transform);
                RankingContentChild.Add(curContent);
            }
            else
            {
                curContent = RankingContentChild[i];
            }

            // 3. 텍스트 컴포넌트 가져오기 및 예외 처리
            Text[] texts = curContent.GetComponentsInChildren<Text>();
            if (texts.Length < 3)
            {
                Debug.LogError($"{i}번째 프리팹에 Text 컴포넌트가 부족합니다!");
                continue;
            }

            // 인덱스 순서 주의: 프리팹 구조에 맞게 고정하세요. 
            // 예: [0]=순위, [1]=이름, [2]=점수
            Text nameText = texts[0];  // RankingPlayerNameText
            Text scoreText = texts[1];  // RankingPlayerScore
            Text countText = texts[2];  // count

            // 4. 데이터 반영 (데이터가 있으면 값 입력, 없으면 하이픈 처리)
            if (i < rankingDataList.Count)
            {
                nameText.text = rankingDataList[i].name;
                scoreText.text = rankingDataList[i].score.ToString();
            }
            else
            {
                nameText.text = "-";
                scoreText.text = "0";
            }

            // 5. 순위 표시 (01, 02, 03...)
            countText.text = string.Format("{0:00}", i + 1);
        }
    }

    private void Update()
    {      
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mousePosition = Input.mousePosition/*Camera.main.ScreenToWorldPoint(Input.mousePosition)*/;
            
        }
    }

    public void CloseRankingPage()
    {
        GetComponent<Animation>().Play("RankingPageCloseAnim");
    }

    public void OnAnimationEnd()
    {
        gameObject.SetActive(false);
    }


}
