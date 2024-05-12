using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BtnRank : MonoBehaviour
{
    public GameObject rankingPage;

    private void Start()
    {
        Button thisBtn = this.gameObject.GetComponent<Button>();
        thisBtn.onClick.AddListener(RankFunc);

        if (rankingPage == null)
        {
            Debug.LogError("랭킹 패널 할당하세요");
        }
    }

    public void RankFunc()
    {
        rankingPage.SetActive(true);
    }
}
