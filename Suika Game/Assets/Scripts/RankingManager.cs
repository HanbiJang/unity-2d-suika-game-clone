using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RankingEntry
{
    public string name;
    public int score;

    public RankingEntry(string name, int score)
    {
        this.name = name;
        this.score = score;
    }
}

[Serializable]
public class RankingData
{
    public List<RankingEntry> entries = new List<RankingEntry>();
}

public static class RankingManager
{
    private const string RANKING_KEY = "LocalRanking";
    private const int MAX_RANKING_COUNT = 3;

    public static List<RankingEntry> GetRanking()
    {
        string json = PlayerPrefs.GetString(RANKING_KEY, "");
        if (string.IsNullOrEmpty(json))
        {
            return new List<RankingEntry>();
        }

        try
        {
            RankingData data = JsonUtility.FromJson<RankingData>(json);
            return data.entries;
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to load ranking: " + e.Message);
            return new List<RankingEntry>();
        }
    }

    public static bool IsInTop3(int score)
    {
        try
        {
            List<RankingEntry> currentRanking = GetRanking();
            if (currentRanking == null || currentRanking.Count < MAX_RANKING_COUNT)
            {
                return true;
            }

            // Check if the score is higher than the lowest score in top 3
            return score > currentRanking[currentRanking.Count - 1].score;
        }
        catch (Exception e)
        {
            Debug.LogError("Error checking ranking top 3: " + e.Message);
            return true; // Return true as fallback so at least the name input UI might show
        }
    }

    public static void AddRanking(string name, int score)
    {
        List<RankingEntry> currentRanking = GetRanking();
        currentRanking.Add(new RankingEntry(name, score));

        // Sort descending by score
        currentRanking.Sort((a, b) => b.score.CompareTo(a.score));

        // Keep only top 3
        if (currentRanking.Count > MAX_RANKING_COUNT)
        {
            currentRanking.RemoveRange(MAX_RANKING_COUNT, currentRanking.Count - MAX_RANKING_COUNT);
        }

        // Save to PlayerPrefs
        RankingData data = new RankingData { entries = currentRanking };
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(RANKING_KEY, json);
        PlayerPrefs.Save();
    }
}
