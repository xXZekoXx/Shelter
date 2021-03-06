using System;
using JetBrains.Annotations;
using UnityEngine;

[AddComponentMenu("NGUI/Interaction/Checkbox Controlled Component")]
// ReSharper disable once CheckNamespace
public class UICheckboxControlledComponent : MonoBehaviour
{
    public bool inverse;
    private bool mUsingDelegates;
    public MonoBehaviour target;

    [UsedImplicitly]
    private void OnActivate(bool isActive)
    {
        if (!this.mUsingDelegates)
        {
            this.OnActivateDelegate(isActive);
        }
    }

    private void OnActivateDelegate(bool isActive)
    {
        if (enabled && this.target != null)
        {
            this.target.enabled = !this.inverse ? isActive : !isActive;
        }
    }

    private void Start()
    {
        UICheckbox component = GetComponent<UICheckbox>();
        if (component != null)
        {
            this.mUsingDelegates = true;
            component.onStateChange = (UICheckbox.OnStateChange) Delegate.Combine(component.onStateChange, new UICheckbox.OnStateChange(this.OnActivateDelegate));
        }
    }
}

