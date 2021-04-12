using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ITVComponents.Scripting.CScript.Core.Methods
{
    public static class FlatTypeMembers
    {
        public static MemberInfo[] GetMembers(this Type type, bool isStatic)
        {
            List<MemberInfo> retMembers = new List<MemberInfo>();
            retMembers.AddRange(GetProperties(type,isStatic));
            retMembers.AddRange(GetFields(type,isStatic));
            retMembers.AddRange(GetConstructors(type));
            retMembers.AddRange(GetEvents(type, isStatic));
            retMembers.AddRange(GetMethods(type, isStatic));
            return retMembers.ToArray();
        }

        public static PropertyInfo[] GetProperties(this Type type, bool isStatic)
        {
            if (isStatic)
            {
                return type.GetProperties(BindingFlags.GetProperty | BindingFlags.SetProperty | BindingFlags.Static |
                                          BindingFlags.Public);
            }

            if (type.IsInterface)
            {
                var ifTypes = type.GetInterfaces();
                var allProps = ifTypes.SelectMany(t =>
                    t.GetProperties(BindingFlags.GetProperty | BindingFlags.SetProperty | BindingFlags.Instance |
                                    BindingFlags.Public));
                return type.GetProperties(BindingFlags.GetProperty | BindingFlags.SetProperty | BindingFlags.Instance |
                                          BindingFlags.Public).Union(allProps).ToArray();
            }

            return type.GetProperties(BindingFlags.GetProperty | BindingFlags.SetProperty | BindingFlags.Instance |
                                      BindingFlags.Public | BindingFlags.FlattenHierarchy);
        }

        public static FieldInfo[] GetFields(this Type type, bool isStatic)
        {
            if (isStatic)
            {
                return type.GetFields(BindingFlags.GetField| BindingFlags.SetField | BindingFlags.Static |
                                          BindingFlags.Public);
            }

            if (type.IsInterface)
            {
                var ifTypes = type.GetInterfaces();
                var allProps = ifTypes.SelectMany(t =>
                    t.GetFields(BindingFlags.GetField | BindingFlags.SetField | BindingFlags.Instance |
                                    BindingFlags.Public));
                return type.GetFields(BindingFlags.GetField | BindingFlags.SetField | BindingFlags.Instance |
                                          BindingFlags.Public).Union(allProps).ToArray();
            }

            return type.GetFields(BindingFlags.GetField | BindingFlags.SetField | BindingFlags.Instance |
                                      BindingFlags.Public | BindingFlags.FlattenHierarchy);
        }

        public static ConstructorInfo[] GetConstructors(this Type type)
        {
            if (type.IsInterface)
            {
                return new ConstructorInfo[0];
            }

            return type.GetConstructors(BindingFlags.CreateInstance| BindingFlags.Public);
        }

        public static EventInfo[] GetEvents(this Type type, bool isStatic)
        {
            if (isStatic)
            {
                return type.GetEvents(BindingFlags.Public|BindingFlags.Static);
            }

            if (type.IsInterface)
            {
                var ifTypes = type.GetInterfaces();
                var allProps = ifTypes.SelectMany(t =>
                    t.GetEvents(BindingFlags.Public | BindingFlags.Instance));
                return type.GetEvents(BindingFlags.Public | BindingFlags.Instance).Union(allProps).ToArray();
            }

            return type.GetEvents(BindingFlags.Public | BindingFlags.Instance);
        }

        public static MethodInfo[] GetMethods(this Type type, bool isStatic)
        {
            if (isStatic)
            {
                return type.GetMethods(BindingFlags.InvokeMethod | BindingFlags.Static |
                                          BindingFlags.Public);
            }

            if (type.IsInterface)
            {
                var ifTypes = type.GetInterfaces();
                var allProps = ifTypes.SelectMany(t =>
                    t.GetMethods(BindingFlags.InvokeMethod | BindingFlags.Instance |
                                    BindingFlags.Public));
                return type.GetMethods(BindingFlags.InvokeMethod | BindingFlags.Instance |
                                          BindingFlags.Public).Union(allProps).ToArray();
            }

            return type.GetMethods(BindingFlags.InvokeMethod | BindingFlags.Instance |
                                      BindingFlags.Public | BindingFlags.FlattenHierarchy);
        }
    }
}
