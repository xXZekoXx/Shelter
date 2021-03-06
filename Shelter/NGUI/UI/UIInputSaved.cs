using UnityEngine;

[AddComponentMenu("NGUI/UI/Input (Saved)")]
// ReSharper disable once CheckNamespace
public class UIInputSaved : UIInput
{
    public string playerPrefsField;

    private void Awake()
    {
        onSubmit = new OnSubmit(this.SaveToPlayerPrefs);
        if (!string.IsNullOrEmpty(this.playerPrefsField) && PlayerPrefs.HasKey(this.playerPrefsField))
        {
            this.text = PlayerPrefs.GetString(this.playerPrefsField);
        }
    }

    private void OnApplicationQuit()
    {
        this.SaveToPlayerPrefs(this.text);
    }

    private void SaveToPlayerPrefs(string val)
    {
        if (!string.IsNullOrEmpty(this.playerPrefsField))
        {
            PlayerPrefs.SetString(this.playerPrefsField, val);
        }
    }

    public override string text
    {
        get
        {
            return base.text;
        }
        set
        {
            base.text = value;
            this.SaveToPlayerPrefs(value);
        }
    }
}

