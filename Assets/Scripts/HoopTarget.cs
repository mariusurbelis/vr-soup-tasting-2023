using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HoopTarget : MonoBehaviour
{
    public int id = -1;
    public int hoopScore = 0;

    private readonly HashSet<Collider> _colliders = new();
    
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
            Debug.Log("Score!");
            CloudServices.CallScoreFunction(id, hoopScore);
        }

        _colliders.Remove(other);
    }
}