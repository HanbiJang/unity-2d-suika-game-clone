using UnityEngine;
using UnityEngine.SceneManagement;
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
        // PhotonNetworkManager가 없으면 씬 이동 전 생성
        if (PhotonNetworkManager.Instance == null)
        {
            GameObject obj = new GameObject("PhotonNetworkManager");
            obj.AddComponent<PhotonNetworkManager>();
        }

        SceneManager.LoadScene("LobbyScene");
    }
}
