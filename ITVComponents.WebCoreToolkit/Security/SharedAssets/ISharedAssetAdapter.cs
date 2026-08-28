using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    public interface ISharedAssetAdapter
    {
        AssetInfo GetAssetInfo(string assetKey, ClaimsPrincipal requestor, bool asOwner = false);
        bool VerifyRequestLocation(string requestPath, string assetKey, string userScope, ClaimsPrincipal requestor);
        AssetTemplateInfo[] GetEligibleShares(string requestPath);
        AssetInfo CreateSharedAsset(string requestPath, AssetTemplateInfo template, string title);

        /// <summary>
        /// Erzeugt eine Freigabe, die auf ein bestimmtes Objekt zeigt.
        /// <para>
        /// Die Argumentwerte werden <b>hart</b> geprueft: fehlt ein Pflichtargument der Vorlage oder passt
        /// ein Wert nicht zu seinem Typ, entsteht keine Freigabe. Diese Pruefung braucht nur die Vorlage
        /// und ist deshalb immer verlaesslich - anders als die Pruefung gegen die Konsumenten-Registry,
        /// die nach einem Neustart unvollstaendig sein kann und nur warnen darf.
        /// </para>
        /// </summary>
        /// <param name="requestPath">der Pfad, auf dem geteilt wird</param>
        /// <param name="template">die Vorlage</param>
        /// <param name="title">die Bezeichnung der Freigabe</param>
        /// <param name="argumentValues">die Werte der Argumente dieser Vorlage</param>
        /// <param name="recipientLabel">an wen sie gerichtet ist, oder null. Eine Notiz, kein Nachweis -
        /// sie entscheidet ueber nichts</param>
        /// <param name="anonymous">true, wenn die Freigabe ohne Anmeldung benutzbar sein soll. Das
        /// entscheidet, WER sie erreicht, und muss deshalb schon beim Anlegen feststehen: eine Freigabe
        /// ohne Reichweite erreicht niemanden - auch den Empfaenger nicht, fuer den sie gemacht wurde</param>
        /// <param name="error">benennt, was fehlt oder nicht passt, wenn nichts entsteht</param>
        /// <returns>die Freigabe oder null</returns>
        AssetInfo CreateSharedAsset(string requestPath, AssetTemplateInfo template, string title,
            IDictionary<string, string> argumentValues, string recipientLabel, bool anonymous, out string error);
        bool UpdateSharedAsset(FullAssetInfo updateInfo);
        bool DeleteSharedAsset(FullAssetInfo assetInfo);
        string CreateAnonymousLink(AssetInfo info, HttpContext context);
        string CreateLink(AssetInfo info, HttpContext context);

        /// <summary>
        /// Wie <see cref="CreateLink(AssetInfo, HttpContext)"/>, aber ohne laufende Anfrage - fuer den
        /// Blazor-Circuit, der keine hat.
        /// </summary>
        /// <param name="info">die Freigabe</param>
        /// <param name="origin">Schema und Host, z.B. <c>https://app.example.com</c></param>
        /// <returns>der Link</returns>
        string CreateLink(AssetInfo info, string origin);

        /// <summary>
        /// Wie <see cref="CreateAnonymousLink(AssetInfo, HttpContext)"/>, aber ohne laufende Anfrage.
        /// </summary>
        /// <param name="info">die Freigabe</param>
        /// <param name="origin">Schema und Host</param>
        /// <returns>der Link fuer anonyme Empfaenger</returns>
        string CreateAnonymousLink(AssetInfo info, string origin);

        /// <summary>
        /// Die Freigaben des aktuellen Mandanten.
        /// </summary>
        /// <param name="search">Suchbegriff auf Titel und Pfad, oder null</param>
        /// <param name="skip">wieviele Zeilen zu ueberspringen sind</param>
        /// <param name="take">wieviele Zeilen zu liefern sind</param>
        /// <param name="total">die Gesamtzahl</param>
        /// <returns>die Zeilen</returns>
        SharedAssetListItem[] ListSharedAssets(string search, int skip, int take, out int total);

        /// <summary>
        /// Erneuert das Geheimnis einer Freigabe: alle bereits verschickten anonymen Links werden damit
        /// ungueltig, die Freigabe selbst bleibt bestehen.
        /// <para>
        /// Das ist der einzige Weg, einen verteilten Link zurueckzuziehen, ohne die Freigabe zu loeschen -
        /// und deshalb gehoert er in die Verwaltungsmaske.
        /// </para>
        /// </summary>
        /// <param name="assetKey">die Freigabe</param>
        /// <returns>true, wenn erneuert wurde</returns>
        bool RotateAnonymousToken(string assetKey);

        /// <summary>
        /// Erzeugt ein Ad-hoc-Ticket: eine Freigabe, die nirgends gespeichert wird, sondern
        /// verschluesselt in der URL reist.
        /// <para>
        /// Die Vorlage muss es erlauben, und die Frist ist Pflicht - hoechstens so lang, wie die Vorlage
        /// zulaesst. Ein Ticket laesst sich nicht einzeln loeschen; die kurze Frist ist der Grund, warum
        /// das vertretbar ist.
        /// </para>
        /// </summary>
        /// <param name="requestPath">der Pfad, auf dem geteilt wird</param>
        /// <param name="template">die Vorlage</param>
        /// <param name="argumentValues">worauf das Ticket zeigt</param>
        /// <param name="recipientLabel">an wen es geht (verschluesselt, nicht im Klartext in der URL)</param>
        /// <param name="lifetime">wie lange es gilt; null = die Hoechstdauer der Vorlage</param>
        /// <param name="origin">Schema und Host fuer den Link</param>
        /// <param name="error">benennt, was fehlt, wenn nichts entsteht</param>
        /// <returns>der fertige Link oder null</returns>
        string CreateAdHocTicket(string requestPath, AssetTemplateInfo template,
            IDictionary<string, string> argumentValues, string recipientLabel, TimeSpan? lifetime, string origin,
            out string error);

        /// <summary>
        /// Loest ein Ad-hoc-Ticket auf: entschluesseln, pruefen, und die Rechte aus seiner Vorlage holen.
        /// Liefert null, wenn irgendetwas daran nicht stimmt - abgelaufen, widerrufen, veraendert, oder
        /// die Vorlage erlaubt keine Tickets (mehr).
        /// </summary>
        /// <param name="tenantName">der Mandant aus dem Abschnitt</param>
        /// <param name="payload">die verschluesselte Nutzlast</param>
        /// <param name="requestor">wer anfragt</param>
        /// <returns>die Angaben zur Freigabe oder null</returns>
        AssetInfo GetTicketInfo(string tenantName, string payload, ClaimsPrincipal requestor);

        /// <summary>
        /// Zieht ein Ad-hoc-Ticket zurueck. Die Kennung landet auf der Sperrliste, bis das Ticket ohnehin
        /// abgelaufen waere.
        /// </summary>
        /// <param name="nonce">die Kennung aus der Nutzlast</param>
        /// <param name="expiresUtc">wann es von selbst geendet haette</param>
        /// <returns>true, wenn es zurueckgezogen wurde</returns>
        bool RevokeTicket(string nonce, DateTime expiresUtc);
        FullAssetInfo FindAnonymousAsset(string assetKey);
        protected internal void SetImpersonationOff();
        protected internal void SetImpersonationOn();
    }
}
