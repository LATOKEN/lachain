using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Lachain.Utility
{
    public static class AttributeScanner
    {
        public static IEnumerable<Tuple<MethodInfo, TA>> FindAttributes<TA>(this Type classType)
            where TA : Attribute
        {
            var attributeUsage = typeof(TA).GetCustomAttribute<AttributeUsageAttribute>();
            if (attributeUsage is null)
                throw new ArgumentException("Unable to find attribute usage attribute in class (" + classType + ")", nameof(classType));
            switch (attributeUsage.ValidOn)
            {
                case AttributeTargets.Method:
                    return FindMethodAttributes<TA>(classType);
                case AttributeTargets.All:
                case AttributeTargets.Assembly:
                case AttributeTargets.Class:
                case AttributeTargets.Constructor:
                case AttributeTargets.Delegate:
                case AttributeTargets.Enum:
                case AttributeTargets.Event:
                case AttributeTargets.Field:
                case AttributeTargets.GenericParameter:
                case AttributeTargets.Interface:
                case AttributeTargets.Module:
                case AttributeTargets.Parameter:
                case AttributeTargets.Property:
                case AttributeTargets.ReturnValue:
                case AttributeTargets.Struct:
                    throw new ArgumentOutOfRangeException(nameof(classType), "Attribute scanner supports only method attributes");
                default:
                    throw new ArgumentOutOfRangeException(nameof(classType), "Attribute scanner supports only method attributes");
            }
        }

        public static IEnumerable<Tuple<MethodInfo, TA>> FindMethodAttributes<TA>(this Type classType)
            where TA : Attribute
        {
            return classType.GetMethods().Select(method => Tuple.Create(method, method.GetCustomAttribute<TA>())).Where(tuple => !(tuple.Item2 is null)).ToList();
        }
    }
}