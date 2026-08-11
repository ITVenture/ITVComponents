using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace ITVComponents.WebCoreToolkit.Blazor.SharedComponents.Widgets
{
    /// <summary>
    /// The registry <b>key -&gt; renderer</b> for dashboard tiles. The host fills it at startup, a widget
    /// names only the key.
    /// </summary>
    /// <remarks>
    /// Dieselbe Form wie <c>CustomCompanyInfoViewConfiguration</c> bei den Zusatzangaben-Masken: ein
    /// Schluessel kann nur auf etwas zeigen, das der Host selbst registriert hat. Der Konfigurationsweg
    /// (WebPart-Abschnitt <c>WidgetRenderers</c>) fuellt dieselbe Registrierung ueber
    /// <see cref="RegisterRenderer(Type, string, string, string, IReadOnlyList{DeclaredField}, Func{string, IReadOnlyDictionary{string, string}, string})"/>.
    /// </remarks>
    public class WidgetRendererConfiguration
    {
        private readonly Dictionary<string, WidgetRendererDescriptor> renderers =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>All registered renderers, in registration order.</summary>
        public IReadOnlyCollection<WidgetRendererDescriptor> Descriptors => renderers.Values;

        /// <summary>
        /// Registers a renderer. Key and labels come from its <see cref="WidgetRendererAttribute"/>; every
        /// argument given here overrides the attribute.
        /// </summary>
        /// <typeparam name="T">
        /// die Komponente. <c>IComponent</c> UND <c>IWidgetRenderer</c> werden schon beim UEBERSETZEN
        /// verlangt - was hier durchkommt, kann die Flaeche auch tatsaechlich zeichnen.
        /// </typeparam>
        public WidgetRendererConfiguration RegisterRenderer<T>(
            string? key = null,
            string? displayName = null,
            string? editorLanguage = null,
            IReadOnlyList<DeclaredField>? options = null,
            Func<string?, IReadOnlyDictionary<string, string?>, string?>? validate = null)
            where T : IComponent, IWidgetRenderer
            => RegisterRenderer(typeof(T), key, displayName, editorLanguage, options, validate);

        /// <summary>
        /// Registers a renderer by type - the way in for the configuration path, where the type is a string
        /// and the compiler cannot check anything.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// der Typ ist keine Komponente, erfuellt den Vertrag nicht, oder es ist kein Schluessel zu
        /// ermitteln. Das ist ein Startfehler und keiner, der beim Zeichnen einer Kachel auffallen darf.
        /// </exception>
        /// <exception cref="InvalidOperationException">der Schluessel ist bereits vergeben.</exception>
        public WidgetRendererConfiguration RegisterRenderer(
            Type componentType,
            string? key = null,
            string? displayName = null,
            string? editorLanguage = null,
            IReadOnlyList<DeclaredField>? options = null,
            Func<string?, IReadOnlyDictionary<string, string?>, string?>? validate = null)
        {
            ArgumentNullException.ThrowIfNull(componentType);

            if (!typeof(IComponent).IsAssignableFrom(componentType))
            {
                throw new ArgumentException(
                    $"'{componentType.FullName}' is not a Blazor component and cannot render a widget.",
                    nameof(componentType));
            }

            if (!typeof(IWidgetRenderer).IsAssignableFrom(componentType))
            {
                throw new ArgumentException(
                    $"'{componentType.FullName}' does not implement {nameof(IWidgetRenderer)}.",
                    nameof(componentType));
            }

            var attribute = componentType.GetCustomAttribute<WidgetRendererAttribute>();
            // Ein leerer Schluessel ist gueltig (der Renderer fuer Widgets ohne Angabe) - deshalb wird auf
            // "gar nichts angegeben" geprueft und nicht auf "leer".
            string? effectiveKey = key ?? attribute?.Key;
            if (effectiveKey is null)
            {
                throw new ArgumentException(
                    $"'{componentType.FullName}' has no {nameof(WidgetRendererAttribute)} and no key was given.",
                    nameof(key));
            }

            if (renderers.ContainsKey(effectiveKey))
            {
                throw new InvalidOperationException(
                    $"A widget renderer for the key '{effectiveKey}' is already registered " +
                    $"({renderers[effectiveKey].ComponentType.FullName}).");
            }

            renderers[effectiveKey] = new WidgetRendererDescriptor
            {
                Key = effectiveKey,
                ComponentType = componentType,
                DisplayName = displayName
                              ?? attribute?.DisplayName
                              ?? (effectiveKey.Length != 0 ? effectiveKey : componentType.Name),
                EditorLanguage = editorLanguage ?? attribute?.EditorLanguage ?? "html",
                Options = options ?? Array.Empty<DeclaredField>(),
                Validate = validate
            };

            return this;
        }

        /// <summary>Looks up a renderer. A null or empty key asks for the default renderer.</summary>
        /// <returns>the descriptor, or null when nothing is registered under that key</returns>
        public WidgetRendererDescriptor? Get(string? key)
        {
            renderers.TryGetValue(key ?? string.Empty, out WidgetRendererDescriptor? descriptor);
            return descriptor;
        }

        /// <summary>The registered keys, for a message that says what would have been available.</summary>
        public IReadOnlyList<string> KnownKeys
            => renderers.Keys.Select(k => k.Length != 0 ? k : "(default)").ToArray();
    }
}
