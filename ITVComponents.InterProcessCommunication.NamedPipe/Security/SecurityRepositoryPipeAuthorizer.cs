using System;
using System.Linq;
using System.Security.Claims;
using ITVComponents.GenericService.ServiceSecurity;
using ITVComponents.WebCoreToolkit.Security;

namespace ITVComponents.InterProcessCommunication.NamedPipe.Security
{
    /// <summary>
    /// Authorizes pipe clients against a security repository - by default the users, roles and permissions in the
    /// service's configuration (<see cref="JsonSettingsSecurityRepository"/>), as the other authenticated hubs do.
    /// </summary>
    /// <remarks>
    /// The authentication type of a pipe client is <see cref="PipeClaimTypes.AuthenticationType"/>; a configured user
    /// without authentication type applies to every kind of access.
    /// </remarks>
    public class SecurityRepositoryPipeAuthorizer : IPipeHubAuthorizer
    {
        private readonly ISecurityRepository securityRepository;
        private readonly IUserNameMapper userNameMapper;

        /// <summary>
        /// Authorizes against the users, roles and permissions in the service's configuration.
        /// </summary>
        public SecurityRepositoryPipeAuthorizer()
            : this(new JsonSettingsSecurityRepository(), new PipeUserNameMapper())
        {
        }

        /// <summary>
        /// Authorizes against the given repository.
        /// </summary>
        /// <param name="securityRepository">the users, roles and permissions</param>
        /// <param name="userNameMapper">turns an identity into the labels the repository knows</param>
        public SecurityRepositoryPipeAuthorizer(ISecurityRepository securityRepository, IUserNameMapper userNameMapper)
        {
            this.securityRepository = securityRepository ?? throw new ArgumentNullException(nameof(securityRepository));
            this.userNameMapper = userNameMapper ?? throw new ArgumentNullException(nameof(userNameMapper));
        }

        /// <inheritdoc />
        public bool IsAuthenticated(ClaimsIdentity caller)
        {
            return securityRepository.IsAuthenticated(userNameMapper.GetUserLabels(caller), caller.AuthenticationType);
        }

        /// <inheritdoc />
        public bool HasAnyPermission(ClaimsIdentity caller, params string[] permissions)
        {
            var labels = userNameMapper.GetUserLabels(caller);
            if (!securityRepository.IsAuthenticated(labels, caller.AuthenticationType))
            {
                return false;
            }

            var granted = securityRepository.GetPermissions(labels, caller.AuthenticationType).Select(p => p.PermissionName);
            return permissions.Length == 0 || granted.Any(g => permissions.Contains(g, StringComparer.OrdinalIgnoreCase));
        }
    }
}
