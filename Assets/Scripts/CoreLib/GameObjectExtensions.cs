using System.Collections.Generic;
using UnityEngine;

public static class GameObjectExtensions
{
    public static GameObject FindObjectOrAncestorWithTag(this GameObject gameObject, string tag)
    {
        if (gameObject.tag == tag) return gameObject;

        var parentTransform = gameObject.transform.parent;
        return parentTransform?.gameObject.FindObjectOrAncestorWithTag(tag);
    }
    public static ComponentType FindComponentInObjectOrAncestor<ComponentType>(this GameObject gameObject) where ComponentType : class
    {
        var component = gameObject.GetComponent<ComponentType>();
        if (component != null) return component;

        var parentTransform = gameObject.transform.parent;
        return parentTransform?.gameObject.FindComponentInObjectOrAncestor<ComponentType>();
    }

    public static GameObject FindDescendant(this GameObject gameObject, string descendantName)
    {
        var descendantTransform = FindDescendant(gameObject.transform, descendantName);
        return descendantTransform?.gameObject;
    }
    public static Transform FindDescendant(this Transform transform, string descendantName)
    {
        var descendantTransform = transform.Find(descendantName);
        if (descendantTransform != null) return descendantTransform;

        foreach (Transform childTransform in transform)
        {
            descendantTransform = FindDescendant(childTransform, descendantName);
            if (descendantTransform != null) return descendantTransform;
        }

        return null;
    }
    public static IEnumerable<Transform> ThisAndDescendantsDepthFirst(this Transform transform)
    {
        yield return transform;

        foreach (Transform child in transform)
        {
            foreach (var descendant in child.ThisAndDescendantsDepthFirst())
            {
                yield return descendant;
            }
        }
    }

    public static Vector3 GetHorizontalVelocity(Rigidbody rigidbody)
    {
        var velocity = rigidbody.velocity;
        return new Vector3(velocity.x, 0, velocity.z);
    }

    public static void TwoBoneIk(
        Transform bone1Transform, float bone1Length, Transform bone2Transform, float bone2Length,
        Vector3 targetPosition, Vector3 elbowTargetPosition, bool getPositiveAngleSolution
    )
    {
        // Get thetas.
        var targetOffsetFromBone1 = targetPosition - bone1Transform.position;
        var targetDistanceFromBone1 = targetOffsetFromBone1.magnitude;
        float theta1InRadians, theta2InRadians;
        MathfExtensions.GetTwoBoneIkAngles(
            bone1Length, bone2Length, targetDistanceFromBone1, getPositiveAngleSolution,
            out theta1InRadians, out theta2InRadians
        );

        // Rotate bone1 to point to the target, and point the up vector towards the elbow target.
        var elbowTargetOffsetFromBone1 = elbowTargetPosition - bone1Transform.position;
        var lookAtUpDirection = Vector3Extensions.Reject(elbowTargetOffsetFromBone1, targetOffsetFromBone1);
        bone1Transform.LookAt(targetPosition, lookAtUpDirection);

        // Apply thetas.
        bone1Transform.Rotate(-Vector3.right, Mathf.Rad2Deg * theta1InRadians, Space.Self);
        bone2Transform.localRotation = Quaternion.AngleAxis(Mathf.Rad2Deg * theta2InRadians, -Vector3.right);
    }
}