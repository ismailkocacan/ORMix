namespace Ormix.Extensions.SystemAdmin.Parsers
{
    public abstract class Parser<T>
    {
        protected readonly char[] RowSplitterSeperator = new[] { '\n', '\r' };
        protected readonly char ColumnSplitterSeperator = ' ';

        public abstract T? FindParse(string data, string searchString);
        public abstract List<T> Parse(string data);
        public abstract T? ParseLine(string line);

        protected long ParseLong(string value)
          => long.TryParse(value, out long result) ? result : 0;

        protected int ParseInt(string value)
          => int.TryParse(value, out int result) ? result : 0;
    }
}
