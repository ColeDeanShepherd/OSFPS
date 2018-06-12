﻿using UnityEngine;

public class RocketComponent : MonoBehaviour
{
    public RocketState State;

    public Rigidbody Rigidbody;
    public Collider Collider;

    private void Awake()
    {
        Rigidbody = GetComponent<Rigidbody>();
        Collider = GetComponent<Collider>();
    }
    private void Start()
    {
        Destroy(gameObject, OsFps.MaxRocketLifetime);
    }
    private void OnCollisionEnter(Collision collision)
    {
        OsFps.Instance.RocketOnCollisionEnter(this, collision);
    }
}