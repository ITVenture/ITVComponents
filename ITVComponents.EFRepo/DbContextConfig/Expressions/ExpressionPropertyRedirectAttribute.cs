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

    [AttributeUsage(AttributeTargets.Method)]
    public class ExpressionMethodRedirectAttribute : Attribute
    {
        public string ReplacerName { get; }

        public ExpressionMethodRedirectAttribute(string replacerName)
        {
            ReplacerName = replacerName;
        }
    }
}
