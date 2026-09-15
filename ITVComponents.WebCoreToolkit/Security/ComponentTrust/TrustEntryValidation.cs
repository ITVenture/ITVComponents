using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ITVComponents.WebCoreToolkit.Security.ComponentTrust
{
    /// <summary>
    /// Der Befund zu EINEM Typnamen aus einem Vertrauens-Eintrag.
    /// </summary>
    public sealed class TrustTypeDiagnostic
    {
        /// <summary>
        /// Der Name, wie er in der Datenbankzeile steht.
        /// </summary>
        public string StoredName { get; init; }

        /// <summary>
        /// Ob sich daraus ueberhaupt ein Typ laden liess.
        /// </summary>
        public bool Resolvable { get; init; }

        /// <summary>
        /// Ob der gespeicherte Name <b>zeichengenau</b> dem entspricht, was der geladene Typ zur Laufzeit als
        /// <see cref="Type.AssemblyQualifiedName"/> meldet.
        /// </summary>
        /// <remarks>
        /// Das ist der Punkt, auf den es ankommt: das Nachschlagen im Provider vergleicht Zeichenketten. Ein
        /// Typ kann sich laden lassen (die Bindung ist versionstolerant) und der Eintrag trotzdem nie greifen.
        /// </remarks>
        public bool ExactMatch { get; init; }

        /// <summary>
        /// Der Name, den der Typ tatsaechlich fuehrt - gesetzt, wenn er vom gespeicherten abweicht.
        /// </summary>
        public string ActualName { get; init; }

        /// <summary>
        /// Im Klartext, was nicht stimmt. <c>null</c>, wenn nichts zu beanstanden ist.
        /// </summary>
        public string Hint { get; init; }

        /// <summary>
        /// True, wenn dieser Name zur Laufzeit treffen wird.
        /// </summary>
        public bool Ok => Resolvable && ExactMatch;

        public override string ToString() => Ok ? StoredName : $"{StoredName} -- {Hint}";
    }

    /// <summary>
    /// Der Befund zu einer Zeile der Vertrauens-Tabelle.
    /// </summary>
    public sealed class TrustEntryDiagnostic
    {
        /// <summary>
        /// Der Schluessel der Zeile, damit man sie in der Maske wiederfindet.
        /// </summary>
        public int EntryId { get; init; }

        /// <summary>
        /// Die Beschreibung aus der Zeile.
        /// </summary>
        public string Description { get; init; }

        /// <summary>
        /// Der Typ, dem vertraut wird (der Aufrufer).
        /// </summary>
        public TrustTypeDiagnostic TrustedType { get; init; }

        /// <summary>
        /// Der Typ, bei dem ihm vertraut wird (das Ziel).
        /// </summary>
        public TrustTypeDiagnostic TrustingType { get; init; }

        /// <summary>
        /// True, wenn beide Namen zur Laufzeit treffen.
        /// </summary>
        public bool Ok => TrustedType is { Ok: true } && TrustingType is { Ok: true };

        public override string ToString()
        {
            if (Ok)
            {
                return $"#{EntryId} ok";
            }

            var parts = new List<string>();
            if (TrustedType is { Ok: false })
            {
                parts.Add($"trusted type: {TrustedType.Hint}");
            }

            if (TrustingType is { Ok: false })
            {
                parts.Add($"target type: {TrustingType.Hint}");
            }

            return $"#{EntryId} ({Description}): {string.Join(" | ", parts)}";
        }
    }

    /// <summary>
    /// Das Ergebnis von <see cref="ISecurityAccessProvider.ValidateTrustEntries"/>.
    /// </summary>
    /// <remarks>
    /// Gedacht fuer einen Start-Check oder die Diagnose-Maske: ein Vertrauens-Eintrag, der nicht mehr
    /// aufloest, bricht <b>nichts Sichtbares</b> - der Build ist gruen, die Migration laeuft, die Anmeldung
    /// gelingt, und erst danach fehlen Daten. Diese Pruefung zieht den Befund an den Anfang.
    /// </remarks>
    public sealed class TrustEntryValidationResult
    {
        /// <summary>
        /// Das Ergebnis eines Providers, der die Pruefung nicht anbietet.
        /// </summary>
        public static TrustEntryValidationResult NotSupported { get; } =
            new(false, Array.Empty<TrustEntryDiagnostic>());

        public TrustEntryValidationResult(bool supported, IReadOnlyList<TrustEntryDiagnostic> entries)
        {
            Supported = supported;
            Entries = entries ?? Array.Empty<TrustEntryDiagnostic>();
        }

        /// <summary>
        /// False, wenn der Provider gar nicht pruefen kann - dann sagt <see cref="Entries"/> nichts aus.
        /// </summary>
        public bool Supported { get; }

        /// <summary>
        /// Alle geprueften Zeilen, in der Reihenfolge der Tabelle.
        /// </summary>
        public IReadOnlyList<TrustEntryDiagnostic> Entries { get; }

        /// <summary>
        /// Die Zeilen, die zur Laufzeit nicht treffen werden.
        /// </summary>
        public IEnumerable<TrustEntryDiagnostic> Broken => Entries.Where(n => !n.Ok);

        /// <summary>
        /// True, wenn geprueft wurde und jede Zeile trifft.
        /// </summary>
        public bool AllValid => Supported && Entries.All(n => n.Ok);

        /// <summary>
        /// Der Befund als mehrzeiliger Text - so, wie man ihn beim Hochfahren protokollieren will.
        /// </summary>
        public string Describe()
        {
            if (!Supported)
            {
                return "The configured ISecurityAccessProvider does not support validating trust entries.";
            }

            var broken = Broken.ToList();
            var sb = new StringBuilder();
            sb.Append($"{Entries.Count} trust entries checked, {broken.Count} of them will never match at runtime.");
            foreach (var entry in broken)
            {
                sb.AppendLine();
                sb.Append("  ").Append(entry);
            }

            return sb.ToString();
        }
    }
}
