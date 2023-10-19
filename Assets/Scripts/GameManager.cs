using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HoopData
{
    public int ID;
    public int Score;
    public float X;
    public float Y;
    public float Z;
    public string Color;
}

public class GameManager : MonoBehaviour
{
    public TextMeshProUGUI scoreDisplay;
    public GameObject hoopPrefab;
    public Transform gameInfoContent;
    public GameObject hoopColorInfoPrefab;
    
    private static GameManager _instance;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else
        {
            // Destroy this instance if another one already exists
            Destroy(this);
        }
    }

    private static List<GameObject> _hoops = new();
    private static List<HoopTarget> _hoopTargets = new();
    private static List<GameObject> _hoopColorInfo = new();

    public static void SpawnHoops(HoopData[] hoops)
    {
        for (var i = _hoops.Count - 1; i >= 0; i--)
        {
            Destroy(_hoops[i]);
            _hoops.RemoveAt(i);
        }

        for (var i = _hoopTargets.Count - 1; i >= 0; i--)
        {
            _hoopTargets.RemoveAt(i);
        }
        
        for (var i = _hoopColorInfo.Count - 1; i >= 0; i--)
        {
            Destroy(_hoopColorInfo[i]);
            _hoopColorInfo.RemoveAt(i);
        }

        foreach (var hoop in hoops)
        {
            var h = Instantiate(_instance.hoopPrefab, new Vector3(hoop.X, hoop.Y, hoop.Z),
                _instance.hoopPrefab.transform.rotation);
            var ht = h.GetComponentInChildren<HoopTarget>();
            ht.id = hoop.ID;
            ht.hoopScore = hoop.Score;
            _hoopTargets.Add(ht);
            _hoops.Add(h);
            
            var hci = Instantiate(_instance.hoopColorInfoPrefab, _instance.gameInfoContent);
            hci.GetComponentInChildren<TextMeshProUGUI>().text = $"Hoop points {hoop.Score}";
            hci.GetComponentInChildren<Image>().color = ColorUtility.TryParseHtmlString(hoop.Color, out var parsedColor) ? parsedColor : Color.white;
            
            foreach (var child in h.GetComponentsInChildren<Renderer>())
            {
                child.material.color = ColorUtility.TryParseHtmlString(hoop.Color, out var parsedColorHoop) ? parsedColorHoop : Color.white;
            }
        }
    }
}