using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurityShared.SyntaxHelper
{
    public interface ICalculatedColumnsSyntaxProvider
    {
        PropertyBuilder<T> WithCalculatedPropert<T>(PropertyBuilder<T> property);
    }
}
