using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace ITVComponents.WebCoreToolkit.Security.PermissionHandling
{
    public class FeatureActivatedRequirement: IAuthorizationRequirement
    {
        /// <summary>
        /// A list of required permissions that are required for a specific action
        /// </summary>
        public string[] RequiredFeatures{ get; }


        /// <summary>
        /// Initializes a new instance of the FeatureActivatedRequirement class
        /// </summary>
        /// <param name="requiredFeatures">a lsit of features that need to be activated</param>
        public FeatureActivatedRequirement(string[] requiredFeatures)
        {
            RequiredFeatures = requiredFeatures;
        }
    }
}
