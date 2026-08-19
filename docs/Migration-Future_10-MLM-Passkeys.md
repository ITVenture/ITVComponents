# Passkeys nach der Konventions-Korrektur: was MLM tun muss

**Kurz:** MLM behält seine Passkeys nur, wenn zwei Dinge zusammenkommen — der eigene Passkey-Block im
`ApplicationDbContext` **und** der neue Schalter `UsePasskeys` in den Identity-Optionen. Fehlt der
Block, verschwinden die Passkeys aus dem Modell. Fehlt der Schalter, verschwinden sie aus der
Oberfläche. Beides ist neu; vorher gab es keinen Schalter, und der Block wirkte nur zufällig.

---

## Warum sich überhaupt etwas ändert

.NET 10 hat `IdentityUserContext` ein `UserPasskeys`-DbSet hinzugefügt, den Entitätstyp dazu aber
**ausdrücklich ausgeschlossen** — Passkeys sind seither opt-in.

Die Konvention `TableNamesFromProperties` in `ITVComponents.EFRepo` lief mit `FlattenHierarchy` über
*alle* DbSet-Eigenschaften, also auch über geerbte, und rief für jede `builder.Entity(...)`. Damit hat
sie den ausdrücklichen Ausschluss überstimmt und `IdentityUserPasskey<string>` unkonfiguriert ins
Modell gezogen — samt dessen `Data`-Eigenschaft, die dort als schlüssellose Entität landete. Ergebnis:

```
The entity type 'IdentityPasskeyData' requires a primary key to be defined.
  at ModelValidator.ValidateNonNullPrimaryKeys
```

Das traf **jeden** Identity-Kontext der Bibliothek, auf SQL Server genauso wie auf PostgreSQL, und
zwar beim Bauen des Modells — also `dotnet ef` *und* die Laufzeit. Dass MLM lief, lag allein am
eigenen Passkey-Block in `ApplicationDbContext.OnModelCreating`, der die Entität nachträglich
vollständig konfiguriert hat.

Die Konvention respektiert jetzt ausdrückliche Ausschlüsse. Damit ist der Fehler weg — und damit ist
auch der Passkey-Typ standardmäßig **nicht mehr im Modell**, so wie .NET 10 es vorsieht.

> Der Ausschluss gilt nur für *absichtliche* Ausschlüsse (`Explicit`, `DataAnnotation`). Was bloß eine
> Konvention ausgeschlossen hat, wird weiterhin benannt wie bisher — die Tabellennamen `Users`,
> `Roles`, `UserClaims` usw. kommen genau aus dieser Konvention und bleiben unverändert.

---

## Was MLM tun muss

### 1. Den Passkey-Block behalten — und prüfen, dass er vollständig ist

Der Block in `ApplicationDbContext.OnModelCreating` ist ab jetzt **die einzige** Stelle, die den
Passkey-Typ ins Modell bringt. Er muss

- `IdentityUserPasskey<string>` als Entität registrieren (damit hebt er den Ausschluss von Identity
  bewusst auf — das ist genau die vorgesehene opt-in-Geste),
- einen Schlüssel setzen (`CredentialId`),
- und **`Data` mitkonfigurieren**. Das ist die Stelle, an der es sonst kippt: ohne eine Aussage dazu
  ist `IdentityPasskeyData` eine schlüssellose Entität, und die Validierung bricht wieder mit
  derselben Meldung ab. Sinnvoll ist, sie als komplexen Typ bzw. als JSON-Spalte abzulegen, nicht als
  eigene Tabelle.

**Prüfen lässt sich das ohne Datenbank:**

```
dotnet ef dbcontext info --context ApplicationDbContext
```

Kommt eine Ausgabe mit `Provider name`, ist das Modell in Ordnung. Kommt
`IdentityPasskeyData requires a primary key`, fehlt im Block die Aussage über `Data`.

### 2. Den Schalter setzen

Neu in `IdentityUiOptions`:

```json
{
  "UsePasskeys": true
}
```

**Standard ist `false`.** Ohne diese Zeile registriert das WebPart den Passkey-Handler nicht, und
dann gilt:

| Stelle | Verhalten bei `UsePasskeys: false` |
|---|---|
| Anmeldeseite | kein „Mit Passkey anmelden" |
| Konto-Navigation | kein Eintrag „Passkeys" |
| `Account/Manage/Passkeys` | Seite antwortet „nicht verfügbar" |
| Endpunkte (`/PasskeyCreationOptions`, `/PasskeyAttestation`, …) | antworten ablehnend |

### 3. Nach dem Deployment nachsehen

Ein Konto öffnen, das heute einen Passkey hat, und in der Kontoverwaltung prüfen, ob der Eintrag
„Passkeys" da ist und den vorhandenen Schlüssel auflistet. Fehlt der Eintrag, ist Punkt 2 offen;
ist er da, meldet aber beim Anlegen einen Datenbankfehler, ist Punkt 1 offen.

---

## Warum der Schalter nötig ist und nicht abgeleitet wird

Naheliegend wäre `UserManager.SupportsUserPasskey`. Das trägt aber nicht: der EF-Benutzer-Speicher
setzt die Passkey-Methoden **unbedingt** um, meldet also `true`, auch wenn der DbContext die Entität
gar nicht abbildet. Der Wert beantwortet „kann der Speicher das grundsätzlich", nicht „ist es hier
eingerichtet" — und genau letzteres entscheidet, ob das Speichern gelingt.

Verlässlich wäre nur ein Blick ins Modell des konkreten DbContext, den die Identity-Seiten nicht
kennen (sie sehen `SignInManager`/`UserManager`, nicht den Kontext). Deshalb eine ausdrückliche
Angabe des Hosts — dieselbe Geste, die .NET 10 beim Modell ohnehin verlangt. Wer den Schalter setzt,
sagt damit zu, die Entität auch abzubilden.

---

## Betrifft es andere Konsumenten?

Ja, aber folgenlos: Hosts ohne Passkey-Block hatten bisher gar keine funktionierenden Passkeys — bei
ihnen ändert sich nur, dass die Oberfläche jetzt ehrlich ist und den Abschnitt nicht mehr anbietet.
Betroffen im Sinne von „muss handeln" ist allein, wer Passkeys tatsächlich benutzt.
