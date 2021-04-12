using System;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Extensions;
using Microsoft.AspNetCore.Authorization;

namespace ITVComponents.WebCoreToolkit.Security.PermissionHandling
{
    /// <summary>
    /// Evaluates the authorisation for the AssignedPermission requirement
    /// </summary>
    public class AssignedPermissionsHandler:AuthorizationHandler<AssignedPermissionRequirement>
    {
        private readonly IServiceProvider currentServices;

        /// <summary>
        /// Initializes a new instance of the AssignedPermissionsHandler class
        /// </summary>
        /// <param name="currentServices">the available services for the current context</param>
        public AssignedPermissionsHandler(IServiceProvider currentServices)
        {
            this.currentServices = currentServices;
        }

        /// <summary>
        /// Makes a decision if authorization is allowed based on a specific requirement.
        /// </summary>
        /// <param name="context">The authorization context.</param>
        /// <param name="requirement">The requirement to evaluate.</param>
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AssignedPermissionRequirement requirement)
        {
            //var permissions = context.User.GetUserPermissions();//permissionEstimator.EstimatePermissions(context.User);
            if (currentServices.VerifyUserPermissions(requirement.RequiredPermissions))
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }

            return Task.CompletedTask;
        }
    }
}
