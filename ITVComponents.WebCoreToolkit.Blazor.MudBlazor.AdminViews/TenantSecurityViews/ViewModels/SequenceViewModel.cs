using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Blazor.MudBlazor.AdminViews.TenantSecurityViews.ViewModels;

public sealed class SequenceViewModel
{
    public int SequenceId { get; set; }

    public int TenantId { get; set; }

    [Required, MaxLength(300)]
    public string SequenceName { get; set; } = string.Empty;

    public int MinValue { get; set; } = -1;
    public int MaxValue { get; set; } = int.MaxValue;
    public bool Cycle { get; set; }
    public int StepSize { get; set; } = 1;
    public int CurrentValue { get; set; }
}
