using ITVComponents.WebCoreToolkit.Billing.Terminals.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Abstractions;
using ITVComponents.WebCoreToolkit.EntityFramework.Billing.Models.Payments;
using Microsoft.EntityFrameworkCore;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Billing.Payments
{
    /// <summary>
    /// Kassieren über ein Terminal, das an einem Kassen-PC hängt statt in der Cloud eines Anbieters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der vierte Weg, und der einzige, bei dem wir nicht selbst mit dem Anbieter reden. Gebraucht wird
    /// er für Geräte, die nur lokal ansprechbar sind — wallee über die Local Till Interface etwa, wo
    /// Kasse und Terminal im selben Netz hängen und die Cloud nichts davon weiss.
    /// </para>
    /// <para>
    /// <b>Hier steht auffällig wenig, und das ist der Punkt.</b> Diese Klasse reicht durch und bucht
    /// nichts selbst: die Buchführung liegt in der Basis, also am Server. Was der Agent tut, ist mit dem
    /// Gerät zu reden — mehr darf ihm nicht anvertraut werden, weil er der Rechner ist, der mitten in
    /// einer Zahlung neu startet.
    /// </para>
    /// <para>
    /// Wie aus <see cref="TenantPaymentTerminal.Route"/> eine Verbindung wird, entscheidet die Anwendung
    /// über ihren <see cref="ITerminalAgentLocator"/>. Das Toolkit schreibt keinen Transport vor und
    /// kennt die Proxy-Verdrahtung des Hosts nicht — es fragt nur nach einem <see cref="ITerminalDevice"/>
    /// für dieses eine Gerät.
    /// </para>
    /// </remarks>
    public class AgentTerminalPaymentService<TContext> : TerminalPaymentServiceBase<TContext>
        where TContext : DbContext, IPaymentsContext
    {
        /// <summary>Der Name, unter dem dieser Weg an einem Gerät eingetragen wird.</summary>
        public const string Key = "agent";

        private readonly ITerminalAgentLocator locator;

        /// <summary>Initializes a new instance of the <see cref="AgentTerminalPaymentService{TContext}"/> class.</summary>
        public AgentTerminalPaymentService(IDbContextFactory<TContext> dbFactory, PaymentsRuntime runtime,
            TenantSaleWebhookSink<TContext> sink, ITerminalAgentLocator locator)
            : base(dbFactory, runtime, sink)
        {
            this.locator = locator;
        }

        /// <inheritdoc />
        protected override string ProviderKey => Key;

        /// <inheritdoc />
        protected override async Task<TerminalPaymentOutcome> StartAtDeviceAsync(TenantSale sale,
            TenantPaymentTerminal terminal, TerminalPaymentCommand command, CancellationToken cancellationToken)
        {
            var device = await locator.GetDeviceAsync(terminal, cancellationToken);
            return await device.StartPaymentAsync(TargetOf(terminal), command, cancellationToken);
        }

        /// <inheritdoc />
        protected override async Task<TerminalPaymentOutcome> QueryDeviceAsync(TenantSale sale,
            TenantPaymentTerminal terminal, string operationId, CancellationToken cancellationToken)
        {
            var device = await locator.GetDeviceAsync(terminal, cancellationToken);
            return await device.GetPaymentAsync(TargetOf(terminal), operationId, cancellationToken);
        }

        /// <inheritdoc />
        protected override async Task<TerminalPaymentOutcome> CancelAtDeviceAsync(TenantSale sale,
            TenantPaymentTerminal terminal, string operationId, CancellationToken cancellationToken)
        {
            var device = await locator.GetDeviceAsync(terminal, cancellationToken);
            return await device.CancelPaymentAsync(TargetOf(terminal), operationId, cancellationToken);
        }

        /// <inheritdoc />
        protected override async Task<TerminalStatus> QueryStatusAsync(TenantPaymentTerminal terminal,
            CancellationToken cancellationToken)
        {
            var device = await locator.GetDeviceAsync(terminal, cancellationToken);
            return await device.GetStatusAsync(TargetOf(terminal), cancellationToken);
        }
    }
}
