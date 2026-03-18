using java.lang;

namespace Ormix.NamedParam
{
    public class ParseResult
    {
        private readonly string sql;
        private readonly List<string> orderedParameters;

        public ParseResult(string sql, List<string> orderedParameters)
        {
            this.sql = sql;
            this.orderedParameters = orderedParameters;
        }

        public string GetSql()
        {
            return sql;
        }

        public List<string> GetOrderedParameters()
        {
            return orderedParameters;
        }
    }


    public static class SqlParser
    {
        public static ParseResult Parse(string query)
        {
            var localOrderedParameters = new List<string>();
            int length = query.Length;
            var parsedQuery = new StringBuffer(length);
            bool inSingleQuote = false;
            bool inDoubleQuote = false;
            bool inSingleLineComment = false;
            bool inMultiLineComment = false;
            bool inDoubleСolon = false;

            for (int i = 0; i < length; i++)
            {
                char currentChar = query[i];
                if (inSingleQuote)
                {
                    if (currentChar == '\'')
                        inSingleQuote = false;
                }
                else if (inDoubleQuote)
                {
                    if (currentChar == '"')
                        inDoubleQuote = false;
                }
                else if (inMultiLineComment)
                {
                    if (currentChar == '*' && query[i + 1] == '/')
                        inMultiLineComment = false;
                }
                else if (inDoubleСolon)
                {
                    if (!Character.isJavaIdentifierPart(currentChar))
                        inDoubleСolon = false;
                }
                else if (inSingleLineComment)
                {
                    if (currentChar == '\n')
                        inSingleLineComment = false;
                }
                else
                {
                    if (currentChar == '\'')
                        inSingleQuote = true;

                    else if (currentChar == '"')
                        inDoubleQuote = true;

                    else if (currentChar == '/' && query[i + 1] == '*')
                        inMultiLineComment = true;

                    else if (currentChar == '-' && query[i + 1] == '-')
                        inSingleLineComment = true;

                    else if (currentChar == ':' && query[i + 1] == ':')
                        inDoubleСolon = true;

                    else if (currentChar == ':' && !char.IsLetterOrDigit(query[i - 1]) && (i + 1 < length) && Character.isJavaIdentifierStart(query[i + 1]))
                    {
                        int j = i + 2;
                        while (j < length && Character.isJavaIdentifierPart(query[j]))
                            j++;


                        string name = query.Substring(i + 1, j - (i + 1));
                        localOrderedParameters.Add(name);

                        currentChar = '?'; // replace the parameter with a question mark
                        i += name.Length; // skip past the end if the parameter
                    }
                }
                parsedQuery.append(currentChar);
            }
            return new ParseResult(parsedQuery.ToString(), localOrderedParameters);
        }
    }
}
