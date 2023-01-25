using System;

namespace ITVComponents.EFRepo.DbContextConfig.Expressions
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ExpressionPropertyRedirectAttribute:Attribute
    {
        public string ReplacerName { get; }

        public ExpressionPropertyRedirectAttribute(string replacerName)
        {
            ReplacerName = replacerName;
        }
    }
}
