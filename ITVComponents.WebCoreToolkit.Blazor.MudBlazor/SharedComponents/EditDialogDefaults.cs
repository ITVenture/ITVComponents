using MudBlazor;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents;

/// <summary>
/// Shared <see cref="DialogOptions"/> presets for Mud-based admin dialogs across the
/// Toolkit's Blazor libraries (TSV, ANCT*UserView, TSCUserView, ...).
/// </summary>
public static class EditDialogDefaults
{
    /// <summary>Edit/create dialogs (single-row CRUD): medium width, full-width within.</summary>
    public static DialogOptions Edit => new()
    {
        MaxWidth = MaxWidth.Medium,
        FullWidth = true,
        CloseOnEscapeKey = true
    };

    /// <summary>Detail dialogs that host tabbed sub-grids: extra-large width.</summary>
    public static DialogOptions Detail => new()
    {
        MaxWidth = MaxWidth.ExtraLarge,
        FullWidth = true,
        CloseOnEscapeKey = true
    };
}
