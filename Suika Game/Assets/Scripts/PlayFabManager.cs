using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PlayFabManager : MonoBehaviour
{
    public bool isLogOn = false;
    bool isBestScoreLoaded = false;
    public int curSceneNum = 0;
    static string customId = "";
    static string playfabId = "";
    private string entityId;
    private string entityType;

    string playerName = "";
    public int playerBestScore = 0;
    string playerDisplayName = "";

    public Text BestScoreText;
    public Text UserDisplayNameText;
    public RankingPage rankingPage; //인스펙터 할당

    private void Awake()
    {
        curSceneNum = 0;
        DontDestroyOnLoad(this.gameObject); //이 게임 오브젝트를 씬 전환시에도 없어지지 않도록 설정
    }

    private void Start()
    {
        //PlayerPrefs.DeleteKey("customId");
        customId = PlayerPrefs.GetString("customId");
        //게임 시작 시 로그인..
        LoginToPlayFab(() =>
        {
            GetPlayerData();
        });

    }
    bool isNameSet = false;
    bool isBestScoreSet = false;
    private void Update()
    {
        if (!isBestScoreLoaded && curSceneNum == 1)
        {
            isBestScoreLoaded = true; //너무 많은 요청을 막기 위해
            SetBestScore();
        }

        if (!isNameSet && isLogOn)
        {
            isNameSet = true;
            GetDisplayNameAndSet();
        }

        if (!isBestScoreSet && isBestScoreLoaded && SceneManager.GetActiveScene().buildIndex == 1)
        {  
            BestScoreText = GameObject.Find("BestScoreText").GetComponent<Text>();
            if (BestScoreText) BestScoreText.text = string.Format("{0:00000}", playerBestScore);
            isBestScoreSet = true;
        }
    }

    void SetBestScore()
    {
        //로그인이 되었다면
        if (!isLogOn)
        {
            LoginToPlayFab(() =>
            {
                // 로그인 성공 후 GetPlayerBestScore 실행
                GetPlayerBestScore();
            });
        }

        else GetPlayerBestScore(); //플레이어 데이터 가져오기
    }

    void SetDisplayNameToText(string str)
    {
        if (curSceneNum == 0)
        {
            UserDisplayNameText.text = "ID: " + str;
        }
    }

    public void LoginToPlayFab(Action LoginSuccess)
    {
        if (string.IsNullOrEmpty(PlayFabSettings.staticSettings.TitleId))
            PlayFabSettings.staticSettings.TitleId = "1DEBB";

        if (string.IsNullOrEmpty(customId)) CreateGuestIdAndLogin();
        else LoginGuestId();
    }

    void LoginGuestId()
    {
        Debug.Log("Guest Login");

        var request = new LoginWithCustomIDRequest { CustomId = customId, CreateAccount = false };
        PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccess, OnLoginFailure);
    }

    void CreateGuestIdAndLogin()
    {
        //예전 정보 삭제
        DeletePreData();

        customId = GetRandomString(16);
        PlayerPrefs.SetString("customId", customId);
        Debug.Log("customId : " + customId);

        var request = new LoginWithCustomIDRequest { CustomId = customId, CreateAccount = true };
        PlayFabClientAPI.LoginWithCustomID(request, OnCreateLoginSuccess, OnLoginFailure);
    }

    void DeletePreData()
    {
        PlayerPrefs.DeleteAll();
    }

    void SetDisplayName(string name)
    {
        var request = new UpdateUserTitleDisplayNameRequest { DisplayName = name };
        PlayFabClientAPI.UpdateUserTitleDisplayName(request, (result) => print("디스플레이네임 변경 성공"), (error) => print("디스플레이네임 변경 실패"));
    }

    void GetDisplayNameAndSet()
    {
        var request = new GetPlayerProfileRequest
        {
            PlayFabId = playfabId
        };

        PlayFabClientAPI.GetPlayerProfile(request,
            result => SetDisplayNameToText(playerDisplayName=result.PlayerProfile.DisplayName),
            error => Debug.LogError("플레이어 프로필 가져오기 실패: " + error.GenerateErrorReport())
        );
    }

    public void SetStat(int highScore)
    {
        var request = new UpdatePlayerStatisticsRequest { Statistics = new List<StatisticUpdate> { new StatisticUpdate { StatisticName = "HighScore", Value = highScore } } };
        PlayFabClientAPI.UpdatePlayerStatistics(request, (result) => print("HighScore 값 저장됨"), (error) => print("HighScore 값 저장실패"));
    }

    public delegate void LeaderboardCallback(GetLeaderboardResult result);
    public void GetLeaderboard(LeaderboardCallback callback)
    {
        var request = new GetLeaderboardRequest
        {
            StartPosition = 0,
            StatisticName = "HighScore",
            MaxResultsCount = RankingPage.rankingCnt,
            ProfileConstraints = new PlayerProfileViewConstraints() { ShowDisplayName = true }
        };

        PlayFabClientAPI.GetLeaderboard(request, (result) =>
        {
            List<Tuple<string, int>> newRank = new List<Tuple<string, int>>();
            for (int i = 0; i < result.Leaderboard.Count; i++)
            {
                var curBoard = result.Leaderboard[i];
                newRank.Add(Tuple.Create<string, int>(curBoard.DisplayName, curBoard.StatValue));

                Debug.Log("curboard: " + curBoard);
            }

            rankingPage.SetRankingData(newRank);

            if (callback != null) callback(result); // 콜백 함수 호출
        },
        (error) => print("리더보드 불러오기 실패"));
    }

    void OnCreateLoginSuccess(LoginResult result)
    {
        Debug.Log("아이디 생성 성공");

        playfabId = result.PlayFabId;
        entityId = result.EntityToken.Entity.Id;
        entityType = result.EntityToken.Entity.Type;

        SetPlayerNameAndBestScore("none", 0); //초기 데이터 세팅
        SetStat(0); //통계 기능 - 최고 점수 0점
        SetDisplayName(GetRandomString(10)); //통계 기능 - 랜덤 디스플레이 닉네임 설정하기

        isLogOn = true;
    }

    //로그인 성공 콜백
    void OnLoginSuccess(LoginResult result)
    {
        Debug.Log("로그인 성공");

        playfabId = result.PlayFabId;
        entityId = result.EntityToken.Entity.Id;
        entityType = result.EntityToken.Entity.Type;

        isLogOn = true;
    }


    //랜덤한 문자열 생성
    string GetRandomString(int _totLen)
    {
        string input = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var chars = Enumerable.Range(0, _totLen).Select(x => input[UnityEngine.Random.Range(0, input.Length)]);
        return new string(chars.ToArray());
    }

    //로그인 실패 콜백
    private void OnLoginFailure(PlayFabError error)
    {
        Debug.LogWarning("Something went wrong with your first API call.  :(");
        Debug.LogError("Here's some debug information:");
        Debug.LogError(error.GenerateErrorReport());
    }

    public void SetPlayerNameAndBestScore(string name, int score)
    {
        SetName(name);
        SetBestScore(score);
    }

    void SetName(string name)
    {
        var request = new UpdateUserDataRequest() { Data = new Dictionary<string, string>() { { "Name", name } } };
        PlayFabClientAPI.UpdateUserData(request, (result) => print("이름 데이터 저장 성공"), (error) => error.GenerateErrorReport());
    }

    public void SetBestScore(int score)
    {
        var request = new UpdateUserDataRequest() { Data = new Dictionary<string, string>() { { "BestScore", score.ToString() } } };
        PlayFabClientAPI.UpdateUserData(request, (result) => print("점수 데이터 저장 성공"), (error) => error.GenerateErrorReport());
    }

    void GetPlayerData()
    {
        //사용자 데이터 요청을 생성
        var request = new GetUserDataRequest() { PlayFabId = playfabId };

        //PlayFab API를 사용하여 사용자 데이터를 Get
        PlayFabClientAPI.GetUserData(request, OnDataReceived, OnDataFailed);
    }

    public void GetPlayerBestScore()
    {
        //사용자 데이터 요청을 생성
        var request = new GetUserDataRequest() { PlayFabId = playfabId };

        //PlayFab API를 사용하여 사용자 데이터를 Get
        PlayFabClientAPI.GetUserData(request, OnBestScoreReceived, OnBestScoreFailed);
    }

    void OnBestScoreReceived(GetUserDataResult result)
    {
        Debug.Log("Data Receiving Success");

        int num;
        foreach (var data in result.Data)
        {
            if (data.Key.Equals("BestScore"))
            {
                bool success = int.TryParse(data.Value.Value, out num);
                if (success) playerBestScore = num;
            }
        }

        //텍스트에 표시하기 (플래그)
        isBestScoreLoaded = true;
    }

    // 데이터를 성공적으로 받았을 때의 콜백 함수입니다.
    void OnDataReceived(GetUserDataResult result)
    {
        Debug.Log("Data Receiving Success");

        int num;
        foreach (var data in result.Data)
        {
            if (data.Key.Equals("Name")) playerName = data.Value.Value;
            else if (data.Key.Equals("BestScore"))
            {
                bool success = int.TryParse(data.Value.Value, out num);
                if (success) playerBestScore = num;
            }
        }

        Debug.Log("Name: " + playerName + "PlayerBestScore: " + playerBestScore);
    }

    // 데이터를 받는 데 실패했을 때의 콜백 함수입니다.
    void OnDataFailed(PlayFabError error)
    {
        print("데이터 불러오기 실패");
    }

    void OnBestScoreFailed(PlayFabError error)
    {
        print("최고 점수 불러오기 실패");
        isBestScoreLoaded = false;
    }
}