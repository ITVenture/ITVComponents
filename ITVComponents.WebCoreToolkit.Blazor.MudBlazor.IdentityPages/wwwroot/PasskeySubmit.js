// Web Component that drives WebAuthn from a form-submit. Consumer must include this script as a module
// in their host (e.g., App.razor):
//
//   <script src="_content/ITVComponents.WebCoreToolkit.Blazor.MudBlazor.IdentityPages/PasskeySubmit.js" type="module"></script>
//
// The element posts credential JSON via the parent form to its server-side action. Endpoints:
//   - POST /Account/PasskeyRequestOptions?username=...      (sign-in path, anonymous)
//   - POST /Account/Manage/PasskeyCreationOptions           (management path, requires auth)

const browserSupportsPasskeys =
    typeof navigator.credentials !== 'undefined' &&
    typeof window.PublicKeyCredential !== 'undefined' &&
    typeof window.PublicKeyCredential.parseCreationOptionsFromJSON === 'function' &&
    typeof window.PublicKeyCredential.parseRequestOptionsFromJSON === 'function';

async function fetchWithErrorHandling(url, options = {}) {
    const response = await fetch(url, {
        credentials: 'include',
        ...options
    });
    if (!response.ok) {
        const text = await response.text();
        console.error(text);
        throw new Error(`The server responded with status ${response.status}.`);
    }
    return response;
}

async function createCredential(headers, signal) {
    const optionsResponse = await fetchWithErrorHandling('/Account/Manage/PasskeyCreationOptions', {
        method: 'POST',
        headers,
        signal,
    });
    const optionsJson = await optionsResponse.json();
    const options = PublicKeyCredential.parseCreationOptionsFromJSON(optionsJson);
    return await navigator.credentials.create({ publicKey: options, signal });
}

async function requestCredential(email, mediation, headers, signal) {
    const optionsResponse = await fetchWithErrorHandling(`/Account/PasskeyRequestOptions?username=${email}`, {
        method: 'POST',
        headers,
        signal,
    });
    const optionsJson = await optionsResponse.json();
    const options = PublicKeyCredential.parseRequestOptionsFromJSON(optionsJson);
    return await navigator.credentials.get({ publicKey: options, mediation, signal });
}

// ---------------------------------------------------------------------------------------------------
// Interaktiver Weg (Account/Manage/Passkeys).
//
// Die Konto-Seiten rendern interaktiv; das Ergebnis reist ueber die .NET-Referenz zurueck statt ueber
// einen Formular-Post. Der WebAuthn-Aufruf bleibt dabei bewusst HIER, im Klick-Handler:
// navigator.credentials.create() verlangt eine frische Benutzer-Geste. Ginge der Klick erst ueber
// SignalR zum Server und von dort per JS-Interop zurueck, waere der Gesten-Kontext verlassen - Safari
// ist da streng, und der Aufruf schluege mit NotAllowedError fehl.
//
// BEIDE Server-Haelften laufen ueber Endpunkte, nicht ueber den Circuit:
// MakePasskeyCreationOptionsAsync legt die Herausforderung in einem verschluesselten Cookie ab, und
// PerformPasskeyAttestationAsync liest sie von dort wieder. Beide brauchen darum den HttpContext, den es
// auf einem Circuit nicht gibt - frueher erzeugte die Seite die Optionen selbst und nahm auch das
// Credential selbst entgegen; beides schlug mit "HttpContext must not be null" fehl.
//
// Die Optionen werden schon beim Anhaengen geholt, damit der Klick-Handler ohne vorheriges await bis zu
// credentials.create() durchlaeuft und die Benutzer-Geste erhalten bleibt.
// ---------------------------------------------------------------------------------------------------
export function attachPasskeyCreate(buttonId, dotNetRef, tokenHeaderName, tokenValue) {
    const button = document.getElementById(buttonId);
    if (!button) {
        console.error(`attachPasskeyCreate: no element with id '${buttonId}'.`);
        return;
    }

    const headers = tokenHeaderName ? { [tokenHeaderName]: tokenValue } : {};

    let options = null;
    let optionsError = null;
    const optionsReady = fetchWithErrorHandling('/Account/Manage/PasskeyCreationOptions', {
        method: 'POST',
        headers,
    })
        .then(response => response.json())
        .then(json => { options = PublicKeyCredential.parseCreationOptionsFromJSON(json); })
        .catch(error => { optionsError = error; console.error(error); });

    button.addEventListener('click', async (event) => {
        event.preventDefault();
        try {
            if (!browserSupportsPasskeys) {
                throw new Error('Some passkey features are missing. Please update your browser.');
            }

            // Normalfall: laengst da, kein await noetig. Nur wer sofort nach dem Seitenaufbau klickt,
            // wartet hier - und zahlt dafuer das Risiko, dass Safari die Geste nicht mehr anerkennt.
            if (!options && !optionsError) {
                await optionsReady;
            }

            if (optionsError) {
                throw optionsError;
            }

            const credential = await navigator.credentials.create({ publicKey: options });

            // Pruefen und Ablegen erledigt der Endpunkt - er hat den HttpContext, aus dem
            // PerformPasskeyAttestationAsync die Herausforderung zurueckliest.
            //
            // Bewusst NICHT ueber fetchWithErrorHandling: der Endpunkt begruendet eine Ablehnung im Rumpf
            // ("Schluessel ist schon vergeben", "Attestierung fehlgeschlagen"), und diese Begruendung soll
            // der Benutzer lesen - nicht "status 400".
            const attestationResponse = await fetch('/Account/Manage/PasskeyAttestation', {
                credentials: 'include',
                method: 'POST',
                headers: { ...headers, 'Content-Type': 'application/json' },
                body: JSON.stringify(credential),
            });
            if (!attestationResponse.ok) {
                const reason = (await attestationResponse.text())
                    || `The server responded with status ${attestationResponse.status}.`;
                throw new Error(reason);
            }
            const { credentialId } = await attestationResponse.json();
            await dotNetRef.invokeMethodAsync('OnPasskeyRegisteredAsync', credentialId);
        } catch (error) {
            if (error.name === 'AbortError') {
                // Vom Benutzer abgebrochen - kein Fehler, nur nichts zu tun.
                return;
            }
            console.error(error);
            const message = error.name === 'NotAllowedError'
                ? 'No passkey was provided by the authenticator.'
                : error.message;
            await dotNetRef.invokeMethodAsync('OnPasskeyFailedAsync', message);
        }
    });
}

