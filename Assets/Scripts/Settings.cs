using UnityEngine;

[System.Serializable]
public class Settings
{
    public static Settings LoadFromFile(string filePath)
    {
        if (System.IO.File.Exists(filePath))
        {
            var settingsJsonString = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            return JsonUtility.FromJson<Settings>(settingsJsonString);
        }
        else
        {
            return new Settings();
        }
    }
    public static void SaveSettings(Settings settings, string filePath)
    {
        System.IO.File.WriteAllText(filePath, JsonUtility.ToJson(settings));
    }

    public string PlayerName = "Player";
    public float MouseSensitivity = 3;
    public float FieldOfViewY = 60;
    public float Volume = 1;
}