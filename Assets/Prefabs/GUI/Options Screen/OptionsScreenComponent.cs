using UnityEngine;
using UnityEngine.UI;

public class OptionsScreenComponent : MonoBehaviour
{
    public InputField PlayerNameInputField;
    public Slider MouseSensitivitySlider;
    public Slider VerticalFovSlider;
    public Slider VolumeSlider;

    private void InitializeFromSettings()
    {
        PlayerNameInputField.text = OsFps.Instance.Settings.PlayerName;
        MouseSensitivitySlider.value = OsFps.Instance.Settings.MouseSensitivity;
        VerticalFovSlider.value = OsFps.Instance.Settings.FieldOfViewY;
        VolumeSlider.value = OsFps.Instance.Settings.Volume;
    }
    private void UpdateSettingsFromUi()
    {
        OsFps.Instance.Settings.PlayerName = PlayerNameInputField.text;
        OsFps.Instance.Settings.MouseSensitivity = MouseSensitivitySlider.value;
        OsFps.Instance.Settings.FieldOfViewY = VerticalFovSlider.value;
        OsFps.Instance.Settings.Volume = VolumeSlider.value;
    }

    public void OnOkClick()
    {
        UpdateSettingsFromUi();
        OsFps.Instance.SaveSettings();
        OsFps.Instance.PopMenu();
    }

    public void OnResetClick()
    {
        OsFps.Instance.Settings = new Settings();
        InitializeFromSettings();
    }

    private void Start()
    {
        InitializeFromSettings();
    }
}