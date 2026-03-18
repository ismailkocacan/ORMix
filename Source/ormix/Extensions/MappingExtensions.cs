using java.sql;
using System;
using System.Dynamic;
using System.Reflection;
using System.Text;

namespace Ormix.Extensions
{
    public static class MappingExtensions
    {
        static MappingExtensions()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public static void Map(this ResultSet resultSet, ResultSetMetaData resultSetMetaData, ref ExpandoObject instance)
        {
            int columnCount = resultSetMetaData.getColumnCount();
            for (int columnIndex = 1; columnIndex <= columnCount; columnIndex++)
            {
                var columnName = resultSetMetaData.getColumnLabel(columnIndex);

                switch (resultSetMetaData.getColumnType(columnIndex))
                {
                    case Types.BIT:
                        instance.TryAdd(columnName, resultSet.getByte(columnIndex));
                        break;

                    case Types.TINYINT:
                        instance.TryAdd(columnName, resultSet.getByte(columnIndex));
                        break;

                    case Types.SMALLINT:
                        instance.TryAdd(columnName, resultSet.getShort(columnIndex));
                        break;

                    case Types.INTEGER:
                        instance.TryAdd(columnName, resultSet.getInt(columnIndex));
                        break;

                    case Types.BIGINT:
                        instance.TryAdd(columnName, resultSet.getLong(columnIndex));
                        break;

                    case Types.FLOAT:
                        instance.TryAdd(columnName, resultSet.getFloat(columnIndex));
                        break;

                    case Types.REAL:
                        instance.TryAdd(columnName, resultSet.getFloat(columnIndex));
                        break;

                    case Types.DOUBLE:
                        instance.TryAdd(columnName, resultSet.getFloat(columnIndex));
                        break;

                    case Types.NUMERIC:
                        instance.TryAdd(columnName, resultSet.getFloat(columnIndex));
                        break;

                    case Types.DECIMAL:
                        instance.TryAdd(columnName, resultSet.getFloat(columnIndex));
                        break;

                    case Types.CHAR:
                    case Types.VARCHAR:
                    case Types.LONGVARCHAR:
                    case Types.LONGNVARCHAR:
                    case Types.NCHAR:
                    case Types.NVARCHAR:
                        {
                            var objValue = resultSet.ReadString(resultSetMetaData, columnIndex);
                            instance.TryAdd(columnName, objValue == null ? null : objValue.ToString().Trim());
                        }
                        break;

                    case Types.DATE:
                        instance.TryAdd(columnName, resultSet.GetDateTime(columnIndex));
                        break;

                    case Types.BINARY:
                    case Types.VARBINARY:
                    case Types.LONGVARBINARY:
                        instance.TryAdd(columnName, resultSet.getBytes(columnIndex));
                        break;

                    case Types.BOOLEAN:
                        instance.TryAdd(columnName, resultSet.getBoolean(columnIndex));
                        break;

                    case Types.TIME_WITH_TIMEZONE:
                        instance.TryAdd(columnName, resultSet.getTime(columnIndex));
                        break;

                    case Types.TIMESTAMP_WITH_TIMEZONE:
                        instance.TryAdd(columnName, resultSet.getTime(columnIndex));
                        break;

                    /* Bu turler handle edilmedi, goruyorsun zaten :) */
                    case Types.TIME:
                    case Types.TIMESTAMP:
                    case Types.NULL:
                    case Types.OTHER:
                    case Types.JAVA_OBJECT:
                    case Types.DISTINCT:
                    case Types.STRUCT:
                    case Types.ARRAY:
                    case Types.BLOB:
                    case Types.CLOB:
                    case Types.REF:
                    case Types.DATALINK:
                    case Types.ROWID:
                    case Types.NCLOB:
                    case Types.SQLXML:
                    case Types.REF_CURSOR:
                        break;

                    default:
                        break;
                }
            }
        }

