using System;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.DataSync
{
    /// <summary>
    /// Wird ZUSAETZLICH zu <see cref="IConfigExtension"/> umgesetzt, wenn die Entitaeten einer Sektion
    /// nicht im Modell des Host-Kontexts liegen, sondern in einem eigenen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// **Wofuer.** Die mitgelieferten Sektionen (Billing, Hilfe) arbeiten im Host-Kontext - der
    /// Anwendungs-Context setzt <c>IBillingContext</c> bzw. <c>IHelpSystemContext</c> um. Fachmodule
    /// sind anders gebaut: sie haengen an einem eigenen DbContext, damit ein Modul kommen und gehen
    /// kann, ohne den Sicherheits-Context anzufassen. Deren Entitaeten sind im Host-Kontext nicht
    /// einmal aufloesbar - beschreiben liesse sich eine solche Sektion noch von Hand, einspielen nicht.
    /// </para>
    /// <para>
    /// **Ohne dieses Interface bleibt alles wie bisher**: die Extension bekommt den Host-Kontext, und
    /// ihre Changes laufen mit denen des Hosts in einem Zug.
    /// </para>
    /// </remarks>
    public interface IConfigExtensionContext
    {
        /// <summary>
        /// Erzeugt den Kontext fuer EINEN Vorgang (Beschreiben, Vergleichen oder Einspielen).
        /// </summary>
        /// <param name="services">
        /// der Dienstanbieter des Bereichs, in dem der Handler gebaut wurde - aus ihm darf geholt
        /// werden, was zum Erzeugen noetig ist
        /// </param>
        /// <returns>ein frischer Kontext</returns>
        /// <remarks>
        /// **Der Aufrufer schliesst ihn.** Deshalb heisst die Methode <c>Create…</c> und nicht
        /// <c>Resolve…</c>: erwartet wird eine frische Instanz je Vorgang, kein gehaltener oder
        /// geteilter Kontext.
        ///
        /// Das ist keine Formalie. Unter Blazor Server lebt ein Bereichs-Kontext so lange wie der
        /// Circuit des Benutzers; ein Import wuerde seine Aenderungen in einen Kontext schreiben, den
        /// die Oberflaeche nebenher weiterbenutzt - mit allem, was ein geteilter Aenderungsnachweis an
        /// Ueberraschungen bereithaelt. Je Vorgang ein eigener Kontext, danach geschlossen.
        ///
        /// **Filter sind Sache der Umsetzung.** Den Sicherheits-Context macht das Toolkit fuer die
        /// Dauer des Einspielens mandanten-blind; einen fremden Kontext kann es nicht kennen. Wer dort
        /// globale Filter fuehrt, schaltet sie in dieser Methode aus - sonst sieht der Vergleich
        /// weniger Zeilen, als da sind, und der Import legt an, was er nur nicht sehen konnte.
        /// </remarks>
        DbContext CreateContext(IServiceProvider services);
    }
}
