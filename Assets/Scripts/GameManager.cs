using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class HoopData
{
    public int ID;
    public int Score;
    public float X;
    public float Y;
    public float Z;
    public string Color;
    public float Scale;
    public float Rotation;
}

public class GameManager : MonoBehaviour
{
    public TextMeshProUGUI scoreDisplay;
    public TextMeshProUGUI gameTimerDisplay;
    public GameObject hoopPrefab;
    public GameObject transparentWall;
    public GameObject sphereSpawner;
    public Button startButton;
    public Transform gameInfoContent;
    public GameObject hoopColorInfoPrefab;

    private static GameManager _instance;

    public static bool InGame => _instance._gameStarted;

    private bool _gameStarted = false;
    private float _gameTimer = 0;

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

        UpdateGameTimerDisplay(0);
    }

    private static List<GameObject> _hoops = new();
    private static List<HoopTarget> _hoopTargets = new();
    private static List<GameObject> _hoopColorInfo = new();

    public static void UpdateScoreDisplay(int score)
    {
        _instance.scoreDisplay.text = $"Your score: {score}";
    }

    public async void StartGame()
    {
        if (!await CloudServices.CallStartGameFunction()) return;

        transparentWall.SetActive(false);
        startButton.interactable = false;
        sphereSpawner.SetActive(true);
        _gameTimer = ConfigValues.SessionTime;
        _gameStarted = true;
    }

    private void Update()
    {
        if (_gameStarted)
        {
            _gameTimer -= Time.deltaTime;
            UpdateGameTimerDisplay(_gameTimer);

            if (_gameTimer < 0)
            {
                _gameStarted = false;
                transparentWall.SetActive(true);
                startButton.interactable = true;
                //sphereSpawner.SetActive(false);
                CloudServices.CallEndGameFunction();
                _gameTimer = 0;
                UpdateGameTimerDisplay(0);
            }
        }

        if (Keyboard.current.spaceKey.wasReleasedThisFrame)
        {
            if (!_gameStarted)
            {
                StartGame();
            }
        }
    }

    public static void UpdateGameTimer(float timer)
    {
        _instance._gameTimer = timer;
        UpdateGameTimerDisplay(timer);
    }

    public static void UpdateGameTimerDisplay(float timeLeft)
    {
        _instance.gameTimerDisplay.text = timeLeft.ToString("F2") + "s";
    }

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
                Quaternion.Euler(_instance.hoopPrefab.transform.rotation.eulerAngles.x,
                    hoop.Rotation,
                    _instance.hoopPrefab.transform.rotation.eulerAngles.z));
            h.transform.localScale = new Vector3(h.transform.localScale.x * hoop.Scale,
                h.transform.localScale.y * hoop.Scale, h.transform.localScale.z * hoop.Scale);
            var ht = h.GetComponentInChildren<HoopTarget>();
            ht.id = hoop.ID;
            ht.hoopScore = hoop.Score;
            _hoopTargets.Add(ht);
            _hoops.Add(h);

            var hci = Instantiate(_instance.hoopColorInfoPrefab, _instance.gameInfoContent);

            hci.GetComponentInChildren<TextMeshProUGUI>().text = $"Hoop is worth {hoop.Score} point" + (hoop.Score != 1 ? "s" : "");

            hci.GetComponentInChildren<Image>().color = ColorUtility.TryParseHtmlString(hoop.Color, out var parsedColor)
                ? parsedColor
                : Color.white;

            foreach (var child in h.GetComponentsInChildren<Renderer>())
            {
                child.material.color = ColorUtility.TryParseHtmlString(hoop.Color, out var parsedColorHoop)
                    ? parsedColorHoop
                    : Color.white;
            }
        }
    }
}