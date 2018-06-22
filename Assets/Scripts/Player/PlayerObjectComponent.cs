using System.Collections.Generic;
using UnityEngine;

public class PlayerObjectComponent : MonoBehaviour
{
    public PlayerObjectState State;

    public List<PlayerLagCompensationSnapshot> LagCompensationSnapshots;

    public Rigidbody Rigidbody;
    public GameObject CameraPointObject;
    public GameObject HandsPointObject;
    
    private void Awake()
    {
        LagCompensationSnapshots = new List<PlayerLagCompensationSnapshot>();

        Rigidbody = GetComponent<Rigidbody>();
        CameraPointObject = transform.Find("CameraPoint").gameObject;
        HandsPointObject = gameObject.FindDescendant("HandsPoint");
    }
    private void LateUpdate()
    {
        State.Position = transform.position;
        State.Velocity = Rigidbody.velocity;
    }
}