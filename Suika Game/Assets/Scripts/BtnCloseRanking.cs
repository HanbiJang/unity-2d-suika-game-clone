using UnityEngine;
using UnityEngine.UI;

public class BtnCloseRanking : MonoBehaviour
{
    public RankingPage rankingPage;

    private void Start()
    {
        Button thisBtn = GetComponent<Button>();
        thisBtn.onClick.AddListener(CloseFunc);

        if (rankingPage == null)
        {
            Debug.LogError("랭킹 페이지를 할당하세요");
        }
    }

    public void CloseFunc()
    {
        rankingPage.CloseRankingPage();
    }
}
