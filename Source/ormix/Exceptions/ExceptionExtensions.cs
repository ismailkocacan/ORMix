using Ormix.ErrorCode;

namespace Ormix.Exceptions
{
    public static class ExceptionExtensions
    {
        public static bool TryGetSqlException(this Exception exception, out string message)
        {
            if (exception == null)
            {
                message = string.Empty;
                return false;
            }

            if (exception is java.sql.SQLException javaSqlException &&
                javaSqlException.getErrorCode() == -746)
            {
                message = $"DbError:{javaSqlException.getMessage()}";
                return true;
            }
            message = string.Empty;
            return false;
        }

        public static bool TryGetSqlException(this Exception exception, out int errorCode, out string message)
        {
            errorCode = 0;
            message = string.Empty;
            if (exception == null)
                return false;

            if (exception is java.sql.SQLException javaSqlException)
            {
                errorCode = javaSqlException.getErrorCode();
                message = $"ErrorCode:{errorCode} Error:{javaSqlException.getMessage()} Cause:{javaSqlException.getCause()?.Message}";
                return true;
            }
            return false;
        }

        public static bool IsJavaSqlException(this Exception exception)
          => exception is java.sql.SQLException javaSqlException && javaSqlException != null;

        public static bool IsTransientNetworkError(this Exception exception)
          => exception.TryGetSqlException(out int errorCode, out _) &&
             IfxErrorCode.IsNetworkError(errorCode);

        public static bool IsTransientLockError(this Exception exception)
          => exception.TryGetSqlException(out int errorCode, out _) &&
             IfxErrorCode.IsLockError(errorCode);

        public static bool IsTransientMemoryError(this Exception exception)
           => exception.TryGetSqlException(out int errorCode, out _) &&
               IfxErrorCode.IsMemoryError(errorCode);

        public static bool IsTransientInternalError(this Exception exception)
           => exception.TryGetSqlException(out int errorCode, out _) &&
               IfxErrorCode.IsInternalError(errorCode);
       
        public static bool IsTransientError(this Exception exception)
           => exception.TryGetSqlException(out int errorCode, out _) &&
             (IfxErrorCode.IsLockError(errorCode) ||
              IfxErrorCode.IsMemoryError(errorCode));
    }
}
