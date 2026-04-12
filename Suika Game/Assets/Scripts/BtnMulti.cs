using UnityEngine;
using UnityEngine.UI;

public class BtnMulti : MonoBehaviour
{
    private void Start()
    {
        Button thisBtn = GetComponent<Button>();
        thisBtn.onClick.AddListener(MultiFunc);
    }

    public void MultiFunc()
    {
        LoadingSceneController loadingSceneController = GameObject.Find("LoadingSceneController").GetComponent<LoadingSceneController>();
        loadingSceneController.MyLoadingSceneTo("MultiModeScene");
    }
}
