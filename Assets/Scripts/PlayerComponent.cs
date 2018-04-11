using UnityEngine;

public class PlayerComponent : MonoBehaviour
{
    public PlayerState State;
    public PlayerInput CurrentInput;
    public GameObject cameraPointObject;

    public bool IsMe
    {
        get
        {
            var client = OsFps.Instance.Client;
            return (client != null) && (State.Id == client.PlayerId);
        }
    }

    private new Rigidbody rigidbody;

    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
        cameraPointObject = transform.Find("CameraPoint").gameObject;
    }
    private void Update()
    {
        if(IsMe)
        {
            CurrentInput = GetCurrentPlayerInput();
        }

        transform.localEulerAngles = new Vector3(0, CurrentInput.YAngle, 0);
        cameraPointObject.transform.localEulerAngles = new Vector3(CurrentInput.XAngle, 0, 0);

        var relativeMoveDirection = GetRelativeMoveDirection();
        rigidbody.AddRelativeForce(10 * relativeMoveDirection);
    }

    public PlayerInput GetCurrentPlayerInput()
    {
        var mouseSensitivity = 3;
        var deltaMouse = mouseSensitivity * new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

        var XAngle = Mathf.Clamp(CurrentInput.XAngle - deltaMouse.y, -90, 90);
        var YAngle = Mathf.Repeat(CurrentInput.YAngle + deltaMouse.x, 360);

        return new PlayerInput
        {
            IsMoveFowardPressed = Input.GetKey(KeyCode.W),
            IsMoveBackwardPressed = Input.GetKey(KeyCode.S),
            IsMoveRightPressed = Input.GetKey(KeyCode.D),
            IsMoveLeftPressed = Input.GetKey(KeyCode.A),
            XAngle = XAngle,
            YAngle = YAngle
        };
    }
    private Vector3 GetRelativeMoveDirection()
    {
        var moveDirection = Vector3.zero;

        if (CurrentInput.IsMoveFowardPressed)
        {
            moveDirection += Vector3.forward;
        }

        if (CurrentInput.IsMoveBackwardPressed)
        {
            moveDirection += Vector3.back;
        }

        if (CurrentInput.IsMoveRightPressed)
        {
            moveDirection += Vector3.right;
        }

        if (CurrentInput.IsMoveLeftPressed)
        {
            moveDirection += Vector3.left;
        }

        return moveDirection.normalized;
    }
}