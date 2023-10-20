using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SphereSpawner : MonoBehaviour
{
    public GameObject spherePrefab;
    public float timer = 0;
    public TextMeshProUGUI spawnTimerDisplay;

    private readonly HashSet<Collider> _colliders = new();

    private List<GameObject> _spheres = new();

    private HashSet<Collider> GetColliders()
    {
        return _colliders;
    }

    private void Update()
    {
        if (!GameManager.InGame) {
            HandleEndGame();
            return;
        }

        timer = GetColliders().Count == 0 ? timer - Time.deltaTime : ConfigValues.SpawnDelay;

        if (timer < 0)
        {
            SpawnSphere();
            timer = ConfigValues.SpawnDelay;
        }

        // Debug log timer to 2 fixed positions after the decimal
        spawnTimerDisplay.text = $"Spawn Timer: {timer.ToString("F2")}s";
        
        for (var i = _spheres.Count - 1; i >= 0; i--)
        {
            if (_spheres[i].transform.position.y < -100)
            {
                Destroy(_spheres[i]);
                _spheres.RemoveAt(i);
            }
        }
    }

    private void HandleEndGame()
    {
        foreach (var sphere in _colliders)
        {
            Destroy(sphere);
        }
        _colliders.Clear();

        timer = 0;

        gameObject.SetActive(false);
    }

    private void SpawnSphere()
    {
        var s = Instantiate(spherePrefab, transform.position, Quaternion.identity);
        _spheres.Add(s);
    }

    private void OnTriggerEnter(Collider other) => _colliders.Add(other);
    private void OnTriggerExit(Collider other) => _colliders.Remove(other);
}