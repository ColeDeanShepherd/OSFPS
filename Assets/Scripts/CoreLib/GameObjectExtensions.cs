using UnityEngine;

public static class GameObjectExtensions
{
    public static GameObject FindObjectOrAncestorWithTag(this GameObject gameObject, string tag)
    {
        if (gameObject.tag == tag) return gameObject;

        var parentTransform = gameObject.transform.parent;
        return (parentTransform != null) ? parentTransform.gameObject.FindObjectOrAncestorWithTag(tag) : null;
    }

    public static GameObject FindDescendant(this GameObject gameObject, string descendantName)
    {
        var descendantTransform = FindDescendant(gameObject.transform, descendantName);
        return (descendantTransform != null) ? descendantTransform.gameObject : null;
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
}