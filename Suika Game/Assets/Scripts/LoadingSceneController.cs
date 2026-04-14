using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadingSceneController : MonoBehaviour
{
    [SerializeField] public GameObject CurtainImg;

    private bool isLoaded = false;
    private bool isAutoTransition = false;
    private bool pendingOpenAfterSceneLoad = false;
    private bool isOpenAnimPlayed = false;

    private const float MultiCloseDuration = 3f;
    private const float MultiOpenDelay = 0.3f;

    private AsyncOperation operation;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public void MyLoadingScene()
    {
        StartCoroutine(LoadSceneAsync("GameScene"));
    }

    public void MyLoadingSceneTo(string sceneName)
    {
        StartCoroutine(LoadSceneAsync(sceneName));
    }

    public void StartAutoTransitionToMulti(Action onCurtainClosed)
    {
        isAutoTransition = true;
        isOpenAnimPlayed = false;
        StartCoroutine(AutoTransitionCoroutine(onCurtainClosed));
    }

    private IEnumerator AutoTransitionCoroutine(Action onCurtainClosed)
    {
        if (CurtainImg == null)
        {
            Debug.LogWarning("[LoadingSceneController] CurtainImg가 없어 즉시 씬 전환 콜백을 실행합니다.");
            onCurtainClosed?.Invoke();
            yield break;
        }

        CurtainImg.SetActive(true);
        CurtainImg.GetComponent<Animation>().Play("CurtainCloseAnim");
        yield return new WaitForSeconds(MultiCloseDuration);

        pendingOpenAfterSceneLoad = true;
        onCurtainClosed?.Invoke();
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;
        CurtainImg.SetActive(true);
        CurtainImg.GetComponent<Animation>().Play("CurtainCloseAnim");

        while (!operation.isDone)
        {
            yield return null;

            if (operation.progress < 0.9f)
            {
                continue;
            }

            yield return new WaitForSeconds(1f);
            isLoaded = true;
            yield break;
        }

        yield return new WaitForSeconds(0.3f);
    }

    private void Update()
    {
        if (isAutoTransition)
            return;

        if (isLoaded && !isOpenAnimPlayed && Input.GetMouseButton(0))
        {
            operation.allowSceneActivation = true;
            CurtainImg.GetComponent<Animation>().Play("CurtainOpenAnim");
            isOpenAnimPlayed = true;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!pendingOpenAfterSceneLoad || CurtainImg == null)
            return;

        StartCoroutine(PlayOpenAfterSceneLoad());
    }

    private IEnumerator PlayOpenAfterSceneLoad()
    {
        pendingOpenAfterSceneLoad = false;
        yield return new WaitForSeconds(MultiOpenDelay);
        CurtainImg.GetComponent<Animation>().Play("CurtainOpenAnim");
        isOpenAnimPlayed = true;
        isAutoTransition = false;
    }
}
