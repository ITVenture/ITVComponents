using System;
using ITVComponents.Logging;
using ITVComponents.Settings.Native;
using ITVComponents.WebCoreToolkit.AspExtensions;
using ITVComponents.WebCoreToolkit.AspExtensions.Impl;
using ITVComponents.WebCoreToolkit.Extensions;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account;
using ITVComponents.WebCoreToolkit.IdentityShared.Areas.Identity.Pages.Account.Manage;
using ITVComponents.WebCoreToolkit.IdentityShared.Extensions;
using ITVComponents.WebCoreToolkit.IdentityShared.Helpers;
using ITVComponents.WebCoreToolkit.IdentityShared.Options;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Impl;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Impl.Generic;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage.Impl;
using ITVComponents.WebCoreToolkit.IdentityShared.PageHandlers.Identity.Account.Manage.Impl.Generic;
using ITVComponents.WebCoreToolkit.IdentityShared.Services;
using ITVComponents.WebCoreToolkit.IdentityShared.Services.Impl;
using ITVComponents.WebCoreToolkit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ITVComponents.WebCoreToolkit.IdentityShared
{
    [WebPart]
    public static class WebPartInit
    {
        [LoadWebPartConfig]
        public static object LoadOptions(IConfiguration config, string path)
        {
            return config.GetSection<IdentityUiOptions>(path);
        }

        [ServiceRegistrationMethod]
        public static void Register(IServiceCollection services, IdentityUiOptions options)
        {
            Type tuserType = null;
            if (options.UseDefaultIdentityUserGuard && !string.IsNullOrEmpty(options.IdentityUserType))
            {
                var t = Type.GetType(options.IdentityUserType);
                if (t != null)
                {
                    try
                    {
                        var guardType = typeof(IdentityUserGuard<>).MakeGenericType(t);
                        var serviceType = typeof(UserGuard<>).MakeGenericType(t);
                        services.AddTransient(serviceType, guardType);
                        tuserType = t;
                    }
                    catch (Exception ex)
                    {
                        LogEnvironment.LogEvent($"{t} is not an IdentityUserType. ({ex.Message})", LogSeverity.Error);
                    }
                }
            }

            services.Configure<LoginOptions>(op =>
            {
                op.UserNameIsEmail = options.UserNameIsEmail;
                op.UseLocalAccounts = options.UseLocalAccounts;
                op.RegistrationPage = options.RegistrationPage;
                if (options.UseExternalLogins)
                {
                    op.ExternalLoginPage = options.ExternalLoginPage;
                }
            });

            if (!options.RegisterPageHandlers)
            {
                services.ConfigurePageModelHandlerFactory(ha =>
                {
                    ha.ConfigureHandlerType<AccessDeniedModel, IAccessDeniedHandler, AccessDeniedHandler>(false);
                    ha.ConfigureHandlerType<ConfirmEmailModel, IConfirmEmailHandler, ConfirmEmailHandler>(false);
                    ha.ConfigureHandlerType<ConfirmEmailChangeModel, IConfirmEmailChangeHandler, ConfirmEmailChangeHandler>(false);
                    ha.ConfigureHandlerType<ExternalLoginModel, IExternalLoginHandler, ExternalLoginHandler>(false);
                    ha.ConfigureHandlerType<ForgotPasswordModel, IForgotPasswordHandler, ForgotPasswordHandler>(false);
                    ha.ConfigureHandlerType<LoginModel, ILoginHandler, LoginHandler>(false);
                    ha.ConfigureHandlerType<LoginWith2faModel, ILoginWith2faHandler, LoginWith2faHandler>(false);
                    ha.ConfigureHandlerType<LoginWithRecoveryCodeModel, ILoginWithRecoveryCodeHandler, LoginWithRecoveryCodeHandler>(false);
                    ha.ConfigureHandlerType<LogoutModel, ILogoutHandler, LogoutHandler>(false);
                    ha.ConfigureHandlerType<ResendEmailConfirmationModel, IResendEmailConfirmationHandler, ResendEmailConfirmationHandler>(false);
                    ha.ConfigureHandlerType<ResetPasswordModel, IResetPasswordHandler, ResetPasswordHandler>(false);
                    ha.ConfigureHandlerType<ChangePasswordModel, IChangePasswordHandler, ChangePasswordHandler>(false);
                    ha.ConfigureHandlerType<DeletePersonalDataModel, IDeletePersonalDataHandler, DeletePersonalDataHandler>(false);
                    ha.ConfigureHandlerType<Disable2faModel, IDisable2faHandler, Disable2faHandler>(false);
                    ha.ConfigureHandlerType<DownloadPersonalDataModel, IDownloadPersonalDataHandler, DownloadPersonalDataHandler>(false);
                    ha.ConfigureHandlerType<EmailModel, IEmailHandler, EmailHandler>(false);
                    ha.ConfigureHandlerType<EnableAuthenticatorModel, IEnableAuthenticatorHandler, EnableAuthenticatorHandler>(false);
                    ha.ConfigureHandlerType<ExternalLoginsModel, IExternalLoginsHandler, ExternalLoginsHandler>(false);
                    ha.ConfigureHandlerType<GenerateRecoveryCodesModel, IGenerateRecoveryCodesHandler, GenerateRecoveryCodesHandler>(false);
                    ha.ConfigureHandlerType<IndexModel, IIndexHandler, IndexHandler>(false);
                    ha.ConfigureHandlerType<PersonalDataModel, IPersonalDataHandler, PersonalDataHandler>(false);
                    ha.ConfigureHandlerType<ResetAuthenticatorModel, IResetAuthenticatorHandler, ResetAuthenticatorHandler>(false);
                    ha.ConfigureHandlerType<SetPasswordModel, ISetPasswordHandler, SetPasswordHandler>(false);
                    ha.ConfigureHandlerType<TwoFactorAuthenticationModel, ITwoFactorAuthenticationHandler, TwoFactorAuthenticationHandler>(false);
                });
            }
            else
            {
                services.ConfigurePageModelHandlerFactory(ha =>
                {
                    ha.ConfigureHandlerType<AccessDeniedModel, IAccessDeniedHandler, AccessDeniedHandler>(false);
                    ha.ConfigureHandlerType(typeof(ConfirmEmailModel), typeof(IConfirmEmailHandler), typeof(ConfirmEmailHandler<>),false);
                    ha.ConfigureHandlerType(typeof(ConfirmEmailChangeModel),typeof(IConfirmEmailChangeHandler),typeof(ConfirmEmailChangeHandler<>),false);
                    ha.ConfigureHandlerType(typeof(ExternalLoginModel), typeof(IExternalLoginHandler), typeof(ExternalLoginHandler<>),false);
                    ha.ConfigureHandlerType(typeof(ForgotPasswordModel), typeof(IForgotPasswordHandler), typeof(ForgotPasswordHandler<>),false);
                    ha.ConfigureHandlerType(typeof(LoginModel), typeof(ILoginHandler), typeof(LoginHandler<>), false);
                    ha.ConfigureHandlerType(typeof(LoginWith2faModel), typeof(ILoginWith2faHandler), typeof(LoginWith2faHandler<>),false);
                    ha.ConfigureHandlerType(typeof(LoginWithRecoveryCodeModel), typeof(ILoginWithRecoveryCodeHandler), typeof(LoginWithRecoveryCodeHandler<>),false);
                    ha.ConfigureHandlerType(typeof(LogoutModel), typeof(ILogoutHandler), typeof(LogoutHandler<>),false);
                    ha.ConfigureHandlerType(typeof(ResendEmailConfirmationModel), typeof(IResendEmailConfirmationHandler), typeof(ResendEmailConfirmationHandler<>),false);
                    ha.ConfigureHandlerType(typeof(ResetPasswordModel), typeof(IResetPasswordHandler), typeof(ResetPasswordHandler<>),false);
                    ha.ConfigureHandlerType(typeof(ChangePasswordModel), typeof(IChangePasswordHandler), typeof(ChangePasswordHandler<>),false);
                    ha.ConfigureHandlerType(typeof(DeletePersonalDataModel), typeof(IDeletePersonalDataHandler), typeof(DeletePersonalDataHandler<>),false);
                    ha.ConfigureHandlerType(typeof(Disable2faModel), typeof(IDisable2faHandler), typeof(Disable2faHandler<>),false);
                    ha.ConfigureHandlerType(typeof(DownloadPersonalDataModel), typeof(IDownloadPersonalDataHandler), typeof(DownloadPersonalDataHandler<>),false);
                    ha.ConfigureHandlerType(typeof(EmailModel), typeof(IEmailHandler), typeof(EmailHandler<>),false);
                    ha.ConfigureHandlerType(typeof(EnableAuthenticatorModel), typeof(IEnableAuthenticatorHandler), typeof(EnableAuthenticatorHandler<>),false);
                    ha.ConfigureHandlerType(typeof(ExternalLoginsModel), typeof(IExternalLoginsHandler), typeof(ExternalLoginsHandler<>),false);
                    ha.ConfigureHandlerType(typeof(GenerateRecoveryCodesModel), typeof(IGenerateRecoveryCodesHandler), typeof(GenerateRecoveryCodesHandler<>),false);
                    ha.ConfigureHandlerType(typeof(IndexModel), typeof(IIndexHandler), typeof(IndexHandler<>),false);
                    ha.ConfigureHandlerType(typeof(PersonalDataModel), typeof(IPersonalDataHandler), typeof(PersonalDataHandler<>),false);
                    ha.ConfigureHandlerType(typeof(ResetAuthenticatorModel), typeof(IResetAuthenticatorHandler), typeof(ResetAuthenticatorHandler<>),false);
                    ha.ConfigureHandlerType(typeof(SetPasswordModel), typeof(ISetPasswordHandler), typeof(SetPasswordHandler<>),false);
                    ha.ConfigureHandlerType(typeof(TwoFactorAuthenticationModel), typeof(ITwoFactorAuthenticationHandler), typeof(TwoFactorAuthenticationHandler<>),false);
                    if (tuserType != null)
                    {
                        ha.ConfigureGenericArgument("TUser", tuserType);
                    }
                });
            }

            if (options.UseDefaultPageSet || options.ManagementPages.Count != 0)
            {
                services.ConfigureIdentityPages(p =>
                {
                    if (options.UseDefaultPageSet)
                    {
                        p.AddDefaultPages(options.UseExternalLogins);
                    }

                    if (options.ManagementPages.Count != 0)
                    {
                        foreach (var page in options.ManagementPages)
                        {
                            var tmp = ManagementNavDefaults.FromNavPageDefinition(page);
                            bool ok = false;
                            if (!string.IsNullOrEmpty(page.AddBefore))
                            {
                                ok = p.InsertBefore(tmp, page.AddBefore);
                            }
                            else if (!string.IsNullOrEmpty(page.AddAfter))
                            {
                                ok = p.InsertAfter(tmp, page.AddAfter);
                            }
                            
                            if (!ok)
                            {
                                p.AddNavPage(tmp);
                            }
                        }
                    }
                });
            }

            if (options.UseDefaultIdentityNavigator)
            {
                services.AddScoped<IManageNavigator, ManageNavPages>();
            }

            if (options.UseDefaultMailSender)
            {
                services.AddSingleton<IEmailSender, DefaultMailSender>();
                // Generic transactional-mail abstraction for non-identity flows (e.g. tenant invitations);
                // contains the Identity-UI mail dependency in this assembly.
                services.AddScoped<IAppMailSender, AppMailSender>();
            }

            // Confirmation mailer for account-creating flows outside the identity pages (e.g. direct
            // onboarding). Generic over the configured user type so callers depend only on the abstraction.
            if (tuserType != null)
            {
                services.AddScoped(typeof(IAccountConfirmationMailer),
                    typeof(AccountConfirmationMailer<>).MakeGenericType(tuserType));
            }
        }
    }
}
