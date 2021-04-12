using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.InterProcessCommunication.Grpc.Security;

namespace ITVComponents.InterProcessCommunication.Grpc.Extensions
{
    public static class TransferIdentityExtensions
    {
        /// <summary>
        /// Converts a transferred identity into a Claims-Identity
        /// </summary>
        /// <param name="transferred">the identity that came transferred from a remote client</param>
        /// <returns>an identity that can be used for further processing</returns>
        public static IIdentity ToIdentity(this TransferIdentity transferred)
        {
            if (transferred.IsAuthenticated)
            {
                ClaimsIdentity retVal = new ClaimsIdentity(from t in transferred.Claims select new Claim(t.Type, t.Value, t.ValueType, t.Issuer, t.OriginalIssuer),
                    transferred.AuthenticationType,
                    transferred.NameType,
                    transferred.RoleType);
                return retVal;
            }

            return null;
        }

        public static TransferIdentity ForTransfer(this ClaimsIdentity identity, string transferNameClaim = null, string transferRoleClaim = null)
        {
            var retVal = new TransferIdentity();
            retVal.NameType = transferNameClaim ?? identity.NameClaimType;
            retVal.RoleType = transferRoleClaim ?? identity.RoleClaimType;
            retVal.AuthenticationType = identity.AuthenticationType;
            retVal.IsAuthenticated = identity.IsAuthenticated;
            retVal.Label = identity.Label;
            retVal.Claims.AddRange(from t in identity.Claims
                select new TransferClaim
                {
                    Value = t.Value,
                    Issuer = t.Issuer,
                    Properties = new Dictionary<string, string>(t.Properties),
                    OriginalIssuer = t.OriginalIssuer,
                    Type = t.Type,
                    ValueType = t.ValueType
                });
            return retVal;
        }
    }
}
