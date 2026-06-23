// Minimal client helpers for the onboarding views.
// setCookie writes the join-auto-login nonce as a (deliberately non-httponly) cookie so the ConfirmEmail
// request from the SAME browser can bind the seamless sign-in. The value is only a binding nonce — it grants
// no session on its own (ConfirmEmail also requires the matching server-side token + a valid confirm link).
window.itvOnboarding = window.itvOnboarding || {};
window.itvOnboarding.setCookie = function (name, value, maxAgeSeconds) {
    document.cookie = name + '=' + encodeURIComponent(value) + '; path=/; max-age=' + maxAgeSeconds + '; samesite=strict';
};
