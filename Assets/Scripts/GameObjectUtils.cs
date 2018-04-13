using UnityEngine;

public static class GameObjectUtils
{
    public static GameObject GetObjectOrAncestorWithTag(GameObject gameObject, string tag)
    {
        if (gameObject.tag == tag) return gameObject;

        var parentTransform = gameObject.transform.parent;
        return (parentTransform != null) ? GetObjectOrAncestorWithTag(parentTransform.gameObject, tag) : null;
    }
}