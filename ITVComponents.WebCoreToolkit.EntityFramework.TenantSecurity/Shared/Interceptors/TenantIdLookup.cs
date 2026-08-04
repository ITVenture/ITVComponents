using System;
using System.Linq;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Interceptors
{
    /// <summary>
    /// Baut die Nachschlage-Funktion <b>Mandantenname -&gt; Id</b> fuer eine beliebige Mandanten-Entitaet.
    /// </summary>
    /// <remarks>
    /// Das ist die einzige Stelle, an der ein Verbraucher dieses Vertrags die konkrete Entitaet
    /// ueberhaupt braucht - deshalb steht die Auswahl hier und nicht im Interceptor. Wer eine eigene
    /// Mandanten-Entitaet mitbringt, kommt ueber <see cref="For{TTenant}"/> hinein, ohne dass die
    /// Bibliothek sie kennen muesste.
    /// </remarks>
    public static class TenantIdLookup
    {
        /// <summary>
        /// Liefert eine Funktion, die den Mandanten mit diesem Namen in der angegebenen Entitaet sucht
        /// und seine Id zurueckgibt.
        /// </summary>
        /// <typeparam name="TTenant">die Mandanten-Entitaet dieses Kontexts</typeparam>
        /// <remarks>
        /// Die Abfrage geht bewusst ueber <see cref="EF.Property{TProperty}"/> statt ueber die
        /// Schnittstellen-Eigenschaft: ein Zugriff auf ein Interface-Mitglied eines generischen
        /// Parameters ist fuer den Abfrage-Uebersetzer nicht in jedem Fall eine Spalte, und der
        /// Rueckfall waere eine Auswertung im Speicher - also die ganze Mandanten-Tabelle. Die Namen
        /// kommen per <c>nameof</c> aus dem Vertrag, sind also weiterhin umbenennungs-fest.
        /// </remarks>
        public static Func<DbContext, string, int> For<TTenant>() where TTenant : class, ITenantIdentity
        {
            return (context, tenantName) =>
            {
                // Die haeufigste Fehlbedienung ist die flache Auspraegung an einem hierarchischen Kontext
                // (oder umgekehrt) - und sie ist besonders leicht zu uebersehen, weil die flache und die
                // hierarchische Binder-Entitaet DENSELBEN Klassennamen tragen und sich nur im Namensraum
                // unterscheiden. Deshalb zuerst das Modell fragen: EF wirft sonst erst beim Ausfuehren
                // der Abfrage, und die Meldung nennt den Typ, aber nicht die Ursache.
                if (context.Model.FindEntityType(typeof(TTenant)) == null)
                {
                    throw new InvalidOperationException(
                        $"The tenant entity '{typeof(TTenant).FullName}' is not part of the model of " +
                        $"'{context.GetType().FullName}'. Check which tenant flavour this context uses " +
                        "(flat or hierarchical, full model or binder) and configure the matching one.");
                }

                int[] found = context.Set<TTenant>()
                    .Where(t => EF.Property<string>(t, nameof(ITenantIdentity.TenantName)) == tenantName)
                    .Select(t => EF.Property<int>(t, nameof(ITenantIdentity.TenantId)))
                    .Take(1)
                    .ToArray();

                if (found.Length == 0)
                {
                    // Sonst schriebe der Aufrufer eine Fantasie-Id in die Fremdschluessel - der Datensatz
                    // laege danach bei einem Mandanten, den es nicht gibt, und niemand saehe warum.
                    throw new InvalidOperationException(
                        $"No tenant named '{tenantName}' exists in '{typeof(TTenant).Name}'. The current " +
                        "permission scope does not match any tenant of this context.");
                }

                return found[0];
            };
        }
    }
}
