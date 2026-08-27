using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace ITVComponents.WebCoreToolkit.Security.SharedAssets
{
    /// <summary>
    /// Der Riegel am Ausgang, MVC-Fassung. Er macht aus "der Handler muss dran denken" ein "wenn er nicht
    /// dran denkt, kommt nichts raus".
    /// <para>
    /// Er greift <b>nach dem Model-Binding</b> - das ist der frueheste Zeitpunkt, an dem das angefragte
    /// Objekt ueberhaupt bekannt ist. Was gebunden wurde, bietet er von sich aus als Bestaetigung an
    /// (Namensgleichheit mit den Argumenten der Freigabe); nach der Aktion prueft er, ob eine
    /// Bestaetigung vorliegt, und verwirft andernfalls das Ergebnis.
    /// </para>
    /// <para>
    /// <b>Warum nicht am Eintritt:</b> Rechte werden vor dem Binding geprueft, die Argumente sind erst
    /// danach bekannt. Beides in einen Zeitpunkt zu falten geht in keinem Host.
    /// </para>
    /// </summary>
    public sealed class SharedAssetGuardFilter : IAsyncActionFilter
    {
        private readonly ISharedAssetContext assetContext;
        private readonly ILogger<SharedAssetGuardFilter> logger;
        private readonly IAssetAccessLog accessLog;

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedAssetGuardFilter"/> class.
        /// </summary>
        /// <param name="assetContext">der Asset-Kontext der Anfrage</param>
        /// <param name="logger">ein Logger fuer zurueckgehaltene Antworten</param>
        /// <param name="accessLog">das Zugriffsprotokoll - ohne EF-Paket die Null-Fassung</param>
        public SharedAssetGuardFilter(ISharedAssetContext assetContext, ILogger<SharedAssetGuardFilter> logger,
            IAssetAccessLog accessLog)
        {
            this.assetContext = assetContext;
            this.logger = logger;
            this.accessLog = accessLog;
        }

        /// <inheritdoc/>
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!assetContext.HasAsset)
            {
                await next();
                return;
            }

            assetContext.ResetConfirmation();
            OfferBoundArguments(context);

            var executed = await next();

            // Hier - und nicht bei jeder Anfrage: eine Aktion ist ein Vorgang, eine Unterressource
            // nicht. Der Riegel ist damit zugleich die Stelle, an der genau einmal je Vorgang
            // feststeht, ob etwas rausging.
            AssetAccessRecorder.Record(accessLog, assetContext, context.HttpContext.User,
                context.HttpContext.Request.Path.Value);

            if (!assetContext.MustHoldBack)
            {
                return;
            }

            // Die Antwort ist fertig - und geht trotzdem nicht raus. Das ist der ganze Sinn eines Riegels
            // am Ausgang: der Endpunkt muss nichts wissen, damit nichts Falsches ausgeliefert wird.
            logger.LogWarning(
                "Withholding the response of {Action}: it runs inside shared asset '{AssetKey}', whose template requires the arguments to be confirmed - and they were not (or the confirmation failed).",
                context.ActionDescriptor.DisplayName, assetContext.AssetKey);
            executed.Result = new StatusCodeResult(403);
            executed.Canceled = true;
        }

        /// <summary>
        /// Bietet die gebundenen Werte als Bestaetigung an. Ein Argument, das die Freigabe nicht kennt,
        /// ist dabei kein Fehler - der Kontext entscheidet, ob es ihn etwas angeht.
        /// </summary>
        private void OfferBoundArguments(ActionExecutingContext context)
        {
            foreach (var argument in context.ActionArguments)
            {
                if (argument.Value == null || !IsSimple(argument.Value))
                {
                    // Nur einfache Werte: ein gebundenes Modell waere ein Objekt, dessen Name nichts ueber
                    // das geteilte Objekt aussagt. Wer aus einem Modell bestaetigen will, ruft Require
                    // selbst - dort ist bekannt, welche Eigenschaft gemeint ist.
                    continue;
                }

                assetContext.Require(argument.Key, argument.Value);
            }
        }

        private static bool IsSimple(object value)
        {
            var type = value.GetType();
            return type.IsPrimitive || type.IsEnum || value is string || value is Guid || value is DateTime
                   || value is decimal;
        }
    }
}
