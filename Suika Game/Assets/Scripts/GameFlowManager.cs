using System.Collections;
using UnityEngine;

public class GameFlowManager : MonoBehaviour
{
    [SerializeField] private GameObject gameOverCanvas;

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
}