using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoxTarget : MonoBehaviour
{
    public static int Score
    {
        get => _instance._colliders.Count;
    }
    
    private readonly HashSet<Collider> _colliders = new();

    private static BoxTarget _instance;
    
    private HashSet<Collider> GetColliders()
    {
        return _colliders;
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    private void OnTriggerEnter(Collider other) => _colliders.Add(other);
    private void OnTriggerExit(Collider other) => _colliders.Remove(other);
}