        public static void Map<T>(this ResultSet resultSet, ResultSetMetaData resultSetMetaData, ref T instance, Type typeOfT)
        {
            var typeOfGuid = typeof(Guid);
            var typeOfGuidNullable = typeof(Guid);

            for (int columnIndex = 1; columnIndex <= resultSetMetaData.getColumnCount(); columnIndex++)
            {
                var columnName = resultSetMetaData.getColumnLabel(columnIndex);
                var property = typeOfT?.GetProperty(columnName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (property == null)
                    continue;


                // Enum ve Nullable<EnumTuru> propertylerin handle edilmesi.
                if (property.PropertyType.IsEnumType(out Type? enumType) && enumType != null)
                {
                    var value = resultSet.ReadEnum(resultSetMetaData, columnIndex, enumType, columnName);
                    property.SetValue(instance, value);
                    continue;
                }



                bool isGuid = property.PropertyType == typeOfGuid;
                bool isGuidNullable = property.PropertyType == typeOfGuidNullable;
                if (isGuid || isGuidNullable)
                {
                    var value = resultSet.ReadString(resultSetMetaData, columnIndex);
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        if (isGuidNullable)
                            property.SetValue(instance, null);
                    }
                    else
                    {
                        if (Guid.TryParse(value, out Guid guid))
                            property.SetValue(instance, guid);
                    }
                    continue;
                }

                TypeCode typeCodeOfProperty = Type.GetTypeCode(property.PropertyType);
            ReTypeCodeIdenficationOfNullableTypes:
                switch (typeCodeOfProperty)
                {
                    case TypeCode.Boolean:
                        break;
                    case TypeCode.Byte:
                        property.SetValue(instance, resultSet.getByte(columnIndex));
                        break;
                    case TypeCode.Char:
                        break;
                    case TypeCode.DBNull:
                        break;
                    case TypeCode.DateTime:
                        property.SetValue(instance, resultSet.GetDateTime(columnIndex, columnName));
                        break;
                    case TypeCode.Decimal:
                        property.SetValue(instance, (decimal)resultSet.getDouble(columnIndex));
                        break;
                    case TypeCode.Double:
                        property.SetValue(instance, (decimal)resultSet.getDouble(columnIndex));
                        break;
                    case TypeCode.Empty:
                        break;
                    case TypeCode.Int16:
                        property.SetValue(instance, resultSet.getShort(columnIndex));
                        break;
                    case TypeCode.Int32:
                        property.SetValue(instance, resultSet.getInt(columnIndex));
                        break;
                    case TypeCode.Int64:
                        property.SetValue(instance, resultSet.getLong(columnIndex));
                        break;
                    case TypeCode.Object:
                        {
                            if (property.PropertyType.IsArray)
                            {
                                if (Type.GetTypeCode(property.PropertyType.GetElementType()) == TypeCode.Byte)
                                {
                                    byte[] bytes = resultSet.getBytes(columnIndex);
                                    property.SetValue(instance, bytes);
                                }
                            }
                            else
                            {
                                if (property.PropertyType.TryFindTypeCodeOfNullableType(out TypeCode? typeCode) && typeCode != null)
                                {
                                    typeCodeOfProperty = typeCode.Value;
                                    goto ReTypeCodeIdenficationOfNullableTypes;
                                }
                            }
                        }
                        break;
                    case TypeCode.SByte:
                        break;
                    case TypeCode.Single:
                        break;
                    case TypeCode.String:
                        {
                            var value = resultSet.ReadString(resultSetMetaData, columnIndex);
                            property.SetValue(instance, value == null ? null : value.Trim());
                        }
                        break;
                    case TypeCode.UInt16:
                        break;
                    case TypeCode.UInt32:
                        break;
                    case TypeCode.UInt64:
                        break;
                    default:
#if DEBUG && ENABLE_EXCEPTIONS
                        throw new NotImplementedException(property.PropertyType.FullName);
#endif
                        break;
                }
            }
        }

        #region Private Methods
        private static string ConvertToUtf8String(byte[] srcData)
        {
            if (srcData == null)
                return string.Empty;

            if (srcData.Length == 0)
                return string.Empty;

            Encoding windows1254Encoding = Encoding.GetEncoding(1254);
            byte[] utf8Bytes = Encoding.Convert(windows1254Encoding, Encoding.UTF8,
                srcData);
            return Encoding.UTF8.GetString(utf8Bytes);
        }

        private static bool IsTextField(this ResultSetMetaData resultSetMetaData, int columnIndex)
        {
            return resultSetMetaData.getColumnTypeName(columnIndex) == "text";
        }

        private static bool IsEnumType(this Type propertyType, out Type? enumType)
        {
            if (propertyType.IsEnum)
            {
                enumType = propertyType;
                return true;
            }

            if (propertyType.IsNullableEnum(out Type? typeOfNullableEnum))
            {
                enumType = typeOfNullableEnum;
                return true;
            }

            enumType = null;
            return false;
        }


        private static bool IsNullableEnum(this Type type, out Type? typeOfNullableEnum)
        {
            Type? underlyingType = Nullable.GetUnderlyingType(type);
            bool isNullableEnum = underlyingType != null && underlyingType.IsEnum;
            typeOfNullableEnum = isNullableEnum ? underlyingType : null;
            return isNullableEnum;
        }


        private static string? ReadString(this ResultSet resultSet, ResultSetMetaData resultSetMetaData, int columnIndex)
        {
            /* Kaptanin Seyir Defteri :
               Field TEXT turunde ise clob ile ozel okuma muamelesi, 
               degilse bildigin gibi okumaya devam et.
            */
            return resultSetMetaData.IsTextField(columnIndex) ?
                resultSet.ReadTextFieldTypeAsClob(columnIndex) :
                resultSet.getString(columnIndex);
        }

        private static object? ReadEnum(this ResultSet resultSet, ResultSetMetaData resultSetMetaData, int columnIndex, Type enumType, string columnName)
        {
            var value = resultSet.ReadString(resultSetMetaData, columnIndex);
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var isDefined = Enum.TryParse(enumType, value, true, out object? enumValue);
            if (!isDefined)
                throw new InvalidDataException($"Alan: {columnName} için {value} değeri {enumType.FullName} içerisinde tanımlı değil!");

            return enumValue;
        }

        private static string ReadTextFieldTypeAsClob(this ResultSet resultSet, int columnIndex)
        {
            var clob = resultSet.getClob(columnIndex);
            if (clob == null)
                return string.Empty;

            long length = clob.length();
            if (length == 0)
                return string.Empty;

            var clobArray = new byte[length];
            using var asciiStream = clob.getAsciiStream();

            int offset = 0;
            int value = 0;
            do
                value = asciiStream.read(b: clobArray,
                                         off: offset,
                                         len: (int)(length - offset));
            while (value != -1);

            return ConvertToUtf8String(clobArray);
        }

        #endregion
    }
}