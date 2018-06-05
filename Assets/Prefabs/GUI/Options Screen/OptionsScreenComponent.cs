using UnityEngine;
using UnityEngine.UI;

public class OptionsScreenComponent : MonoBehaviour
{
    public Slider MouseSensitivitySlider;
    public Slider VerticalFovSlider;

    public void OnOkClick()
    {
        OsFps.Instance.Settings.MouseSensitivity = MouseSensitivitySlider.value;
        OsFps.Instance.Settings.FieldOfViewY = VerticalFovSlider.value;

        OsFps.Instance.SaveSettings();
        OsFps.Instance.PopMenu();
    }

    private void Start()
    {
        MouseSensitivitySlider.value = OsFps.Instance.Settings.MouseSensitivity;
        VerticalFovSlider.value = OsFps.Instance.Settings.FieldOfViewY;
    }
}