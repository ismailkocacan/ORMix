//#define ENABLE_EXCEPTIONS
//#undef ENABLE_EXCEPTIONS

using java.sql;
using Microsoft.CodeAnalysis;
using Ormix.NamedParam;
using System.ComponentModel.DataAnnotations.Schema;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Ormix.Extensions
{
    public static class QueryExtensions
    {
        private struct StatementResultSetPair
        {
            public PreparedStatement statement { get; set; }
            public ResultSet resultSet { get; set; }
        }

        private static StatementResultSetPair CreateStatementAndResult(
            Connection connection, string sql,
            int resultSetType, int resultSetConcurrency,
            dynamic? parameters = null, int fetchSize = 100)
        {
            ArgumentNullException.ThrowIfNull(sql);

            var namedStatement = NamedParameterPreparedStatement
                .createNamedParameterPreparedStatement(connection, sql, resultSetType, resultSetConcurrency);
            //https://www.oninit.com/manual/informix/english/docs/dbdk/is40/esqlc/143.html
            namedStatement.setFetchSize(fetchSize);
            SetStatementParameters(namedStatement, parameters);
            var resultSet = namedStatement.executeQuery();
            return new StatementResultSetPair()
            {
                statement = namedStatement,
                resultSet = resultSet
            };
        }

        public static int Execute(this Connection connection, string sql, dynamic? parameters = null)
        {
            using var statement = NamedParameterPreparedStatement
                 .createNamedParameterPreparedStatement(connection, sql);
            SetStatementParameters(statement, parameters);
            return statement.executeUpdate();
        }

        private static string GenerateInsertSql(PropertyInfo[] properties,
                                                   string tableName,
                                                   bool isDynamic = false)
        {
            if (properties.Length < 1)
                throw new MissingMemberException($"Empty class cannot insert. Minimum 1 property is required!");

            var iFields = new StringBuilder();
            var iParams = new StringBuilder();
            for (int i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                if (!isDynamic)
                {
                    var autoIncFieldAttr = (DatabaseGeneratedAttribute[])property
                      .GetCustomAttributes(typeof(DatabaseGeneratedAttribute), false);
                    if (autoIncFieldAttr != null &&
                        autoIncFieldAttr.Any() &&
                        autoIncFieldAttr[0].DatabaseGeneratedOption == DatabaseGeneratedOption.Identity)
                        continue;
                }

                bool isAppendComma = i < properties.Length - 1;
                iFields.Append(property.Name);
                if (isAppendComma)
                    iFields.Append(", ");

                iParams.Append($":{property.Name}");
                if (isAppendComma)
                    iParams.Append(", ");
            }

            return @$"insert into {tableName} ({iFields.ToString()}) values ({iParams.ToString()})";
        }

        public static int Insert<TEntity>(this Connection connection, TEntity entity, string tableName = "")
        {
            ArgumentNullException.ThrowIfNull(entity);

            if (entity is IEntityValidation iEntityValidation)
                iEntityValidation.Validate();

            Type typeOfEntity = entity.GetType();
            var attributesOfClass = (TableAttribute[])typeOfEntity
                .GetCustomAttributes(typeof(TableAttribute), false);

            if (string.IsNullOrEmpty(tableName))
                if (!attributesOfClass.Any())
                    throw new CustomAttributeFormatException($"{nameof(TableAttribute)} attribute not found!");

            if (attributesOfClass.Length > 1)
                throw new CustomAttributeFormatException($"1 {nameof(TableAttribute)} attribute is enough!");

            string iTableName = string.IsNullOrEmpty(tableName) ? attributesOfClass[0].Name : tableName;
            var insertSql = GenerateInsertSql(properties: typeOfEntity.GetProperties(),
                                              tableName: iTableName,
                                              isDynamic: false);
            return Execute(connection, insertSql, entity);
        }

        public static int Insert(this Connection connection, dynamic entity, string tableName)
        {
            ArgumentNullException.ThrowIfNull(entity);
            ArgumentNullException.ThrowIfNull(tableName);

            var insertSql = GenerateInsertSql(properties: entity.GetType().GetProperties(),
                                              tableName: tableName,
                                              isDynamic: true);
            return Execute(connection, insertSql, entity);
        }

        public static int DeleteByUniqueKey<TUniqueKey>(this Connection connection, string tableName, string uniqueKeyFieldName, TUniqueKey uniqueKeyValue)
        {
            ArgumentNullException.ThrowIfNull(tableName);
            ArgumentNullException.ThrowIfNull(uniqueKeyFieldName);
            ArgumentNullException.ThrowIfNull(uniqueKeyValue);

            var parameters = new ExpandoObject();
            parameters.TryAdd(uniqueKeyFieldName, uniqueKeyValue);

            var deleteSql = $"delete from {tableName} where {uniqueKeyFieldName} = :{uniqueKeyFieldName}";
            return Execute(connection, deleteSql, parameters);
        }


        public static Result<T> QueryStoredProc<T>(this Connection connection, string sql, dynamic? parameters = null, bool includeMetaData = false) where T : class, new()
        {
            ArgumentNullException.ThrowIfNull(sql);

            using var statement = NamedParameterCallebleStatement
                       .createNamedParameterPreparedStatement(connection, sql);
            SetStatementParameters(statement, parameters);
            using var resultSet = statement.executeQuery();
            if (!resultSet.CheckResultSet())
                return Result<T>.CreateEmpty();

            Type typeOfT = typeof(T);
            var list = new List<T>();
            var result = new Result<T>();

            ResultSetMetaData resultSetMetaData = resultSet.getMetaData();
            result.Start();
            if (includeMetaData)
                result.AddMetaData(resultSetMetaData);

            while (resultSet.next())
            {
                T instance = new T();
                resultSet.Map(resultSetMetaData, ref instance, typeOfT);
                list.Add(instance);
            }
            result.Data = list;
            result.Stop();
            return result;
        }

        public static Result<string> QueryStoredProcString(this Connection connection, string sql, dynamic? parameters = null, bool includeMetaData = false)
        {
            ArgumentNullException.ThrowIfNull(sql);

            using var statement = NamedParameterCallebleStatement
                       .createNamedParameterPreparedStatement(connection, sql);
            SetStatementParameters(statement, parameters);
            using var resultSet = statement.executeQuery();
            if (!resultSet.CheckResultSet())
                return Result<string>.CreateEmpty();

            var result = new Result<string>();

            ResultSetMetaData resultSetMetaData = resultSet.getMetaData();
            result.AddMetaData(resultSetMetaData);

            while (resultSet.next())
                result.Data.Add(resultSet.getString(1));

            return result;
        }

        public static bool ExecuteStoredProc(this Connection connection, string sql, dynamic? parameters = null, bool includeMetaData = false)
        {
            ArgumentNullException.ThrowIfNull(sql);
            using (var statement = NamedParameterCallebleStatement.createNamedParameterPreparedStatement(connection, sql))
            {
                SetStatementParameters(statement, parameters);
                return statement.execute();
            }
        }

        public static int ExecuteUpdateStoredProc(this Connection connection, string sql, dynamic? parameters = null, bool includeMetaData = false)
        {
            ArgumentNullException.ThrowIfNull(sql);
            using var statement = NamedParameterCallebleStatement
                       .createNamedParameterPreparedStatement(connection, sql);
            SetStatementParameters(statement, parameters);
            return statement.executeUpdate();
        }



        public static Result<int> QueryStoredProcInt(this Connection connection, string sql, dynamic? parameters = null, bool includeMetaData = false)
        {
            ArgumentNullException.ThrowIfNull(sql);

            using var statement = NamedParameterCallebleStatement
                       .createNamedParameterPreparedStatement(connection, sql);
            SetStatementParameters(statement, parameters);
            using var resultSet = statement.executeQuery();
            if (!resultSet.CheckResultSet())
                return Result<int>.CreateEmpty();

            var result = new Result<int>();

            ResultSetMetaData resultSetMetaData = resultSet.getMetaData();
            result.AddMetaData(resultSetMetaData);

            while (resultSet.next())
                result.Data.Add(resultSet.getInt(1));

            return result;
        }
        public static Result<decimal> QueryStoredProcDecimal(this Connection connection, string sql, dynamic? parameters = null, bool includeMetaData = false)
        {
            ArgumentNullException.ThrowIfNull(sql);

            using var statement = NamedParameterCallebleStatement
                       .createNamedParameterPreparedStatement(connection, sql);
            SetStatementParameters(statement, parameters);
            using var resultSet = statement.executeQuery();
            if (!resultSet.CheckResultSet())
                return Result<decimal>.CreateEmpty();

            var result = new Result<decimal>();

            ResultSetMetaData resultSetMetaData = resultSet.getMetaData();
            result.AddMetaData(resultSetMetaData);

            while (resultSet.next())
                result.Data.Add((decimal)resultSet.getFloat(1));

            return result;
        }

        public static T? QuerySingle<T>(this Connection connection, string sql, dynamic? parameters = null, bool includeMetaData = false) where T : class, new()
        {
            ArgumentNullException.ThrowIfNull(sql);

            using var statement = NamedParameterPreparedStatement
                 .createNamedParameterPreparedStatement(connection, sql,
                 resultSetType: ResultSet.TYPE_FORWARD_ONLY,
                 resultSetConcurrency: ResultSet.CONCUR_READ_ONLY);
            statement.setFetchSize(1);
            SetStatementParameters(statement, parameters);
            using var resultSet = statement.executeQuery();
            if (!resultSet.CheckResultSet())
                return default(T);

            Type typeOfT = typeof(T);
            ResultSetMetaData resultSetMetaData = resultSet.getMetaData();
            if (resultSet.next())
            {
                T instance = new T();
                resultSet.Map(resultSetMetaData, ref instance, typeOfT);
                return instance;
            }
            return default(T);
        }

        public static Result<byte> QuerySingleByte(this Connection connection, string sql, dynamic? parameters = null)
        {
            ArgumentNullException.ThrowIfNull(sql);

            StatementResultSetPair statementResult = CreateStatementAndResult(connection, sql,
                resultSetType: ResultSet.TYPE_FORWARD_ONLY,
                resultSetConcurrency: ResultSet.CONCUR_READ_ONLY,
                parameters: parameters);


            using (statementResult.statement)
            {
                using (statementResult.resultSet)
                {
                    if (!statementResult.resultSet.CheckResultSet())
                        return Result<byte>.CreateEmpty();

                    var result = new Result<byte>();

                    ResultSetMetaData resultSetMetaData = statementResult.resultSet.getMetaData();
                    result.AddMetaData(resultSetMetaData);

                    while (statementResult.resultSet.next())
                        result.Data.Add(statementResult.resultSet.getByte(1));
                    return result;
                }
            }
        }

        public static Result<byte[]> QuerySingleBytes(this Connection connection, string sql, dynamic? parameters = null)
        {
            ArgumentNullException.ThrowIfNull(sql);

            StatementResultSetPair statementResult = CreateStatementAndResult(connection, sql,
                resultSetType: ResultSet.TYPE_FORWARD_ONLY,
                resultSetConcurrency: ResultSet.CONCUR_READ_ONLY,
                parameters: parameters);

            using (statementResult.statement)
            {
                using (statementResult.resultSet)
                {
                    if (!statementResult.resultSet.CheckResultSet())
                        return Result<byte[]>.CreateEmpty();

                    var result = new Result<byte[]>();

                    ResultSetMetaData resultSetMetaData = statementResult.resultSet.getMetaData();
                    result.AddMetaData(resultSetMetaData);

                    while (statementResult.resultSet.next())
                        result.Data.Add(statementResult.resultSet.getBytes(1));
                    return result;
                }
            }
        }

        public static Result<short> QuerySingleShort(this Connection connection, string sql, dynamic? parameters = null)
        {
            ArgumentNullException.ThrowIfNull(sql);

            StatementResultSetPair statementResult = CreateStatementAndResult(connection, sql,
                resultSetType: ResultSet.TYPE_FORWARD_ONLY,
                resultSetConcurrency: ResultSet.CONCUR_READ_ONLY,
                parameters: parameters);

            using (statementResult.statement)
            {
                using (statementResult.resultSet)
                {
                    if (!statementResult.resultSet.CheckResultSet())
                        return Result<short>.CreateEmpty();

                    var result = new Result<short>();

                    ResultSetMetaData resultSetMetaData = statementResult.resultSet.getMetaData();
                    result.AddMetaData(resultSetMetaData);

                    while (statementResult.resultSet.next())
                        result.Data.Add(statementResult.resultSet.getShort(1));
                    return result;
                }
            }
        }

        public static Result<int> QuerySingleInt(this Connection connection, string sql, dynamic? parameters = null)
        {
            ArgumentNullException.ThrowIfNull(sql);

            StatementResultSetPair statementResult = CreateStatementAndResult(connection, sql,
                resultSetType: ResultSet.TYPE_FORWARD_ONLY,
                resultSetConcurrency: ResultSet.CONCUR_READ_ONLY,
                parameters: parameters);

            using (statementResult.statement)
            {
                using (statementResult.resultSet)
                {
                    if (!statementResult.resultSet.CheckResultSet())
                        return Result<int>.CreateEmpty();

                    var result = new Result<int>();

                    ResultSetMetaData resultSetMetaData = statementResult.resultSet.getMetaData();
                    result.AddMetaData(resultSetMetaData);

                    while (statementResult.resultSet.next())
                        result.Data.Add(statementResult.resultSet.getInt(1));
                    return result;
                }
            }
        }

        public static Result<long> QuerySingleLong(this Connection connection, string sql, dynamic? parameters = null)
        {
            ArgumentNullException.ThrowIfNull(sql);

            StatementResultSetPair statementResult = CreateStatementAndResult(connection, sql,
                resultSetType: ResultSet.TYPE_FORWARD_ONLY,
                resultSetConcurrency: ResultSet.CONCUR_READ_ONLY,
                parameters: parameters);

            using (statementResult.statement)
            {
                using (statementResult.resultSet)
                {
                    if (!statementResult.resultSet.CheckResultSet())
                        return Result<long>.CreateEmpty();

                    var result = new Result<long>();

                    ResultSetMetaData resultSetMetaData = statementResult.resultSet.getMetaData();
                    result.AddMetaData(resultSetMetaData);

                    while (statementResult.resultSet.next())
                        result.Data.Add(statementResult.resultSet.getLong(1));
                    return result;
                }
            }
        }

        public static Result<string> QuerySingleString(this Connection connection, string sql, dynamic? parameters = null)
        {
            ArgumentNullException.ThrowIfNull(sql);

            StatementResultSetPair statementResult = CreateStatementAndResult(connection, sql,
                resultSetType: ResultSet.TYPE_FORWARD_ONLY,
                resultSetConcurrency: ResultSet.CONCUR_READ_ONLY,
                parameters: parameters);

            using (statementResult.statement)
            {
                using (statementResult.resultSet)
                {
                    if (!statementResult.resultSet.CheckResultSet())
                        return Result<string>.CreateEmpty();

                    var result = new Result<string>();

                    ResultSetMetaData resultSetMetaData = statementResult.resultSet.getMetaData();
                    result.AddMetaData(resultSetMetaData);

                    while (statementResult.resultSet.next())
                    {
                        var value = statementResult.resultSet.getString(1);
                        result.Data.Add(string.IsNullOrEmpty(value) ? null : value.Trim());
                    }
                    return result;
                }
            }
        }

        public static Result<float> QuerySingleFloat(this Connection connection, string sql, dynamic? parameters = null)
        {
            ArgumentNullException.ThrowIfNull(sql);

            StatementResultSetPair statementResult = CreateStatementAndResult(connection, sql,
                resultSetType: ResultSet.TYPE_FORWARD_ONLY,
                resultSetConcurrency: ResultSet.CONCUR_READ_ONLY,
                parameters: parameters);

            using (statementResult.statement)
            {
                using (statementResult.resultSet)
                {
                    if (!statementResult.resultSet.CheckResultSet())
                        return Result<float>.CreateEmpty();

                    var result = new Result<float>();

                    ResultSetMetaData resultSetMetaData = statementResult.resultSet.getMetaData();
                    result.AddMetaData(resultSetMetaData);

                    while (statementResult.resultSet.next())
                        result.Data.Add(statementResult.resultSet.getFloat(1));
                    return result;
                }
            }
        }

        /// <summary>
        /// Query data with paging
        /// </summary>
        /// <code>
        ///  var page = new Page(1, 10);
        ///  var pagingResult = connectionFactory.Connection
        ///   .Query<GumMevzuat2>(@"select kod,aciklama,kitapbilgi  from gummevzuat order by kod asc ", page);
        ///    for (int pageNumber = 2; pageNumber <= pagingResult.TotalPages; pageNumber++)
        ///    {
        ///        page = new Page() { PageNumber = pageNumber, PageSize = 10 };
        ///          pagingResult = connectionFactory.Connection
        ///          .Query<GumMevzuat2>(@"select kod,aciklama,kitapbilgi from gummevzuat order by kod asc ", page);
        ///    }
        /// </code>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <param name="sql"></param>
        /// <param name="page"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static PagingResult<T> Query<T>(this Connection connection, string sql, Page page, dynamic? parameters = null, bool includeMetaData = false, bool isblobSelect = false)
        {
            ArgumentNullException.ThrowIfNull(sql);
            ArgumentNullException.ThrowIfNull(page);

            int recordCountOfSql = connection.QuerySingleInt($"select count(*) from ({sql})").Data[0];
            var pagingSql = $"select * from ({sql}) skip {page.Skip} limit {page.PageSize}";


            int resultSetType = isblobSelect ? ResultSet.TYPE_FORWARD_ONLY : ResultSet.TYPE_SCROLL_INSENSITIVE;
            StatementResultSetPair statementResult = CreateStatementAndResult(connection, pagingSql,
                    resultSetType,
                    ResultSet.CONCUR_READ_ONLY,
                    fetchSize: page.PageSize,
                    parameters: parameters);

            using (statementResult.statement)
            {
                using (statementResult.resultSet)
                {
                    if (!statementResult.resultSet.CheckResultSet())
                        return PagingResult<T>.Empty(page, recordCountOfSql);

                    int recordCount = 0;
                    if (!isblobSelect)
                    {
                        recordCount = statementResult.resultSet.GetRecordCount();
                        if (recordCount == 0)
                            return PagingResult<T>.Empty(page, recordCountOfSql);
                    }

                    Type typeOfT = typeof(T);
                    var list = new List<T>(recordCount);
                    ResultSetMetaData resultSetMetaData = statementResult.resultSet.getMetaData();
                    while (statementResult.resultSet.next())
                    {
                        T instance = New<T>.CreateInstance();
                        statementResult.resultSet.Map(resultSetMetaData, ref instance, typeOfT);
                        list.Add(instance);
                    }

                    var pagingResult = new PagingResult<T>(list, page, recordCountOfSql);
                    if (includeMetaData)
                        pagingResult.AddMetaData(resultSetMetaData);
                    return pagingResult;
                }
            }
        }

        public static Result<T> Query<T>(this Connection connection, string sql, dynamic? parameters = null, bool includeMetaData = false, bool isblobSelect = false) where T : class, new()
        {
            ArgumentNullException.ThrowIfNull(sql);

            int resultSetType = isblobSelect ? ResultSet.TYPE_FORWARD_ONLY : ResultSet.TYPE_SCROLL_INSENSITIVE;
            StatementResultSetPair statementResult = CreateStatementAndResult(connection, sql,
                resultSetType,
                ResultSet.CONCUR_READ_ONLY,
                parameters: parameters);

            using (statementResult.statement)
            {
                using (statementResult.resultSet)
                {
                    if (!statementResult.resultSet.CheckResultSet())
                        return Result<T>.CreateEmpty();


                    int recordCount = 0;
                    if (!isblobSelect)
                    {
                        recordCount = statementResult.resultSet.GetRecordCount();
                        if (recordCount == 0)
                            return Result<T>.CreateEmpty();
                    }


                    Type typeOfT = typeof(T);
                    var list = new List<T>(recordCount);
                    var result = new Result<T>();

                    ResultSetMetaData resultSetMetaData = statementResult.resultSet.getMetaData();

                    result.Start();

                    if (includeMetaData)
                        result.AddMetaData(resultSetMetaData);

                    while (statementResult.resultSet.next())
                    {
                        T instance = new T();
                        statementResult.resultSet.Map(resultSetMetaData, ref instance, typeOfT);
                        list.Add(instance);
                    }
                    result.Data = list;
                    result.Stop();
                    return result;
                }
            }
        }

        public static AggregateResult<T, R> Query<T, R>(this Connection connection, string sql, dynamic? parameters = null, bool includeMetaData = false, bool isblobSelect = false, Action<T, R> aggregate = null)
        where T : class, new()
        where R : class, new()

        {
            ArgumentNullException.ThrowIfNull(sql);

            int resultSetType = isblobSelect ? ResultSet.TYPE_FORWARD_ONLY : ResultSet.TYPE_SCROLL_INSENSITIVE;
            StatementResultSetPair statementResult = CreateStatementAndResult(connection, sql,
                resultSetType,
                ResultSet.CONCUR_READ_ONLY,
                parameters: parameters);

            using (statementResult.statement)
            {
                using (statementResult.resultSet)
                {
                    if (!statementResult.resultSet.CheckResultSet())
                        return new AggregateResult<T, R>();


                    int recordCount = 0;
                    if (!isblobSelect)
                    {
                        recordCount = statementResult.resultSet.GetRecordCount();
                        if (recordCount == 0)
                            return new AggregateResult<T, R>();
                    }


                    Type typeOfT = typeof(T);
                    var list = new List<T>(recordCount);
                    var result = new AggregateResult<T, R>();
                    result.AggregateValue = new R();

                    ResultSetMetaData resultSetMetaData = statementResult.resultSet.getMetaData();

                    R r = new R();
                    result.Start();

                    if (includeMetaData)
                        result.AddMetaData(resultSetMetaData);

                    while (statementResult.resultSet.next())
                    {
                        T instance = new T();
                        statementResult.resultSet.Map(resultSetMetaData, ref instance, typeOfT);
                        aggregate(instance, result.AggregateValue);
                        list.Add(instance);
                    }

                    result.Data = list;
                    result.Stop();
                    return result;
                }
            }
        }



        public static DynamicResult QueryDynamic(this Connection connection, string sql, dynamic? parameters = null, bool includeMetaData = false, bool isblobSelect = false)
        {
            ArgumentNullException.ThrowIfNull(sql);
            /* 
             Blob alan okuma yapilacagi durumda resultSetType parametresine,
             ResultSet.TYPE_SCROLL_INSENSITIVE degeri gecilirse asagidaki hata geliyor.
             java.sql.SQLException: 'Scroll cursor can't select blob columns.'
             */
            int resultSetType = isblobSelect ? ResultSet.TYPE_FORWARD_ONLY : ResultSet.TYPE_SCROLL_INSENSITIVE;
            StatementResultSetPair statementResult = CreateStatementAndResult(connection, sql,
                resultSetType,
                ResultSet.CONCUR_READ_ONLY,
                parameters: parameters);

            using (statementResult.statement)
            {
                using (statementResult.resultSet)
                {
                    if (!statementResult.resultSet.CheckResultSet())
                        return DynamicResult.Empty();

                    int recordCount = 0;
                    if (!isblobSelect)
                    {
                        recordCount = statementResult.resultSet.GetRecordCount();
                        if (recordCount == 0)
                            return DynamicResult.Empty();
                    }

                    ResultSetMetaData resultSetMetaData = statementResult.resultSet.getMetaData();
                    var dynamicResult = new DynamicResult();

                    dynamicResult.Start();
                    dynamicResult.Data = new List<dynamic>(recordCount);
                    if (includeMetaData)
                        dynamicResult.AddMetaData(resultSetMetaData);

                    while (statementResult.resultSet.next())
                    {
                        var instance = new ExpandoObject();
                        statementResult.resultSet.Map(resultSetMetaData, ref instance);
                        dynamicResult.Data.Add(instance);
                    }
                    dynamicResult.Stop();
                    return dynamicResult;
                }
            }
        }

        public static DynamicPagingResult QueryDynamic(this Connection connection, string sql, Page page, dynamic? parameters = null, bool includeMetaData = false, bool isblobSelect = false)
        {
            ArgumentNullException.ThrowIfNull(sql);
            ArgumentNullException.ThrowIfNull(page);

            int recordCountOfSql = connection.QuerySingleInt($"select count(*) from ({sql})").Data[0];
            var pagingSql = $"select * from ({sql}) skip {page.Skip} limit {page.PageSize}";


            int resultSetType = isblobSelect ? ResultSet.TYPE_FORWARD_ONLY : ResultSet.TYPE_SCROLL_INSENSITIVE;
            StatementResultSetPair statementResult = CreateStatementAndResult(connection, pagingSql,
                    resultSetType,
                    ResultSet.CONCUR_READ_ONLY,
                    fetchSize: page.PageSize,
                    parameters: parameters);

            using (statementResult.statement)
            {
                using (statementResult.resultSet)
                {
                    if (!statementResult.resultSet.CheckResultSet())
                        return DynamicPagingResult.Empty(page, recordCountOfSql);


                    int recordCount = 0;
                    if (!isblobSelect)
                    {
                        recordCount = statementResult.resultSet.GetRecordCount();
                        if (recordCount == 0)
                            return DynamicPagingResult.Empty(page, recordCountOfSql);
                    }

                    var list = new List<dynamic>(recordCount);
                    ResultSetMetaData resultSetMetaData = statementResult.resultSet.getMetaData();
                    while (statementResult.resultSet.next())
                    {
                        var instance = new ExpandoObject();
                        statementResult.resultSet.Map(resultSetMetaData, ref instance);
                        list.Add(instance);
                    }

                    var pagingResult = new DynamicPagingResult(list, page, recordCountOfSql);
                    if (includeMetaData)
                        pagingResult.AddMetaData(resultSetMetaData);
                    return pagingResult;
                }
            }
        }

        private static string Sha256Hash(string value)
        {
            using var hash = System.Security.Cryptography.SHA256.Create();
            var byteArray = hash.ComputeHash(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(byteArray);
        }

        public static DynamicCompiledResult QueryDynamicCompiled(this Connection connection, string sql, dynamic? parameters = null, bool includeMetaData = false, bool isblobSelect = false)
        {
            ArgumentNullException.ThrowIfNull(sql);
            /* 
             Blob alan okuma yapilacagi durumda resultSetType parametresine,
             ResultSet.TYPE_SCROLL_INSENSITIVE degeri gecilirse asagidaki hata geliyor.
             java.sql.SQLException: 'Scroll cursor can't select blob columns.'
             */
            int resultSetType = isblobSelect ? ResultSet.TYPE_FORWARD_ONLY : ResultSet.TYPE_SCROLL_INSENSITIVE;
            StatementResultSetPair statementResult = CreateStatementAndResult(connection, sql,
                resultSetType,
                ResultSet.CONCUR_READ_ONLY,
                parameters: parameters);

            using (statementResult.statement)
            {
                using (statementResult.resultSet)
                {
                    if (!statementResult.resultSet.CheckResultSet())
                        return DynamicCompiledResult.Empty();

                    int recordCount = 0;
                    if (!isblobSelect)
                    {
                        recordCount = statementResult.resultSet.GetRecordCount();
                        if (recordCount == 0)
                            return DynamicCompiledResult.Empty();
                    }

                    ResultSetMetaData resultSetMetaData = statementResult.resultSet.getMetaData();
                    var dynamicCompiledResult = new DynamicCompiledResult();

                    dynamicCompiledResult.Start();
                    dynamicCompiledResult.Data = new List<object>(recordCount);
                    if (includeMetaData)
                        dynamicCompiledResult.AddMetaData(resultSetMetaData);


                    var hashOfSql = Sha256Hash(sql);
                    string dynamicallyStronglyTypedClassName = $"DynamicallyStronglyTypedClass{hashOfSql}";
                    var classAsString = resultSetMetaData.GetCSharpClassAsString(dynamicallyStronglyTypedClassName);
                    var assembly = CodeCompiler.Compile(dynamicallyStronglyTypedClassName, classAsString);
                    Type? type = assembly?.GetType(dynamicallyStronglyTypedClassName);
                    if (type == null)
                        throw new TypeLoadException(dynamicallyStronglyTypedClassName);

                    var compiledFunc = NewEx.CreateInstanceFunc(type);
                    while (statementResult.resultSet.next())
                    {
                        var instance = compiledFunc();
                        statementResult.resultSet.Map(resultSetMetaData, ref instance, type);
                        dynamicCompiledResult.Data.Add(instance);
                    }
                    dynamicCompiledResult.Stop();
                    return dynamicCompiledResult;
                }
            }
        }

        public static string QueryResultAsStringCSharpClass(this Connection connection, string sql, string className, dynamic? parameters = null, bool includeMetaData = false, bool isblobSelect = false)
        {
            int resultSetType = isblobSelect ? ResultSet.TYPE_FORWARD_ONLY : ResultSet.TYPE_SCROLL_INSENSITIVE;
            StatementResultSetPair statementResult = CreateStatementAndResult(connection, sql,
                resultSetType,
                ResultSet.CONCUR_READ_ONLY,
                parameters: parameters);

            using (statementResult.statement)
            {
                using (statementResult.resultSet)
                {
                    if (!statementResult.resultSet.CheckResultSet())
                        return string.Empty;

                    ResultSetMetaData resultSetMetaData = statementResult.resultSet.getMetaData();
                    return resultSetMetaData.GetCSharpClassAsString(className);
                }
            }
        }

        public static string QueryResultInsertSql(this Connection connection, string sql, string tableName, dynamic? parameters = null, bool includeMetaData = false, bool isblobSelect = false)
        {
            int resultSetType = isblobSelect ? ResultSet.TYPE_FORWARD_ONLY : ResultSet.TYPE_SCROLL_INSENSITIVE;
            StatementResultSetPair statementResult = CreateStatementAndResult(connection, sql,
                resultSetType,
                ResultSet.CONCUR_READ_ONLY,
                parameters: parameters);

            using (statementResult.statement)
            {
                using (statementResult.resultSet)
                {
                    if (!statementResult.resultSet.CheckResultSet())
                        return string.Empty;

                    ResultSetMetaData resultSetMetaData = statementResult.resultSet.getMetaData();
                    return resultSetMetaData.GetInsertSQLFromTable(tableName);
                }
            }
        }

        public static string GetCSharpClassAsString(this ResultSetMetaData resultSetMetaData, string className)
        {
            var fieldNames = new HashSet<string>();
            string AddProperty(string propertyType, string propertyName, bool isNullable)
            {
                var sIsNullable = isNullable ? "?" : string.Empty;
                return $"  public {propertyType}{sIsNullable} {propertyName} {{ get; set; }}";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"public class {className} ")
                .AppendLine("{");

            int columnCount = resultSetMetaData.getColumnCount();
            for (int columnIndex = 1; columnIndex <= columnCount; columnIndex++)
            {
                var columnName = resultSetMetaData.getColumnLabel(columnIndex);
                bool isNullable = resultSetMetaData.isNullable(columnIndex) == ResultSetMetaData.columnNullable;


                // Ayni alan ismi sorguda tekrar ediyorsa tekrar eklenmemesi icin...
                if (fieldNames.Contains(columnName))
                    continue;
                fieldNames.Add(columnName);

                switch (resultSetMetaData.getColumnType(columnIndex))
                {
                    case Types.BIT:
                        sb.AppendLine(AddProperty("bool", columnName, isNullable));
                        break;

                    case Types.TINYINT:
                        sb.AppendLine(AddProperty("byte", columnName, isNullable));
                        break;

                    case Types.SMALLINT:
                        sb.AppendLine(AddProperty("short", columnName, isNullable));
                        break;

                    case Types.INTEGER:
                        sb.AppendLine(AddProperty("int", columnName, isNullable));
                        break;

                    case Types.BIGINT:
                        sb.AppendLine(AddProperty("long", columnName, isNullable));
                        break;

                    case Types.FLOAT:
                    case Types.REAL:
                    case Types.DOUBLE:
                        sb.AppendLine(AddProperty("double", columnName, isNullable));
                        break;

                    case Types.NUMERIC:
                    case Types.DECIMAL:
                        sb.AppendLine(AddProperty("decimal", columnName, isNullable));
                        break;

                    case Types.CHAR:
                    case Types.VARCHAR:
                    case Types.LONGVARCHAR:
                    case Types.NCHAR:
                    case Types.NVARCHAR:
                    case Types.LONGNVARCHAR:
                        sb.AppendLine(AddProperty("string", columnName, isNullable));
                        break;

                    case Types.DATE:
                    case Types.TIME:
                    case Types.TIMESTAMP:
                    case Types.TIME_WITH_TIMEZONE:
                    case Types.TIMESTAMP_WITH_TIMEZONE:
                        sb.AppendLine(AddProperty("System.DateTime", columnName, isNullable));
                        break;

                    case Types.BINARY:
                    case Types.VARBINARY:
                    case Types.LONGVARBINARY:
                        sb.AppendLine(AddProperty("byte[]", columnName, isNullable));
                        break;

                    case Types.BLOB:
                    case Types.CLOB:
                    case Types.NCLOB:
                        sb.AppendLine(AddProperty("byte[]", columnName, isNullable));
                        break;


                    case Types.BOOLEAN:
                        sb.AppendLine(AddProperty("bool", columnName, isNullable));
                        break;

                    case Types.NULL:
                        break;

                    case Types.OTHER:
                        break;

                    case Types.JAVA_OBJECT:
                        break;

                    case Types.DISTINCT:
                        break;

                    case Types.STRUCT:
                        break;

                    case Types.ARRAY:
                        break;

                    case Types.REF:
                        break;
                    case Types.DATALINK:
                        break;

                    case Types.ROWID:
                        break;

                    case Types.SQLXML:
                        break;

                    case Types.REF_CURSOR:
                        break;

                    default:
                        break;
                }
            }
            sb.AppendLine("}");
            return sb.ToString();
        }

        public static string GetInsertSQLFromTable(this ResultSetMetaData resultSetMetaData, string tableName)
        {
            var fieldNames = new HashSet<string>();

            var fields = new StringBuilder();
            var parameters = new StringBuilder();

            int columnCount = resultSetMetaData.getColumnCount();
            for (int columnIndex = 1; columnIndex <= columnCount; columnIndex++)
            {
                var columnName = resultSetMetaData.getColumnLabel(columnIndex);

                // Ayni alan ismi sorguda tekrar ediyorsa tekrar eklenmemesi icin...
                if (fieldNames.Contains(columnName))
                    continue;


                fields.Append(columnName);
                if (columnIndex < columnCount)
                    fields.Append(",");


                parameters.Append(":").Append(columnName);
                if (columnIndex < columnCount)
                    parameters.Append(",");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"insert into {tableName} (")
              .AppendLine(fields.ToString()).Append(")")
              .Append(" values ")
              .Append("(")
              .AppendLine(parameters.ToString())
              .Append(")");

            return sb.ToString();
        }

        public static void AddMetaData<T>(this Result<T> result, ResultSetMetaData resultSetMetaData)
        {
            int columnCount = resultSetMetaData.getColumnCount();
            result.MetaData = new List<ResultMetaData>(columnCount);
            for (int columnIndex = 1; columnIndex <= columnCount; columnIndex++)
            {
                var metaData = new ResultMetaData();
                metaData.ColumnName = resultSetMetaData.getColumnLabel(columnIndex);
                metaData.ColumnType = resultSetMetaData.getColumnType(columnIndex);
                metaData.ColumnTypeName = resultSetMetaData.getColumnTypeName(columnIndex);
                metaData.ColumnSize = resultSetMetaData.getColumnDisplaySize(columnIndex);
                metaData.ColumnPrecision = resultSetMetaData.getPrecision(columnIndex);
                metaData.ColumnScale = resultSetMetaData.getScale(columnIndex);
                result.MetaData.Add(metaData);
            }
        }

        public static List<T> QueryEx<T>(this Connection connection, string sql, dynamic? parameters = null)
        {
            return QueryEx<T>(connection, sql, isblobSelect: false, parameters: parameters);
        }

        public static List<T> QueryEx<T>(Connection connection, string sql, bool isblobSelect = false, dynamic? parameters = null)
        {
            ArgumentNullException.ThrowIfNull(sql);

            int resultSetType = isblobSelect ? ResultSet.TYPE_FORWARD_ONLY : ResultSet.TYPE_SCROLL_INSENSITIVE;
            StatementResultSetPair statementResult = CreateStatementAndResult(connection, sql,
                resultSetType,
                ResultSet.CONCUR_READ_ONLY,
                parameters: parameters);

            using (statementResult.statement)
            {
                using (statementResult.resultSet)
                {
                    if (statementResult.resultSet.CheckResultSet())
                        return new List<T>();


                    int recordCount = 0;
                    if (!isblobSelect)
                    {
                        statementResult.resultSet.GetRecordCount();
                        if (recordCount == 0)
                            return new List<T>();
                    }

                    var list = new List<T>(recordCount);
                    var properties = typeof(T).GetProperties();
                    while (statementResult.resultSet.next())
                    {
                        T item = New<T>.CreateInstance();

                        foreach (var property in properties)
                        {
                            switch (Type.GetTypeCode(property.PropertyType))
                            {
                                case TypeCode.Boolean:
                                    break;
                                case TypeCode.Byte:
                                    property.SetValue(item, statementResult.resultSet.getByte(property.Name));
                                    break;
                                case TypeCode.Char:
                                    break;
                                case TypeCode.DBNull:
                                    break;
                                case TypeCode.DateTime:
                                    property.SetValue(item, statementResult.resultSet.GetDateTime(property.Name));
                                    break;
                                case TypeCode.Decimal:
                                    property.SetValue(item, (decimal)statementResult.resultSet.getDouble(property.Name));
                                    break;
                                case TypeCode.Double:
                                    property.SetValue(item, statementResult.resultSet.getDouble(property.Name));
                                    break;
                                case TypeCode.Empty:
                                    break;
                                case TypeCode.Int16:
                                    property.SetValue(item, statementResult.resultSet.getShort(property.Name));
                                    break;
                                case TypeCode.Int32:
                                    property.SetValue(item, statementResult.resultSet.getInt(property.Name));
                                    break;
                                case TypeCode.Int64:
                                    property.SetValue(item, statementResult.resultSet.getLong(property.Name));
                                    break;
                                case TypeCode.Object:
                                    {
                                        if (property.PropertyType.IsArray &&
                                             Type.GetTypeCode(property.PropertyType.GetElementType()) == TypeCode.Byte)
                                            property.SetValue(item, statementResult.resultSet.getBytes(property.Name));
                                    }
                                    break;
                                case TypeCode.SByte:
                                    break;
                                case TypeCode.Single:
                                    break;
                                case TypeCode.String:
                                    {
                                        var value = statementResult.resultSet.getString(property.Name);
                                        property.SetValue(item, string.IsNullOrEmpty(value) ? null : value.Trim());
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
                        list.Add(item);
                    }
                    return list;
                }
            }
        }





        /// <summary>
        ///----------------------------------------
        ///.NET Type              | Informix Type  
        ///----------------------------------------
        ///TypeCode.Empty = 0     | 0
        ///----------------------------------------
        ///TypeCode.Object = 1    | 0
        ///----------------------------------------
        ///TypeCode.DBNull = 2    | 0
        ///----------------------------------------
        ///TypeCode.Boolean = 3   | Types.BIT
        ///----------------------------------------
        ///TypeCode.Char = 4      | Types.CHAR
        ///----------------------------------------
        ///TypeCode.SByte = 5     | 0
        ///----------------------------------------
        ///TypeCode.Byte = 6      | Types.TINYINT
        ///----------------------------------------
        ///TypeCode.Int16 =       | Types.SMALLINT
        ///----------------------------------------
        ///TypeCode.UInt16 = 8    | 0
        ///----------------------------------------
        ///TypeCode.Int32 = 9     | Types.INTEGER
        ///----------------------------------------
        ///TypeCode.UInt32 = 10   | 0
        ///----------------------------------------
        ///TypeCode.Int64 = 11    | Types.BIGINT
        ///----------------------------------------
        ///TypeCode.UInt64 = 12   | 0
        ///----------------------------------------
        ///TypeCode.Single = 13   | Types.DOUBLE
        ///----------------------------------------
        ///TypeCode.Double = 14   | Types.DOUBLE
        ///----------------------------------------
        ///TypeCode.Decimal = 15  | Types.DOUBLE
        ///----------------------------------------
        ///TypeCode.DateTime = 16 | Types.TIMESTAMP
        ///----------------------------------------
        ///TypeCode.String = 18   | Types.VARCHAR
        ///----------------------------------------
        /// </summary>
        /// <returns></returns>
        private static int[] GetSqlTypes()
        {
            return new int[]{
                 0
                ,0
                ,0
                ,Types.BIT
                ,Types.CHAR
                ,0
                ,Types.TINYINT
                ,Types.SMALLINT
                ,0
                ,Types.INTEGER
                ,0
                ,Types.BIGINT
                ,0
                ,Types.DOUBLE
                ,Types.DOUBLE
                ,Types.DOUBLE
                ,Types.TIMESTAMP
                ,Types.VARCHAR
            };
        }

        private static void SetStatementParameters(NamedParameterPreparedStatement statement, dynamic? parameters)
        {
            if (parameters == null)
                return;

            Type type = ((object)parameters).GetType();
            var properties = type.GetProperties();

            if (!properties.Any())
                throw new ArgumentException($"{nameof(parameters)} must contain at least one parameter.");

            var typeOfGuid = typeof(Guid);
            var typeOfGuidNullable = typeof(Guid?);

            var sqlTypes = GetSqlTypes();
            foreach (var property in properties)
            {

                bool isGuid = property.PropertyType == typeOfGuid;
                bool isGuidNullable = property.PropertyType == typeOfGuidNullable;
                if (isGuid || isGuidNullable)
                {
                    object? objValue = property.GetValue(parameters);
                    statement.setstring(property.Name, objValue == null ? null : objValue.ToString());
                    continue;
                }

                TypeCode typeCodeOfProperty = Type.GetTypeCode(property.PropertyType);
            ReTypeCodeIdenficationOfNullableTypes:
                switch (typeCodeOfProperty)
                {
                    case TypeCode.Boolean:
                        break;
                    case TypeCode.Byte:
                        statement.setByte(property.Name, property.GetValue(parameters));
                        break;
                    case TypeCode.Char:
                        break;
                    case TypeCode.DBNull:
                        break;
                    case TypeCode.DateTime:
                        {
                            DateTime dateTime = (DateTime)property.GetValue(parameters);
                            statement.setTimestamp(property.Name, dateTime.GetTimestampFromDateTime());
                        }
                        break;
                    case TypeCode.Decimal:
                        {
                            double decVal = (double)property.GetValue(parameters);
                            statement.setDouble(property.Name, decVal);
                        }
                        break;
                    case TypeCode.Double:
                        statement.setDouble(property.Name, property.GetValue(parameters));
                        break;
                    case TypeCode.Empty:
                        break;
                    case TypeCode.Int16:
                        statement.setShort(property.Name, property.GetValue(parameters));
                        break;
                    case TypeCode.Int32:
                        statement.setInt(property.Name, property.GetValue(parameters));
                        break;
                    case TypeCode.Int64:
                        statement.setLong(property.Name, property.GetValue(parameters));
                        break;
                    case TypeCode.Object:
                        {
                            if (property.PropertyType.TryFindTypeCodeOfNullableType(out TypeCode? typeCode) && typeCode.HasValue)
                            {
                                if (property.GetValue(parameters) == null)
                                    statement.setNull(property.Name, sqlTypes[(byte)typeCode]);
                                else
                                {
                                    typeCodeOfProperty = typeCode.Value;
                                    goto ReTypeCodeIdenficationOfNullableTypes;
                                }
                            }

                            if (property.PropertyType.IsArray && Type.GetTypeCode(property.PropertyType.GetElementType()) == TypeCode.Byte)
                                statement.setBytes(property.Name, (byte[])property.GetValue(parameters));
                        }
                        break;
                    case TypeCode.SByte:
                        break;
                    case TypeCode.Single:
                        break;
                    case TypeCode.String:
                        statement.setstring(property.Name, property.GetValue(parameters));
                        break;
                    case TypeCode.UInt16:
                        break;
                    case TypeCode.UInt32:
                        break;
                    case TypeCode.UInt64:
                        break;
                    default:
                        break;
                }
            }
        }

        private static void SetStatementParameters(NamedParameterCallebleStatement statement, dynamic? parameters)
        {
            if (parameters == null)
                return;

            Type type = ((object)parameters).GetType();
            var properties = type.GetProperties();
            if (!properties.Any())
                throw new ArgumentException($"{nameof(parameters)} must contain at least one parameter.");

            var typeOfGuid = typeof(Guid);
            var typeOfGuidNullable = typeof(Guid?);
            var sqlTypes = GetSqlTypes();
            foreach (var property in properties)
            {

                bool isGuid = property.PropertyType == typeOfGuid;
                bool isGuidNullable = property.PropertyType == typeOfGuidNullable;
                if (isGuid || isGuidNullable)
                {
                    object? objValue = property.GetValue(parameters);
                    statement.setstring(property.Name, objValue == null ? null : objValue.ToString());
                    continue;
                }

                TypeCode typeCodeOfProperty = Type.GetTypeCode(property.PropertyType);
            ReTypeCodeIdenficationOfNullableTypes:
                switch (typeCodeOfProperty)
                {
                    case TypeCode.Boolean:
                        break;
                    case TypeCode.Byte:
                        statement.setByte(property.Name, property.GetValue(parameters));
                        break;
                    case TypeCode.Char:
                        break;
                    case TypeCode.DBNull:
                        break;
                    case TypeCode.DateTime:
                        {
                            DateTime dateTime = (DateTime)property.GetValue(parameters);
                            statement.setTimestamp(property.Name, dateTime.GetTimestampFromDateTime());
                        }
                        break;
                    case TypeCode.Decimal:
                        {
                            double decVal = (double)property.GetValue(parameters);
                            statement.setDouble(property.Name, decVal);
                        }
                        break;
                    case TypeCode.Double:
                        statement.setDouble(property.Name, property.GetValue(parameters));
                        break;
                    case TypeCode.Empty:
                        break;
                    case TypeCode.Int16:
                        statement.setShort(property.Name, property.GetValue(parameters));
                        break;
                    case TypeCode.Int32:
                        statement.setInt(property.Name, property.GetValue(parameters));
                        break;
                    case TypeCode.Int64:
                        statement.setLong(property.Name, property.GetValue(parameters));
                        break;
                    case TypeCode.Object:
                        {
                            if (property.PropertyType.TryFindTypeCodeOfNullableType(out TypeCode? typeCode) && typeCode.HasValue)
                            {
                                if (property.GetValue(parameters) == null)
                                    statement.setNull(property.Name, sqlTypes[(byte)typeCode]);
                                else
                                {
                                    typeCodeOfProperty = typeCode.Value;
                                    goto ReTypeCodeIdenficationOfNullableTypes;
                                }
                            }


                            if (property.PropertyType.IsArray && Type.GetTypeCode(property.PropertyType.GetElementType()) == TypeCode.Byte)
                                statement.setBytes(property.Name, (byte[])property.GetValue(parameters));
                        }
                        break;
                    case TypeCode.SByte:
                        break;
                    case TypeCode.Single:
                        break;
                    case TypeCode.String:
                        statement.setstring(property.Name, property.GetValue(parameters));
                        break;
                    case TypeCode.UInt16:
                        break;
                    case TypeCode.UInt32:
                        break;
                    case TypeCode.UInt64:
                        break;
                    default:
                        break;
                }
            }
        }

        public static int GetRecordCount(this ResultSet resultSet)
        {
            /* ResultSet icindeki satir sayisini getRow ile elde edebilmek icin,
               CreateStatement cagrisinda, 
               resultSetType parametresine ResultSet.TYPE_SCROLL_INSENSITIVE degerini geciyoruz. 
            */
            resultSet.last();
            int recordCount = resultSet.getRow();
            resultSet.beforeFirst();
            return recordCount;
        }

        public static bool CheckResultSet(this ResultSet resultSet)
        {
            return resultSet != null && !resultSet.isClosed();
        }
    }


    public static class New<T>
    {
        public static readonly Func<T> CreateInstance = Expression.Lambda<Func<T>>
                                                  (
                                                   Expression.New(typeof(T))
                                                  ).Compile();
    }

    public static class NewEx
    {
        public static Func<object> CreateInstanceFunc(Type type)
        {
            return Expression.Lambda<Func<object>>(Expression.New(type)).Compile();
        }
    }
}
