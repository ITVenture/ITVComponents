using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ITVComponents.Helpers;
using ITVComponents.WebCoreToolkit.Extensions;

namespace ITVComponents.WebCoreToolkit.Models
{
    public sealed class NavigationMenu
    {
        private string metadata;
        private IReadOnlyDictionary<string, string> metadataValues;

        public string DisplayName { get; set; }

        public string Url { get; set; }

        public string SpanClass { get; set; }

        public int SortOrder { get; set; }

        public string RequiredPermission { get;set; }

        public string RequiredFeature { get; set; }

        public List<NavigationMenu> Children { get;} = new List<NavigationMenu>();

        public bool IsValid => !string.IsNullOrEmpty(Url) || Children.Any(n => n.IsValid);

        public string CounterVal { get; set; }

        public bool Active { get; private set; }

        /// <summary>
        /// Raw per-entry metadata as stored on the navigation menu (a JSON object, or null/empty). Use
        /// <see cref="MetadataValues"/> for the parsed key/value view.
        /// </summary>
        public string Metadata
        {
            get => metadata;
            set
            {
                metadata = value;
                metadataValues = null;
            }
        }

        /// <summary>
        /// The parsed <see cref="Metadata"/> as a flat, case-insensitive key → string map. Empty when there is
        /// no metadata or the JSON cannot be parsed (never null). Non-string JSON values are rendered to their
        /// text form. Lazily parsed and cached.
        /// </summary>
        public IReadOnlyDictionary<string, string> MetadataValues => metadataValues ??= ParseMetadata(metadata);

        private static IReadOnlyDictionary<string, string> ParseMetadata(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(json))
            {
                return result;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return result;
                }

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    result[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString()
                        : prop.Value.GetRawText();
                }
            }
            catch (JsonException)
            {
                // Malformed metadata must never break navigation — treat as "no metadata".
            }

            return result;
        }

        public void CleanUp(string currentPath, string jsonLanguageRecord)
        {
            var invalids = Children.Where(n => !n.IsValid).ToArray();
            foreach (var inv in invalids)
            {
                Children.Remove(inv);
            }

            Active = Url?.Equals(currentPath, StringComparison.OrdinalIgnoreCase) ?? false;
            Children.ForEach(n => n.CleanUp(currentPath, jsonLanguageRecord));
            if (!Active)
            {
                Active = Children.Any(c => c.Active);
            }

            if (string.IsNullOrEmpty(CounterVal))
            {
                if (Children.Any(c => !string.IsNullOrEmpty(c.CounterVal) && !c.Active))
                {
                    CounterVal = "...";
                }
            }

            DisplayName = DisplayName.Translate(jsonLanguageRecord);
        }
    }
}
