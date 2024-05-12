using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RankingPage : MonoBehaviour
{
    List<Tuple<string, int>> RankingData;
    public GameObject RankingContent; //인스펙터 할당
    public static int rankingCnt = 10; //보여줄 상위 랭커의 수
    public GameObject RankingContentPrefab;
    List<GameObject> RankingContentChild;
    public ScrollRect scrollRect; //스크롤뷰 위치 고정
    private float[] pagePositions;

    public PlayFabManager playFabManager; //인스펙터 할당

    private void Awake()
    {
        RankingData = new List<Tuple<string, int>>();
        if (RankingContent == null || RankingContentPrefab == null)
        {
            Debug.LogError("RankingContent를 할당하세요");
        }
        RankingContentChild = new List<GameObject>();
    }
    //활성화 시에 실행
    private void OnEnable()
    {
        playFabManager.GetLeaderboard(MyLeaderboardCallback);
    }

    public void ShowWorldRanking()
    {
        //프리팹으로 자식 콘텐츠를 표시하기 + 생성
        if (RankingContentChild.Count == 0)
        {
            for (int i = 0; i < rankingCnt; i++)
            {
                GameObject newContent = Instantiate(RankingContentPrefab, RankingContent.transform);
                Text playerName = newContent.GetComponentsInChildren<Text>()[0];
                Text playerScore = newContent.GetComponentsInChildren<Text>()[1];
                Text count = newContent.GetComponentsInChildren<Text>()[2];
                try
                {
                    playerName.text = RankingData[i].Item1;
                    playerScore.text = RankingData[i].Item2.ToString();
                }
                catch
                {
                    Debug.Log("RankingData에 데이터가 없음");
                }
                count.text = string.Format("{0:00}", i + 1);
                RankingContentChild.Add(newContent);
            }
        }
        else
        {
            for (int i = 0; i < rankingCnt; i++)
            {
                GameObject curContent = RankingContentChild[i];
                Text playerName = curContent.GetComponentsInChildren<Text>()[0];
                Text playerScore = curContent.GetComponentsInChildren<Text>()[1];
                Text count = curContent.GetComponentsInChildren<Text>()[2];

                try
                {
                    playerName.text = RankingData[i].Item1;
                    playerScore.text = RankingData[i].Item2.ToString();                
                }
                catch
                {
                    Debug.Log("RankingData에 데이터가 없음");
                }
                count.text = string.Format("{0:00}", i + 1);
            }
        }
    }

    public void SetRankingData(List<Tuple<string, int>> data)
    {
        RankingData = data;
    }

    private void Update()
    {      
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mousePosition = Input.mousePosition/*Camera.main.ScreenToWorldPoint(Input.mousePosition)*/;

            // Canvas가 활성화되어 있고 클릭된 위치가 Canvas 내부가 아닌 경우 Canvas를 비활성화
            if (this.gameObject.activeSelf
                && !RectTransformUtility.RectangleContainsScreenPoint(this.gameObject.GetComponent<RectTransform>(), mousePosition))
            {
                //애니메이션 재생
                gameObject.GetComponent<Animation>().Play("RankingPageCloseAnim");
            }
            
        }
    }

    // 애니메이션 이벤트에서 호출될 함수
    public void OnAnimationEnd()
    {
        // 애니메이션이 끝나면 Canvas를 비활성화합니다.
        gameObject.SetActive(false);
    }

    //리더보드에서 값을 갱신하면 화면에 표시 (콜백)
    public void MyLeaderboardCallback(PlayFab.ClientModels.GetLeaderboardResult result)
    {
        Debug.Log("Leaderboard received successfully!");
        ShowWorldRanking();
    }

}
