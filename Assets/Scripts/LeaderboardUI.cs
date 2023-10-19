using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Services.Leaderboards.Models;
using UnityEngine;

public class LeaderboardUI : MonoBehaviour
{
    public GameObject leaderboardEntryPrefab;
    
    private List<GameObject> _entries = new();
    
    public void DisplayScores(LeaderboardEntry[] scores)
    {
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            Destroy(_entries[i]);
            _entries.RemoveAt(i);
        }
        
        foreach (var score in scores)
        {
            var entry = Instantiate(leaderboardEntryPrefab, transform);
            entry.GetComponentInChildren<TextMeshProUGUI>().text = $"{score.Rank}. {score.PlayerId} - {score.Score}";
            _entries.Add(entry);
        }
    }

}
