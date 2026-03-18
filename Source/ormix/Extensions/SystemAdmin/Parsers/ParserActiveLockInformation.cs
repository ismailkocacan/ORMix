using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ormix.Extensions.SystemAdmin.Parsers
{
    public class ActiveLock
    {
        public string address { get; set; } = null!;

        public ulong laddress 
            => ulong.TryParse(address, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong iAddress) ? iAddress : 0;

        public string wtlist { get; set; } = null!;

        public string owner { get; set; } = null!;

        public ulong lowner 
            => ulong.TryParse(owner, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong iAddress) ? iAddress : 0;

        public string lklist { get; set; } = null!;

        public string type { get; set; } = null!;

        public int tblsnum { get; set; }

        public int rowid { get; set; }

        public int keybsiz { get; set; }
    }

    public class ParserActiveLockInformation : Parser<ActiveLock>
    {
        public override ActiveLock? FindParse(string data, string searchString)
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

        public override List<ActiveLock> Parse(string data)
        {
            var result = new List<ActiveLock>();
            if (string.IsNullOrWhiteSpace(data))
                return result;

            string[] lines = data.Split(RowSplitterSeperator, StringSplitOptions.RemoveEmptyEntries);
            Span<string> span = lines.AsSpan();
            ref string beginArray = ref MemoryMarshal.GetReference(span);
            for (int i = 3; i < span.Length; i++)
            {
                ref string line = ref Unsafe.Add(ref beginArray, i);
                var activeLock = ParseLine(line);
                if (activeLock != null)
                    result.Add(activeLock);
            }
            return result;
        }

        public override ActiveLock? ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            var columns = line.Split(ColumnSplitterSeperator, StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length != 8)
                return null;

            var activeLock = new ActiveLock();
            activeLock.address = columns[0];
            activeLock.wtlist = columns[1];
            activeLock.owner = columns[2];
            activeLock.lklist = columns[3];
            activeLock.type = columns[4];
            activeLock.tblsnum = ParseInt(columns[5]);
            activeLock.rowid = ParseInt(columns[6]);
            activeLock.keybsiz = ParseInt(columns[7]);
            return activeLock;
        }
    }
}
