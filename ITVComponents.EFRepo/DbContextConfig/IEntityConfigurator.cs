using Microsoft.EntityFrameworkCore;

namespace ITVComponents.EFRepo.DbContextConfig
{
    public interface IEntityConfigurator
    {
        void ConfigureEntity(ModelBuilder modelBuilder);
    }
}
