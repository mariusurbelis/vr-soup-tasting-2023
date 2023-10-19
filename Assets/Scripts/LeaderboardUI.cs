using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LeaderboardUI : MonoBehaviour
{
    public GameObject leaderboardEntryPrefab;
    
    void Start()
    {
        for (int i = 0; i < 10; i++)
        {
            var entry = Instantiate(leaderboardEntryPrefab, transform);
            entry.GetComponentInChildren<TextMeshProUGUI>().text = $"Entry {i}";
        }
    }

}
