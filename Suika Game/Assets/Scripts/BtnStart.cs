using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BtnStart : MonoBehaviour
{
    public string nextSceneName = "GameScene";

    private void Start()
    {
        Button thisBtn = this.gameObject.GetComponent<Button>();
        thisBtn.onClick.AddListener(StartFunc);
    }

    public void StartFunc() {
        SceneManager.LoadSceneAsync(nextSceneName);
        PlayFabManager playFabManager = GameObject.Find("PlayFabManager").GetComponent<PlayFabManager>();
        playFabManager.curSceneNum = 1;
    }
}
