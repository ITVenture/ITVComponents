using System;
using ITVComponents.WebCoreToolkit.Options;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.Blazor.Localization
{
    /// <summary>
    /// Drop-in replacement for Blazor's built-in <c>DataAnnotationsValidator</c> that resolves
    /// topic-prefixed validation messages (e.g. <c>"ITV:RequiredAttribute_ValidationError"</c>)
    /// through the Toolkit's <see cref="AttributeTranslationOptions"/>-based pipeline. Place
    /// inside an <c>&lt;EditForm&gt;</c>, same usage as the standard validator.
    ///
    /// Requires <see cref="ITVComponents.WebCoreToolkit.Extensions.DependencyExtensions.ConfigureAttributeTranslation"/>
    /// and <see cref="Extensions.DependencyExtensions.UseBlazorAttributeMessages"/> to be wired in
    /// startup.
    /// </summary>
    public sealed class ToolkitDataAnnotationsValidator : ComponentBase, IDisposable
    {
        [CascadingParameter]
        private EditContext? CurrentEditContext { get; set; }

        [Inject]
        private IAttributeMessageLocalizer LocalizerSource { get; set; } = default!;

        [Inject]
        private AttributeTranslationOptions Options { get; set; } = default!;

        private EditContext? subscribed;
        private ValidationMessageStore? messageStore;
        private IStringLocalizer? scopedLocalizer;

        protected override void OnInitialized()
        {
            if (CurrentEditContext is null)
            {
                throw new InvalidOperationException(
                    $"{nameof(ToolkitDataAnnotationsValidator)} requires a cascading {nameof(EditContext)}. " +
                    $"Place it inside an <EditForm>.");
            }

            subscribed = CurrentEditContext;
            messageStore = new ValidationMessageStore(subscribed);
            scopedLocalizer = LocalizerSource.GetLocalizerFor(subscribed.Model.GetType());

            subscribed.OnValidationRequested += HandleValidationRequested;
            subscribed.OnFieldChanged += HandleFieldChanged;
        }

        private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
        {
            messageStore!.Clear();
            ToolkitAttributeValidator.ValidateModel(
                subscribed!.Model, subscribed, messageStore, scopedLocalizer!, Options);
            subscribed.NotifyValidationStateChanged();
        }

        private void HandleFieldChanged(object? sender, FieldChangedEventArgs e)
        {
            messageStore!.Clear(e.FieldIdentifier);
            ToolkitAttributeValidator.ValidateField(
                subscribed!.Model, e.FieldIdentifier, messageStore, scopedLocalizer!, Options);
            subscribed.NotifyValidationStateChanged();
        }

        public void Dispose()
        {
            if (subscribed is not null)
            {
                subscribed.OnValidationRequested -= HandleValidationRequested;
                subscribed.OnFieldChanged -= HandleFieldChanged;
            }
        }
    }
}