// Doppelte Registrierung vermeiden: das Modul kann sowohl vom <script>-Tag des Hosts als auch ueber
// import() aus einer interaktiven Komponente geladen werden.
if (!customElements.get('passkey-submit')) {
customElements.define('passkey-submit', class extends HTMLElement {
    static formAssociated = true;

    connectedCallback() {
        this.internals = this.attachInternals();
        this.attrs = {
            operation: this.getAttribute('operation'),
            name: this.getAttribute('name'),
            emailName: this.getAttribute('email-name'),
            requestTokenName: this.getAttribute('request-token-name'),
            requestTokenValue: this.getAttribute('request-token-value'),
        };

        this.internals.form.addEventListener('submit', (event) => {
            if (event.submitter?.name === '__passkeySubmit') {
                event.preventDefault();
                this.obtainAndSubmitCredential();
            }
        });

        this.tryAutofillPasskey();
    }

    disconnectedCallback() {
        this.abortController?.abort();
    }

    async obtainCredential(useConditionalMediation, signal) {
        if (!browserSupportsPasskeys) {
            throw new Error('Some passkey features are missing. Please update your browser.');
        }

        const headers = {
            [this.attrs.requestTokenName]: this.attrs.requestTokenValue,
        };

        if (this.attrs.operation === 'Create') {
            return await createCredential(headers, signal);
        } else if (this.attrs.operation === 'Request') {
            const email = new FormData(this.internals.form).get(this.attrs.emailName);
            const mediation = useConditionalMediation ? 'conditional' : undefined;
            return await requestCredential(email, mediation, headers, signal);
        } else {
            throw new Error(`Unknown passkey operation '${this.attrs.operation}'.`);
        }
    }

    async obtainAndSubmitCredential(useConditionalMediation = false) {
        this.abortController?.abort();
        this.abortController = new AbortController();
        const signal = this.abortController.signal;
        const formData = new FormData();
        try {
            const credential = await this.obtainCredential(useConditionalMediation, signal);
            const credentialJson = JSON.stringify(credential);
            formData.append(`${this.attrs.name}.CredentialJson`, credentialJson);
        } catch (error) {
            if (error.name === 'AbortError') {
                // The user explicitly canceled the operation - return without error.
                return;
            }
            console.error(error);
            if (useConditionalMediation) {
                // An error occurred during conditional mediation, which is not user-initiated.
                // We log the error in the console but do not relay it to the user.
                return;
            }
            const errorMessage = error.name === 'NotAllowedError'
                ? 'No passkey was provided by the authenticator.'
                : error.message;
            formData.append(`${this.attrs.name}.Error`, errorMessage);
        }
        this.internals.setFormValue(formData);
        this.internals.form.submit();
    }

    async tryAutofillPasskey() {
        if (browserSupportsPasskeys && this.attrs.operation === 'Request' && await PublicKeyCredential.isConditionalMediationAvailable?.()) {
            await this.obtainAndSubmitCredential(/* useConditionalMediation */ true);
        }
    }
});
}
