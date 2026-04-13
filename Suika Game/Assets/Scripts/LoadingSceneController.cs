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
        DontDestroyOnLoad(this.gameObject);
    }

    public void MyLoadingScene()
    {
        StartCoroutine(LoadSceneAsync("GameScene"));
    }

    public void MyLoadingSceneTo(string sceneName)
    {
        StartCoroutine(LoadSceneAsync(sceneName));
    }
    AsyncOperation operation;
    IEnumerator LoadSceneAsync(string sceneName)
    {
        // �񵿱� �� �ε� ����
        operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;
        CurtainImg.SetActive(true);

        //Ŀư �ݴ� �ִϸ��̼� ����
        CurtainImg.GetComponent<Animation>().Play("CurtainCloseAnim");

        // �ε��� �Ϸ�� ������ ���
        while (!operation.isDone)
        {
            yield return null;
            float timer = 0f;
            if (operation.progress < 0.9f)
            {
                //����
            }
            else
            {
                //����ũ
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
