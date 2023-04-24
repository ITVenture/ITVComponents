using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.EFRepo.ModelCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ITVComponents.EFRepo.DIIntegration
{
    public class CustomModelCache:IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
        {
            var modelExtensionTag = "DEFAULT";
            if (context is ICustomModelIdProvider mip)
            {
                modelExtensionTag = mip.ModelCacheExtensionLabel;
            }

            return (context.GetType(), modelExtensionTag, designTime);
        }
    }
}
