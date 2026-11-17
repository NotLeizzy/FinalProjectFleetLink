using System.ComponentModel;

namespace FinalProjectFleetLink.Helpers;

public abstract class ReadOnlyUserControl : UserControl
{
    private readonly List<Control> mutationControls = [];
    private bool isReadOnly;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsReadOnly
    {
        get => isReadOnly;
        set
        {
            if (isReadOnly == value)
            {
                return;
            }
            isReadOnly = value;
            ApplyReadOnlyState();
        }
    }

    protected void RegisterMutationControl(params Control[] controls)
    {
        foreach (var control in controls)
        {
            if (!mutationControls.Contains(control))
            {
                mutationControls.Add(control);
            }
        }
        ApplyReadOnlyState();
    }

    private void ApplyReadOnlyState()
    {
        foreach (var control in mutationControls)
        {
            control.Enabled = !IsReadOnly;
        }
    }
}
