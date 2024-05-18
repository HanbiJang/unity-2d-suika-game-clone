using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingSceneController : MonoBehaviour
{
    [SerializeField]
    public GameObject CurtainImg;
    bool isLoaded = false;

    private void Awake()
    {
        DontDestroyOnLoad(this.gameObject); //이 게임 오브젝트를 씬 전환시에도 없어지지 않도록 설정
    }

    public void MyLoadingScene()
    {
        // 비동기 로딩 시작
        StartCoroutine(LoadSceneAsync("GameScene"));
    }
    AsyncOperation operation;
    IEnumerator LoadSceneAsync(string sceneName)
    {
        // 비동기 씬 로딩 시작
        operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;
        CurtainImg.SetActive(true);

        //커튼 닫는 애니메이션 실행
        CurtainImg.GetComponent<Animation>().Play("CurtainCloseAnim");

        // 로딩이 완료될 때까지 대기
        while (!operation.isDone)
        {
            yield return null;
            float timer = 0f;
            if (operation.progress < 0.9f)
            {
                //없음
            }
            else
            {
                //페이크
                yield return new WaitForSeconds(1f);

                isLoaded = true;
                yield break;
            }
        }

        yield return new WaitForSeconds(0.3f);
    }

    bool isOpenAnimPlayed = false;

    private void Update()
    {
        if (isLoaded && !isOpenAnimPlayed)
        {
            if (Input.GetMouseButton(0))
            {
                operation.allowSceneActivation = true;
                CurtainImg.GetComponent<Animation>().Play("CurtainOpenAnim");
                isOpenAnimPlayed = true;
            }
        }
    }
}
