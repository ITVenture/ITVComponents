using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.WebCoreToolkit.Models;

namespace ITVComponents.WebCoreToolkit.Extensions
{
    public static class ClaimsIdentityExtensions
    {
        public static ClaimData[] GetClaimData(this ClaimsIdentity identity)
        {
            return (from t in identity.Claims
                select new ClaimData
                {
                    Issuer = t.Issuer,
                    OriginalIssuer = t.OriginalIssuer,
                    Type = t.Type,
                    Value = t.Value,
                    ValueType = t.ValueType
                }).ToArray();
        }
    }
}
