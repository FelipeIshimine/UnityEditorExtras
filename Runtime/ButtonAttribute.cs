using System;

public enum ButtonGroupLayout
{
    None,
    Vertical,
    Horizontal
}

[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public sealed class ButtonAttribute : Attribute
{
    public string Label;
    public string GroupName;
    public ButtonGroupLayout Layout;
    public string Validation;

    public ButtonAttribute(
        string label,
        ButtonGroupLayout layout = ButtonGroupLayout.None,
        string groupName = null,
        string validation = null)
    {
        Label = label;
        GroupName = groupName;
        Layout = layout;
        Validation = validation;
    }
    public ButtonAttribute(
        ButtonGroupLayout layout = ButtonGroupLayout.None,
        string groupName = null,
        string validation = null)
    {
        Label = null;
        GroupName = groupName;
        Layout = layout;
        Validation = validation;
    }

}