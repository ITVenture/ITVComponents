using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;

namespace ITVComponents.WebCoreToolkit.Net.SpaApi.AspNetCoreIdentity.Handlers
{
    public sealed class LoginHandler
    {
        private static string confirmEmailEndpointName;

        // Validate the email address using DataAnnotations like the UserValidator does when RequireUniqueEmail = true.
        private static readonly EmailAddressAttribute _emailAddressAttribute = new();

        private const string UserEmailRequiredMessage = "LoginHandler.Login requires a user store with email support.";
        private const string ThankYouForRegister = "Thank you for confirming your email.";
        private const string UserMustHaveMail = "Users must have an email.";
        private const string NoConfirmationEndpointRegistered = "No email confirmation endpoint was registered!";
        private const string CouldNotFindConfirmEndpoint = "Could not find endpoint named '{0}'";
        private const string UnableToCreateNewKey = "A new authentication key could not be created.";

        public static async Task<Results<Ok, ValidationProblem>> Register<TUser>(
            [FromBody] RegisterRequest registration, HttpContext context, [FromServices] IServiceProvider sp,
            [FromServices] LinkGenerator linkGenerator, [FromServices] IEmailSender<TUser> emailSender,
            IStringLocalizer<LoginHandler> localizer) where TUser : class, new()
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();

            if (!userManager.SupportsUserEmail)
            {
                throw new NotSupportedException(localizer[UserEmailRequiredMessage]);
            }

            var userStore = sp.GetRequiredService<IUserStore<TUser>>();
            var emailStore = (IUserEmailStore<TUser>)userStore;
            var email = registration.Email;

            if (string.IsNullOrEmpty(email) || !_emailAddressAttribute.IsValid(email))
            {
                return CreateValidationProblem(IdentityResult.Failed(userManager.ErrorDescriber.InvalidEmail(email)));
            }

            var user = new TUser();
            await userStore.SetUserNameAsync(user, email, CancellationToken.None);
            await emailStore.SetEmailAsync(user, email, CancellationToken.None);
            var result = await userManager.CreateAsync(user, registration.Password);

            if (!result.Succeeded)
            {
                return CreateValidationProblem(result);
            }

            await SendConfirmationEmailAsync(user, userManager, context, email, linkGenerator, emailSender, localizer);
            return TypedResults.Ok();
        }

        public static async Task<Results<Ok<AccessTokenResponse>, EmptyHttpResult, ProblemHttpResult>> Login<TUser>(
            [FromBody] LoginRequest login, [FromQuery] bool? useCookies, [FromQuery] bool? useSessionCookies,
            [FromServices] IServiceProvider sp) where TUser : class
        {
            var signInManager = sp.GetRequiredService<SignInManager<TUser>>();

            var useCookieScheme = (useCookies == true) || (useSessionCookies == true);
            var isPersistent = (useCookies == true) && (useSessionCookies != true);
            signInManager.AuthenticationScheme =
                useCookieScheme ? IdentityConstants.ApplicationScheme : IdentityConstants.BearerScheme;

            var result =
                await signInManager.PasswordSignInAsync(login.Email, login.Password, isPersistent,
                    lockoutOnFailure: true);

            if (result.RequiresTwoFactor)
            {
                if (!string.IsNullOrEmpty(login.TwoFactorCode))
                {
                    result = await signInManager.TwoFactorAuthenticatorSignInAsync(login.TwoFactorCode, isPersistent,
                        rememberClient: isPersistent);
                }
                else if (!string.IsNullOrEmpty(login.TwoFactorRecoveryCode))
                {
                    result = await signInManager.TwoFactorRecoveryCodeSignInAsync(login.TwoFactorRecoveryCode);
                }
            }

            if (!result.Succeeded)
            {
                return TypedResults.Problem(result.ToString(), statusCode: StatusCodes.Status401Unauthorized);
            }

// The signInManager already produced the needed response in the form of a cookie or bearer token.
            return TypedResults.Empty;
        }

