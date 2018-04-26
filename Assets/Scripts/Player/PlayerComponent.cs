using UnityEngine;

public class PlayerComponent : MonoBehaviour
{
    public uint Id;
    public Rigidbody Rigidbody;
    public GameObject CameraPointObject;
    public GameObject HandsPointObject;
    
    private void Awake()
    {
        Rigidbody = GetComponent<Rigidbody>();
        CameraPointObject = transform.Find("CameraPoint").gameObject;
        HandsPointObject = gameObject.FindDescendant("HandsPoint");
    }
}