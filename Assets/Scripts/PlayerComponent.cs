using UnityEngine;

public class PlayerComponent : MonoBehaviour
{
    public uint Id;
    public Rigidbody Rigidbody;
    public GameObject CameraPointObject;
    
    private void Awake()
    {
        Rigidbody = GetComponent<Rigidbody>();
        CameraPointObject = transform.Find("CameraPoint").gameObject;
    }
}