        public static async
            Task<Results<Ok<AccessTokenResponse>, UnauthorizedHttpResult, SignInHttpResult, ChallengeHttpResult>>
            Refresh<TUser>([FromBody] RefreshRequest refreshRequest, [FromServices] IServiceProvider sp,
                [FromServices] TimeProvider timeProvider,
                [FromServices] IOptionsMonitor<BearerTokenOptions> bearerTokenOptions) where TUser : class
        {
            var signInManager = sp.GetRequiredService<SignInManager<TUser>>();
            var refreshTokenProtector = bearerTokenOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
            var refreshTicket = refreshTokenProtector.Unprotect(refreshRequest.RefreshToken);

            // Reject the /refresh attempt with a 401 if the token expired or the security stamp validation fails
            if (refreshTicket?.Properties?.ExpiresUtc is not
                    {
                    }
                    expiresUtc ||
                timeProvider.GetUtcNow() >= expiresUtc ||
                await signInManager.ValidateSecurityStampAsync(refreshTicket.Principal) is not TUser user)

            {
                return TypedResults.Challenge();
            }

            var newPrincipal = await signInManager.CreateUserPrincipalAsync(user);
            return TypedResults.SignIn(newPrincipal, authenticationScheme: IdentityConstants.BearerScheme);
        }

        public static async Task<Results<ContentHttpResult, UnauthorizedHttpResult>> ConfirmEmail<TUser>(
            [FromQuery] string userId, [FromQuery] string code, [FromQuery] string? changedEmail,
            [FromServices] IServiceProvider sp, [FromServices] IStringLocalizer<LoginHandler> localizer)
            where TUser : class
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();
            if (await userManager.FindByIdAsync(userId) is not
                {
                }
                user)
            {
                // We could respond with a 404 instead of a 401 like Identity UI, but that feels like unnecessary information.
                return TypedResults.Unauthorized();
            }

            try
            {
                code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            }
            catch (FormatException)
            {
                return TypedResults.Unauthorized();
            }

            IdentityResult result;

            if (string.IsNullOrEmpty(changedEmail))
            {
                result = await userManager.ConfirmEmailAsync(user, code);
            }
            else
            {
                // As with Identity UI, email and user name are one and the same. So when we update the email,
                // we need to update the user name.
                result = await userManager.ChangeEmailAsync(user, changedEmail, code);

                if (result.Succeeded)
                {
                    result = await userManager.SetUserNameAsync(user, changedEmail);
                }
            }

            if (!result.Succeeded)
            {
                return TypedResults.Unauthorized();
            }

