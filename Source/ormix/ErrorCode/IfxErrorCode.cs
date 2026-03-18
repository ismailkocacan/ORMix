using System.Reflection;

namespace Ormix.ErrorCode
{
    public enum IfxErrorCategory
    {
        None = 0,
        LockError = 1,
        NetworkCommunicationErrors = 2,
        MemoryErrors = 3,
        InternalError = 4,
    }

    /// <summary>
    /// https://www.ibm.com/docs/kk/informix-servers/12.10.0?topic=informix-error-messages
    /// </summary>
    public class IfxErrorCode
    {
        public int ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public IfxErrorCategory ErrorCategory { get; set; }

        private readonly static Dictionary<int, IfxErrorCode> errorCodes;

        static IfxErrorCode()
        {
            var allIfxErrorCodes = typeof(IfxErrorCode)
                   .GetFields(BindingFlags.Public | BindingFlags.Static)
                   .Where(f => f.FieldType == typeof(IfxErrorCode))
                   .Select(f => (IfxErrorCode)f.GetValue(null)!)
                   .ToList();

            var duplicates = allIfxErrorCodes
                .GroupBy(ifxErrorCode => ifxErrorCode.ErrorCode)
                .Where(group => group.Count() > 1)
                .ToList();

            if (duplicates.Any())
            {
                var sduplicates = duplicates.Select(g => $"Value {g.Key} is duplicated for: {string.Join(", ", g.Select(arc => arc.ErrorCode))}");
                var message = string.Join("\n", sduplicates);
                throw new ApplicationException($"Duplicate values found:{message}");
            }

            errorCodes = allIfxErrorCodes.ToDictionary(ifxError => ifxError.ErrorCode);
        }


        public IfxErrorCode(int errorCode, string errorMessage, IfxErrorCategory errorCategory = IfxErrorCategory.None)
        {
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            ErrorCategory = errorCategory;
        }

        public override string ToString() => $"{ErrorCode} ({ErrorMessage}) ";

        public override bool Equals(object? obj)
        {
            if (obj is not IfxErrorCode otherValue)
                return false;

            var typeMatches = GetType().Equals(obj.GetType());
            var valueMatches = ErrorCode.Equals(otherValue.ErrorCode);
            return typeMatches && valueMatches;
        }
        public override int GetHashCode()
            => base.GetHashCode();


        public static bool operator ==(IfxErrorCode left, IfxErrorCode right)
        {
            if (left is null || right is null)
                return false;

            if (ReferenceEquals(left, right))
                return true;

            return left.Equals(right);
        }

        public static bool operator !=(IfxErrorCode left, IfxErrorCode right)
          => !(left == right);

        public static explicit operator IfxErrorCode?(int value)
          => errorCodes.TryGetValue(value, out IfxErrorCode? ret) ? ret : null;

        public static bool TryFind(int value, out IfxErrorCode? ifxErrorCode)
        {
            ifxErrorCode = (IfxErrorCode?)value;
            return !(ifxErrorCode is null);
        }

        public static string? ParseLockErrorTableName(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
                return string.Empty;

            var items = errorMessage.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (items != null && items.Length > 1)
            {
                string tableName = items[1]
                    .Replace(oldChar: ')', newChar: ' ')
                    .Replace(oldChar: '.', newChar: ' ')
                    .Replace(oldChar: ':', newChar: ' ');
                return tableName.Trim();
            }
            return string.Empty;
        }

        public static bool IsNetworkError(int errorCode)
        {
            bool bResult = TryFind(errorCode, out IfxErrorCode? ifxErrorCode);
            return bResult ? ifxErrorCode is not null && ifxErrorCode.ErrorCategory == IfxErrorCategory.NetworkCommunicationErrors : false;
        }

        public static bool IsLockError(int errorCode)
        {
            bool bResult = TryFind(errorCode, out IfxErrorCode? ifxErrorCode);
            return bResult ? ifxErrorCode is not null && ifxErrorCode.ErrorCategory == IfxErrorCategory.LockError : false;
        }

        public static bool IsMemoryError(int errorCode)
        {
            bool bResult = TryFind(errorCode, out IfxErrorCode? ifxErrorCode);
            return bResult ? ifxErrorCode is not null && ifxErrorCode.ErrorCategory == IfxErrorCategory.MemoryErrors : false;
        }

        public static bool IsInternalError(int errorCode)
        {
            bool bResult = TryFind(errorCode, out IfxErrorCode? ifxErrorCode);
            return bResult ? ifxErrorCode is not null && ifxErrorCode.ErrorCategory == IfxErrorCategory.InternalError : false;
        }

        // memory errors
        public static readonly IfxErrorCode MemoryAllocFailed = new(errorCode: -208, errorMessage: "Memory allocation failed during query processing.", errorCategory: IfxErrorCategory.MemoryErrors);

        // lock errors
        public static readonly IfxErrorCode LockErrorPosWithTable = new(errorCode: -243, errorMessage: "Could not position within a table table-name.", errorCategory: IfxErrorCategory.LockError);
        public static readonly IfxErrorCode LockErrorFetchNextRow = new(errorCode: -244, errorMessage: "Could not do a physical-order read to fetch next row.", errorCategory: IfxErrorCategory.LockError);
        public static readonly IfxErrorCode LockErrorFileViaIndex = new(errorCode: -245, errorMessage: "Could not position within a file via an index.", errorCategory: IfxErrorCategory.LockError);
        public static readonly IfxErrorCode LockErrorGetNextRow = new(errorCode: -246, errorMessage: "Could not do an indexed read to get the next row.", errorCategory: IfxErrorCategory.LockError);

        // InternalErrors
        public static readonly IfxErrorCode SystemOrInternalError = new(errorCode: -79716, errorMessage: "An operating or runtime system error or a driver internal error occurred.", errorCategory: IfxErrorCategory.InternalError);


        //network connections errors
        public static readonly IfxErrorCode ConnectionNotEstablished = new(errorCode: -79730, errorMessage: "Connection not established", errorCategory: IfxErrorCategory.NetworkCommunicationErrors);
        public static readonly IfxErrorCode ConnectionsAreAllowedIinQuiescentMode = new(errorCode: -27002, errorMessage: "connections are allowed in quiescent mode", errorCategory: IfxErrorCategory.NetworkCommunicationErrors);
        public static readonly IfxErrorCode NetworkConnectionIsBroken = new(errorCode: -25582, errorMessage: "Network connection is broken.", errorCategory: IfxErrorCategory.NetworkCommunicationErrors);
        public static readonly IfxErrorCode UnknownNetworkError = new(errorCode: -25583, errorMessage: "Unknown network error.", errorCategory: IfxErrorCategory.NetworkCommunicationErrors);
        public static readonly IfxErrorCode NetworkSendFailed = new(errorCode: -25586, errorMessage: "Network send failed.", errorCategory: IfxErrorCategory.NetworkCommunicationErrors);
        public static readonly IfxErrorCode NetworkReceiveFailed = new(errorCode: -25587, errorMessage: "Network receive failed.", errorCategory: IfxErrorCategory.NetworkCommunicationErrors);
        public static readonly IfxErrorCode AttemptToConnectToDatabaseServer = new(errorCode: -908, errorMessage: "Attempt to connect to database server(servername) failed.", errorCategory: IfxErrorCategory.NetworkCommunicationErrors);
    }
}
