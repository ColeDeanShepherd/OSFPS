using UnityEngine;
using UnityEngine.UI;

public class HealthBarComponent : MonoBehaviour
{
    public GameObject HealthObject;

    public float HealthPercent
    {
        get
        {
            return _healthPercent;
        }
        set
        {
            _healthPercent = value;
            UpdateHealthWidth(_healthPercent);
        }
    }
    public Color Color
    {
        get
        {
            return HealthObject.GetComponent<Image>().color;
        }
        set
        {
            foreach (var transform in transform.ThisAndDescendantsDepthFirst())
            {
                var image = transform.gameObject.GetComponent<Image>();
                if (image != null)
                {
                    image.color = value;
                }
            }
        }
    }

    private float _healthPercent;

    private void Awake()
    {
        HealthPercent = 1;
    }
    private void UpdateHealthWidth(float healthPercent)
    {
        var rectTransform = HealthObject.GetComponent<RectTransform>();
        var newAnchorMax = rectTransform.anchorMax;
        newAnchorMax.x = healthPercent;
        rectTransform.anchorMax = newAnchorMax;
    }
}