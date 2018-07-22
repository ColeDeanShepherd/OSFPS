using UnityEngine;

public class SniperRifleBulletTrailComponent : MonoBehaviour
{
    public float DriftSpeed = 0.25f;

    private void Update()
    {
        var driftDirection = -transform.right;
        var driftVelocity = DriftSpeed * driftDirection;

        transform.position += (Time.deltaTime * driftVelocity);
    }
}