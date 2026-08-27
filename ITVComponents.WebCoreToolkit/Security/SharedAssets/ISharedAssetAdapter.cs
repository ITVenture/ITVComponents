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
        /// <param name="recipientLabel">an wen sie gerichtet ist, oder null</param>
        /// <param name="error">benennt, was fehlt oder nicht passt, wenn nichts entsteht</param>
        /// <returns>die Freigabe oder null</returns>
        AssetInfo CreateSharedAsset(string requestPath, AssetTemplateInfo template, string title,
            IDictionary<string, string> argumentValues, string recipientLabel, out string error);
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
        FullAssetInfo FindAnonymousAsset(string assetKey);
        protected internal void SetImpersonationOff();
        protected internal void SetImpersonationOn();
    }
}
