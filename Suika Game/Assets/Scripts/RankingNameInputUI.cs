using UnityEngine;
using UnityEngine.UI;

public class RankingNameInputUI : MonoBehaviour
{
    public InputField nameInputField;
    public Button submitButton;
    public GameObject rankingPageGameObject;
    public GameFlowManager GameFlowManager;
    
    private int currentScore;

    private void Awake() 
    {
        if (submitButton != null)
        {
            submitButton.onClick.AddListener(OnSubmit);
        }
        gameObject.SetActive(false);
    }

    public void Show(int score)
    {
        currentScore = score;
        gameObject.SetActive(true);
        if (nameInputField != null)
        {
            nameInputField.text = "";
            nameInputField.ActivateInputField();
        }
    }

    public void OnSubmit()
    {
        string playerName = nameInputField != null ? nameInputField.text : "Anonymous";
        if (string.IsNullOrEmpty(playerName))
        {
            playerName = "Anonymous";
        }

        RankingManager.AddRanking(playerName, currentScore);
        
        // Hide name input UI and show ranking page
        gameObject.SetActive(false);
        if (rankingPageGameObject != null)
        {
            rankingPageGameObject.SetActive(true);
            // Update the ranking UI to show the newly added score
            RankingPage rankingPage = rankingPageGameObject.GetComponent<RankingPage>();
            if (rankingPage != null)
            {
                rankingPage.UpdateRankingUI();
            }
        }

        GameFlowManager.OnRankingUploadComplete();
    }
}
