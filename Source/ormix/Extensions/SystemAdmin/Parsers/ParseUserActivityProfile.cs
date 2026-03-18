using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ormix.Extensions.SystemAdmin.Parsers
{
    public class UserThread
    {
        public string address { get; set; } = null!;

        public ulong laddress
            => ulong.TryParse(address, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong iAddress) ? iAddress : 0;

        public string flags { get; set; } = null!;

        public int sessid { get; set; }

        public string user { get; set; } = null!;

        public string tty { get; set; } = null!;

        public string wait { get; set; } = null!;

        public ulong lwait
            => ulong.TryParse(wait, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong iAddress) ? iAddress : 0;

        public int tout { get; set; }

        public int locks { get; set; }

        public int nreads { get; set; }

        public int nwrites { get; set; }
    }

    public class ParserUserActivityProfile : Parser<UserThread>
    {
        public override UserThread? FindParse(string data, string searchString)
        {
            string[] lines = data.Split(RowSplitterSeperator, StringSplitOptions.RemoveEmptyEntries);
            Span<string> span = lines.AsSpan();
            ref string beginArray = ref MemoryMarshal.GetReference(span);
            for (int i = 3; i < span.Length; i++)
            {
                ref string line = ref Unsafe.Add(ref beginArray, i);
                if (line.Contains(searchString, StringComparison.CurrentCultureIgnoreCase))
                    return ParseLine(line);
            }
            return null;
        }

        public override List<UserThread> Parse(string data)
        {
            var result = new List<UserThread>();
            if (string.IsNullOrWhiteSpace(data))
                return result;

            string[] lines = data.Split(RowSplitterSeperator, StringSplitOptions.RemoveEmptyEntries);
            Span<string> span = lines.AsSpan();
            ref string beginArray = ref MemoryMarshal.GetReference(span);
            for (int i = 3; i < span.Length; i++)
            {
                ref string line = ref Unsafe.Add(ref beginArray, i);
                var userThread = ParseLine(line);
                if (userThread != null)
                    result.Add(userThread);
            }

            return result;
        }

        public override UserThread? ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            var columns = line.Split(ColumnSplitterSeperator, StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length != 10)
                return null;

            var userThread = new UserThread();
            userThread.address = columns[0];
            userThread.flags = columns[1];
            userThread.sessid = ParseInt(columns[2]);
            userThread.user = columns[3];
            userThread.tty = columns[4];
            userThread.wait = columns[5];
            userThread.tout = ParseInt(columns[6]);
            userThread.locks = ParseInt(columns[7]);
            userThread.nreads = ParseInt(columns[8]);
            userThread.nwrites = ParseInt(columns[9]);
            return userThread;
        }
    }
}