            return TypedResults.Text(localizer[ThankYouForRegister]);
        }

        public static async Task<Ok> ResendConfirmationEmail<TUser>(
            [FromBody] ResendConfirmationEmailRequest resendRequest, HttpContext context,
            [FromServices] IServiceProvider sp, [FromServices] LinkGenerator linkGenerator,
            [FromServices] IEmailSender<TUser> emailSender, [FromServices] IStringLocalizer<LoginHandler> localizer)
            where TUser : class
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();
            if (await userManager.FindByEmailAsync(resendRequest.Email) is not
                {
                }
                user)
            {
                return TypedResults.Ok();
            }

            await SendConfirmationEmailAsync(user, userManager, context, resendRequest.Email, linkGenerator,
                emailSender, localizer);
            return TypedResults.Ok();
        }

        public static async Task<Results<Ok, ValidationProblem>> ForgotPassword<TUser>(
            [FromBody] ForgotPasswordRequest resetRequest, [FromServices] IServiceProvider sp,
            [FromServices] IEmailSender<TUser> emailSender) where TUser : class
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();
            var user = await userManager.FindByEmailAsync(resetRequest.Email);

            if (user is not null && await userManager.IsEmailConfirmedAsync(user))
            {
                var code = await userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                await emailSender.SendPasswordResetCodeAsync(user, resetRequest.Email,
                    HtmlEncoder.Default.Encode(code));
            }

            // Don't reveal that the user does not exist or is not confirmed, so don't return a 200 if we would have
            // returned a 400 for an invalid code given a valid user email.
            return TypedResults.Ok();
        }

        public static async Task<Results<Ok, ValidationProblem>> ResetPassword<TUser>(
            [FromBody] ResetPasswordRequest resetRequest, [FromServices] IServiceProvider sp) where TUser : class
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();

            var user = await userManager.FindByEmailAsync(resetRequest.Email);

            if (user is null || !(await userManager.IsEmailConfirmedAsync(user)))
            {
                // Don't reveal that the user does not exist or is not confirmed, so don't return a 200 if we would have
                // returned a 400 for an invalid code given a valid user email.
                return CreateValidationProblem(IdentityResult.Failed(userManager.ErrorDescriber.InvalidToken()));
            }

            IdentityResult result;
            try
            {
                var code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(resetRequest.ResetCode));
                result = await userManager.ResetPasswordAsync(user, code, resetRequest.NewPassword);
            }
            catch (FormatException)
            {
                result = IdentityResult.Failed(userManager.ErrorDescriber.InvalidToken());
            }

            if (!result.Succeeded)
            {
                return CreateValidationProblem(result);
            }

            return TypedResults.Ok();
        }

        public static async Task<Results<Ok<TwoFactorResponse>, ValidationProblem, NotFound>> TwoFa<TUser>(
            ClaimsPrincipal claimsPrincipal, [FromBody] TwoFactorRequest tfaRequest, [FromServices] IServiceProvider sp,
            [FromServices] LinkGenerator linkGenerator, [FromServices] IStringLocalizer<LoginHandler> localizer)
            where TUser : class
        {
            var signInManager = sp.GetRequiredService<SignInManager<TUser>>();
            var userManager = signInManager.UserManager;
            if (await userManager.GetUserAsync(claimsPrincipal) is not
                {
                }
                user)
            {
                return TypedResults.NotFound();
            }

            if (tfaRequest.Enable == true)
            {
                if (tfaRequest.ResetSharedKey)
                {
                    return CreateValidationProblem("CannotResetSharedKeyAndEnable",
                        localizer["CannotResetSharedKeyAndEnable"]);
                }

                if (string.IsNullOrEmpty(tfaRequest.TwoFactorCode))
                {
                    return CreateValidationProblem("RequiresTwoFactor",
                        localizer["RequiresTwoFactor"]);
                }

                if (!await userManager.VerifyTwoFactorTokenAsync(user,
                        userManager.Options.Tokens.AuthenticatorTokenProvider, tfaRequest.TwoFactorCode))
                {
                    return CreateValidationProblem("InvalidTwoFactorCode",
                        localizer["InvalidTwoFactorCode"]);
                }

                await userManager.SetTwoFactorEnabledAsync(user, true);
            }
            else if (tfaRequest.Enable == false || tfaRequest.ResetSharedKey)
            {
                await userManager.SetTwoFactorEnabledAsync(user, false);
            }

            if (tfaRequest.ResetSharedKey)
            {
                await userManager.ResetAuthenticatorKeyAsync(user);
            }

            string[]? recoveryCodes = null;
            if (tfaRequest.ResetRecoveryCodes ||
                (tfaRequest.Enable == true && await userManager.CountRecoveryCodesAsync(user) == 0))
            {
                var recoveryCodesEnumerable = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
                recoveryCodes = recoveryCodesEnumerable?.ToArray();
            }

            if (tfaRequest.ForgetMachine)
            {
                await signInManager.ForgetTwoFactorClientAsync();
            }

            var key = await userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(key))
            {
                await userManager.ResetAuthenticatorKeyAsync(user);
                key = await userManager.GetAuthenticatorKeyAsync(user);

                if (string.IsNullOrEmpty(key))
                {
                    throw new NotSupportedException(localizer[UnableToCreateNewKey]);
                }
            }

            return TypedResults.Ok(new TwoFactorResponse
            {
                SharedKey = key,
                RecoveryCodes = recoveryCodes,
                RecoveryCodesLeft = recoveryCodes?.Length ?? await userManager.CountRecoveryCodesAsync(user),
                IsTwoFactorEnabled = await userManager.GetTwoFactorEnabledAsync(user),
                IsMachineRemembered = await signInManager.IsTwoFactorClientRememberedAsync(user),
            });
        }

        public static async Task<Results<Ok<InfoResponse>, ValidationProblem, NotFound>> UserInfo<TUser>(
            ClaimsPrincipal claimsPrincipal, [FromServices] IServiceProvider sp,
            [FromServices] IStringLocalizer<LoginHandler> localizer) where TUser : class
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();
            if (await userManager.GetUserAsync(claimsPrincipal) is not
                {
                }
                user)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(await CreateInfoResponseAsync(user, userManager, localizer));
        }

        public static async Task<Results<Ok<InfoResponse>, ValidationProblem, NotFound>> UpdateUserInfo<TUser>(
            ClaimsPrincipal claimsPrincipal, [FromBody] InfoRequest infoRequest, HttpContext context,
            [FromServices] IServiceProvider sp, [FromServices] LinkGenerator linkGenerator,
            [FromServices] IEmailSender<TUser> emailSender, IStringLocalizer<LoginHandler> localizer)
            where TUser : class
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();
            if (await userManager.GetUserAsync(claimsPrincipal) is not
                {
                }
                user)
            {
                return TypedResults.NotFound();
            }

            if (!string.IsNullOrEmpty(infoRequest.NewEmail) && !_emailAddressAttribute.IsValid(infoRequest.NewEmail))
            {
                return CreateValidationProblem(
                    IdentityResult.Failed(userManager.ErrorDescriber.InvalidEmail(infoRequest.NewEmail)));
            }

            if (!string.IsNullOrEmpty(infoRequest.NewPassword))
            {
                if (string.IsNullOrEmpty(infoRequest.OldPassword))
                {
                    return CreateValidationProblem("OldPasswordRequired",
                        localizer["OldPasswordRequired"]);
                }

                var changePasswordResult =
                    await userManager.ChangePasswordAsync(user, infoRequest.OldPassword, infoRequest.NewPassword);
                if (!changePasswordResult.Succeeded)
                {
                    return CreateValidationProblem(changePasswordResult);
                }
            }

            if (!string.IsNullOrEmpty(infoRequest.NewEmail))
            {
                var email = await userManager.GetEmailAsync(user);

                if (email != infoRequest.NewEmail)
                {
                    await SendConfirmationEmailAsync(user, userManager, context, infoRequest.NewEmail, linkGenerator,
                        emailSender, localizer, isChange: true);
                }
            }

            return TypedResults.Ok(await CreateInfoResponseAsync(user, userManager, localizer));
        }

        private static async Task SendConfirmationEmailAsync<TUser>(TUser user, UserManager<TUser> userManager,
            HttpContext context, string email, LinkGenerator linkGenerator, IEmailSender<TUser> emailSender,
            IStringLocalizer<LoginHandler> localizer, bool isChange = false) where TUser : class
        {
            if (confirmEmailEndpointName is null)
            {
                throw new NotSupportedException(localizer[NoConfirmationEndpointRegistered]);
            }

            var code = isChange
                ? await userManager.GenerateChangeEmailTokenAsync(user, email)
                : await userManager.GenerateEmailConfirmationTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            var userId = await userManager.GetUserIdAsync(user);
            var routeValues = new RouteValueDictionary()
            {
                ["userId"] = userId,
                ["code"] = code,
            };

            if (isChange)
            {
                // This is validated by the /confirmEmail endpoint on change.
                routeValues.Add("changedEmail", email);
            }

            var confirmEmailUrl = linkGenerator.GetUriByName(context, confirmEmailEndpointName, routeValues)
                                  ?? throw new NotSupportedException(localizer[CouldNotFindConfirmEndpoint,
                                      confirmEmailEndpointName]);

            await emailSender.SendConfirmationLinkAsync(user, email, HtmlEncoder.Default.Encode(confirmEmailUrl));
        }

        private static ValidationProblem CreateValidationProblem(string errorCode, string errorDescription) =>
            TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                { errorCode, [errorDescription] }
            });

        private static ValidationProblem CreateValidationProblem(IdentityResult result)
        {
            // We expect a single error code and description in the normal case.
            // This could be golfed with GroupBy and ToDictionary, but perf! :P
            Debug.Assert(!result.Succeeded);
            var errorDictionary = new Dictionary<string, string[]>(1);

            foreach (var error in result.Errors)
            {
                string[] newDescriptions;

                if (errorDictionary.TryGetValue(error.Code, out var descriptions))
                {
                    newDescriptions = new string[descriptions.Length + 1];
                    Array.Copy(descriptions, newDescriptions, descriptions.Length);
                    newDescriptions[descriptions.Length] = error.Description;
                }
                else
                {
                    newDescriptions = [error.Description];
                }

                errorDictionary[error.Code] = newDescriptions;
            }

            return TypedResults.ValidationProblem(errorDictionary);
        }

        private static async Task<InfoResponse> CreateInfoResponseAsync<TUser>(TUser user,
            UserManager<TUser> userManager, IStringLocalizer<LoginHandler> localizer)
            where TUser : class
        {
            return new()
            {
                Email = await userManager.GetEmailAsync(user) ??
                        throw new NotSupportedException(localizer[UserMustHaveMail]),
                IsEmailConfirmed = await userManager.IsEmailConfirmedAsync(user),
            };
        }

        // Wrap RouteGroupBuilder with a non-public type to avoid a potential future behavioral breaking change.
        public static void PublishConfirmEndpoint(string confirmEmailEndpointName)
        {
            if (!string.IsNullOrEmpty(LoginHandler.confirmEmailEndpointName))
            {
                throw new InvalidOperationException("Confirm-Endpoint was already set!");
            }

            LoginHandler.confirmEmailEndpointName = confirmEmailEndpointName;
        }
    }
}