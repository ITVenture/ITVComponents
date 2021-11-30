using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.WebCoreToolkit.Configuration.Impl
{
    internal class HierarchySettingsImpl<TSettings>:IHierarchySettings<TSettings> where TSettings:class, new()
    {
        private readonly IScopedSettings<TSettings> scoped;
        private readonly IGlobalSettings<TSettings> global;
        private TSettings valueOrDefault;
        private TSettings value;

        public HierarchySettingsImpl(IScopedSettings<TSettings> scoped, IGlobalSettings<TSettings> global)
        {
            this.scoped = scoped;
            this.global = global;
        }

        public TSettings Value => value ??= ValueOrDefault ?? new TSettings();
        public TSettings ValueOrDefault => valueOrDefault??=scoped?.ValueOrDefault ?? global?.ValueOrDefault;
    }
}
