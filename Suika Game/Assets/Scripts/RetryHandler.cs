using UnityEngine;
using UnityEngine.SceneManagement;

public class RetryHandler : MonoBehaviour
{
    public void RestartGame()
    {
        // 현재 활성화된 씬의 이름을 가져와서 다시 로드합니다.
        string currentSceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentSceneName);

        // 만약 게임 오버 시 Time.timeScale = 0; 을 했다면 다시 1로 돌려줘야 합니다.
        Time.timeScale = 1f;
    }

    public void GoToMain()
    {
        // 필요하다면 메인 메뉴로 돌아가는 기능도 추가할 수 있습니다.
        // SceneManager.LoadScene("MainSceneName");
    }
}