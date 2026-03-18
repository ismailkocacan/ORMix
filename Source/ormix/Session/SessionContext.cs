namespace Ormix.Session
{
    public interface ISessionContext : IDisposable
    {
        int SessionId { get; set; }
        int ThreadId { get; set; }
        string? Client { get; set; }
    }

    internal class SessionContext : ISessionContext
    {
        public int SessionId { get; set; }
        public int ThreadId { get; set; }
        public string? Client { get; set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // Cleanup resources
        }

        ~SessionContext()
        {
            Dispose(false);
        }
    }
}
