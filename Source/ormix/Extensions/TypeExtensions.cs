using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ormix.Extensions
{
    public static class TypeExtensions
    {
        public static bool TryFindTypeCodeOfNullableType(this Type type, out TypeCode? typeCode)
        {
            if (type == null)
            {
                typeCode = null;
                return false;
            }

            Type? underlyingType = Nullable.GetUnderlyingType(type);
            bool isNullable = underlyingType != null;
            typeCode = Type.GetTypeCode(underlyingType);
            return isNullable;
        }
    }
}
