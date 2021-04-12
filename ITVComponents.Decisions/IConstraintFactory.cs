using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ITVComponents.Decisions.FactoryHelpers;
using ITVComponents.Plugins;

namespace ITVComponents.Decisions
{
    public interface IConstraintFactory: IPlugin
    {
        void RegisterType(ConstraintConstructor constructor);

        IConstraint<T> GetConstraint<T>(string identifier, Dictionary<string, object> namedParameters) where T : class;

        IConstraint<T> GetConstraint<T>(string identifier, Func<string, Type, object> valueCallback) where T : class;

        IConstraint GetConstraint(Type targetType, string identifier, Dictionary<string, object> namedParameters);

        IConstraint GetConstraint(Type targetType, string identifier, Func<string, Type, object> valueCallback);

        IDecider CreateDecider(Type targetType, bool contextDriven);
    }
}
