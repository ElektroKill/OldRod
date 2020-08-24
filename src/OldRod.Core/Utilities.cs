using AsmResolver.DotNet;

namespace OldRod.Core
{
    internal static class Utilities
    {
        public static bool RequiresSpecialAccess(this ITypeDefOrRef type)
        {
            var typeDef = type.Resolve();
            while (typeDef != null)
            {
                if (typeDef.IsNestedPrivate
                    || typeDef.IsNestedFamily
                    || typeDef.IsNestedFamilyAndAssembly
                    || typeDef.IsNestedFamilyOrAssembly)
                    return true;
                typeDef = typeDef.DeclaringType;
            }

            return false;
        }
    }
}