using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class HoopTarget : MonoBehaviour
{
    public int id = -1;
    public int hoopScore = 0;

    private ParticleSystem[] _particleSystems;

    private readonly HashSet<Collider> _colliders = new();

    private void Start()
    {
        _particleSystems = transform.parent.GetComponentsInChildren<ParticleSystem>();
    }

    private void Update()
    {
        if (Keyboard.current.sKey.wasReleasedThisFrame)
        {
            CloudServices.CallScoreFunction(id, hoopScore);
        }
    }

    private HashSet<Collider> GetColliders()
    {
        return _colliders;
    }

    private void OnTriggerEnter(Collider other) => _colliders.Add(other);

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Sphere"))
        {
            // Sphere entered and left the trigger a.k.a score happened

            if (GameManager.InGame)
            {
                Debug.Log("Score!");

                foreach (var ps in _particleSystems)
                {
                    ps.Play();
                }

                CloudServices.CallScoreFunction(id, hoopScore);
            }

            other.gameObject.tag = "Untagged";
        }

        _colliders.Remove(other);
    }
}