# Issue: `IAppMailSender` kann keine Anhänge — der Transport darunter schon (Anstoss aus MiniStore)

**Status:** ERLEDIGT — umgesetzt im Toolkit (siehe „Auflösung" am Ende)
**Datum:** 2026-09-15
**Quelle:** MiniStore-Session (Konsument). MiniStore baut den Self-Checkout-Beleg: der Kunde soll seinen
Bon als **PDF** bekommen, wahlweise als Download oder per Mail.
**Toolkit-Stand:** `5.0.0-PRE237`. Alle Aussagen unten sind gegen diesen Stand am Quelltext verifiziert.

## Der Fall

`IAppMailSender` ist die Schnittstelle, mit der ein Konsument eine freie Mail verschickt, ohne vom
Identity-UI-Paket abzuhängen. Sie kennt genau einen Aufruf:

```csharp
// ITVComponents.WebCoreToolkit/Security/IAppMailSender.cs
Task SendMailAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
```

Kein Anhang, keine Möglichkeit, einen mitzugeben. Ein PDF lässt sich damit nicht versenden.

**Der Transport darunter kann es.** `AppMailSender` reicht an `IEmailSender` durch, und dessen
Standard-Implementierung baut eine `System.Net.Mail.MailMessage`:

```csharp
// ITVComponents.WebCoreToolkit.IdentityShared/Services/Impl/DefaultMailSender.cs
MailMessage msg = new()
{
    From = new MailAddress(settings.SenderAddress, settings.SenderDisplayName),
    Body = message,
    IsBodyHtml = true,
    Subject = subject
};
```

`MailMessage.Attachments` ist vorhanden und wird nur nicht benutzt. Es fehlt also nichts am Transport —
es fehlt der Weg dorthin.

**Warum der Umweg über `IEmailSender` ihn versperrt:** das ist die Schnittstelle von
ASP.NET Core Identity (`Microsoft.AspNetCore.Identity.UI.Services.IEmailSender`) mit der festen Signatur
`SendEmailAsync(string, string, string)`. Sie lässt sich nicht erweitern. Ein Anhang muss deshalb an ihr
*vorbei*, nicht durch sie hindurch.

## Betroffene Stellen

| Datei | Was dort steht |
|---|---|
| `ITVComponents.WebCoreToolkit/Security/IAppMailSender.cs` | Die Schnittstelle — hier fehlt die Überladung |
| `ITVComponents.WebCoreToolkit.IdentityShared/Services/Impl/AppMailSender.cs` | Reicht heute nur durch; müsste für den Anhang-Fall direkt auf den Transport gehen |
| `ITVComponents.WebCoreToolkit.IdentityShared/Services/Impl/DefaultMailSender.cs` | Baut die `MailMessage`; kennt Produktiv-, Test- (Pickup-Verzeichnis) und Log-Modus |

Der Log-/Testmodus ist der Grund, warum wir das **nicht** am Toolkit vorbei selbst lösen wollen: ein
eigener SMTP-Weg im Konsumenten würde genau diese drei Betriebsarten samt ihrer Einstellungen
(`IdentityMailSettings`) verlieren.

## Vorschlag

Eine Überladung an `IAppMailSender`, die Anhänge mitnimmt — als **Standardimplementierung** auf der
Schnittstelle, damit bestehende Implementierer nichts anpassen müssen:

```csharp
public sealed record MailAttachment(string FileName, byte[] Content, string ContentType);

public interface IAppMailSender
{
    Task SendMailAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);

    /// <summary>Sends an HTML mail with attachments.</summary>
    Task SendMailAsync(string toEmail, string subject, string htmlBody,
        IReadOnlyList<MailAttachment> attachments, CancellationToken ct = default)
        => attachments is not { Count: > 0 }
            ? SendMailAsync(toEmail, subject, htmlBody, ct)
            : throw new NotSupportedException("This mail sender does not support attachments.");
}
```

Dazu im `DefaultMailSender` ein Weg, der die `MailMessage` mit Anhängen baut — die drei Betriebsarten
bleiben, wie sie sind. Ob das über eine zweite Methode am `DefaultMailSender` läuft (an `IEmailSender`
vorbei, direkt von `AppMailSender` aufgerufen) oder über eine eigene, toolkit-eigene Transport-Abstraktion,
ist eine Entscheidung des Toolkits — von aussen zählt nur, dass `IAppMailSender` den Anhang annimmt.

**`byte[]` statt `Stream`** ist Absicht: ein Anhang ist klein, und ein Stream müsste über die Lebensdauer
des Versands offen bleiben, was bei einem Hintergrundversand die häufigste Fehlerquelle ist.

## Umgehung bis dahin

MiniStore schickt den Bon vorerst **als HTML im Rumpf** statt als PDF-Anhang. Derselbe Bon wird ohnehin aus
demselben Dokument gerendert (`ReceiptDocument` → HTML bzw. PDF), der Wechsel ist für uns also eine Zeile,
sobald die Überladung da ist. Der PDF-Weg ist parallel fertig und bedient bereits den Download.

Das ist eine brauchbare Übergangslösung, aber keine dauerhafte: ein Beleg, den man ablegen oder
weiterleiten will, gehört als Datei in die Mail und nicht in den Rumpf.

## Was wir NICHT brauchen

Kein Termin, keine Priorität von unserer Seite — der Download-Weg trägt den Anwendungsfall. Sobald die
Überladung verfügbar ist, ziehen wir nach.

---

## Auflösung (Toolkit)

Umgesetzt wie vorgeschlagen, mit einer Präzisierung an der Stelle, die das Issue dem Toolkit überlassen hat.

**Neu im Kern (`ITVComponents.WebCoreToolkit/Security/`):**

- `MailAttachment` — `record (string FileName, byte[] Content, string ContentType)`. `byte[]` wie
  vorgeschlagen und aus demselben Grund. Ein leerer Dateiname wirft, ein fehlender Medientyp fällt auf
  `application/octet-stream` zurück.
- `IAttachmentMailSender` — die **toolkit-eigene Transport-Abstraktion**. Sie steht *neben*
  `Microsoft.AspNetCore.Identity.UI.Services.IEmailSender`, nicht darüber: dessen Signatur ist fix, ein
  Anhang muss also an ihr vorbei. Ein Transport implementiert beide Schnittstellen.
- `IAppMailSender` bekommt die Überladung als **Standardimplementierung**, wortgleich zum Vorschlag:
  leere Liste → bestehender Weg, sonst `NotSupportedException`. Bestehende Implementierer bleiben
  unverändert übersetzbar.

**Im Transport (`…IdentityShared/Services/Impl/`):**

- `AppMailSender` fragt den injizierten `IEmailSender` nach `IAttachmentMailSender` (`is`-Muster). Der
  `DefaultMailSender` ist als `IEmailSender` registriert und *ist* eine solche Instanz, der Weg trägt also
  ohne zusätzliche Registrierung. Ein Host, der den Transport gegen einen anhanglosen ersetzt hat, bekommt
  eine protokollierte `NotSupportedException` statt einer Mail, deren Dateien still verschwinden.
- `DefaultMailSender` implementiert jetzt `IEmailSender` **und** `IAttachmentMailSender`. Beide Einstiege
  laufen durch **einen** privaten `SendCoreAsync` — das war die eigentliche Arbeit: die drei Betriebsarten
  (Produktiv, Pickup-Verzeichnis, Fehlkonfiguration) lagen inline in der einen Methode und wären bei einem
  zweiten Einstieg auseinandergedriftet. Genau dieses Risiko ist der Grund, warum das Issue den Umweg
  über den eigenen SMTP-Weg vermieden hat; es hätte sich sonst im Toolkit wiederholt.

**Nebenbei mitgenommen:** `MailMessage` und `SmtpClient` werden jetzt entsorgt (die Anhang-Streams hängen
an der `MailMessage`, ohne `using` bliebe pro Mail ein `MemoryStream` offen), der `CancellationToken` wird
bis `SmtpClient.SendMailAsync` durchgereicht, und die Protokollzeilen führen die Anzahl der Anhänge mit.

**Für MiniStore:** der Wechsel ist die angekündigte eine Zeile — `SendMailAsync(…, attachments)` mit
`new MailAttachment("Beleg.pdf", pdfBytes, "application/pdf")`. Die HTML-im-Rumpf-Umgehung kann weg.
