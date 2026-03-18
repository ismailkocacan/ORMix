using java.sql;
using Microsoft.Extensions.Logging;
using Ormix.DataSources;
using System.Data;
using System.Diagnostics;

namespace Ormix.Transactions.UnitOfWork
{
    public interface IUnitOfWork : IDisposable
    {
        bool InTransaction { get; }
        int SessionId { get; }
        int TransactionId { get; }

        IsolationLevel IsolationLevel { get; }

        string? Name { get; }

        void BeginTransaction(string name = "");
        void BeginTransaction(IsolationLevel isolationLevel, string name = "");
        void Rollback();
        void Commit();
        void Complete(bool bCommitOrRollbackFlag);
    }

    public abstract class UnitOfWorkEvent
    {
        public virtual void OnAfterBeginTransaction(IUnitOfWork unitOfWork) { }
        public virtual void OnAfterCommit(IUnitOfWork unitOfWork) { }
        public virtual void OnAfterRollback(IUnitOfWork unitOfWork) { }
        public virtual void OnAfterComplete(IUnitOfWork unitOfWork, bool bCommitOrRollbackFlag) { }
    }


    public class UnitOfWorkEventDefault : UnitOfWorkEvent
    {
        protected readonly Stopwatch stopwatch = new Stopwatch();
        public TimeSpan Elapsed => stopwatch.Elapsed;
        public long ElapsedMilliseconds => stopwatch.ElapsedMilliseconds;

        public override void OnAfterBeginTransaction(IUnitOfWork unitOfWork)
        {
            base.OnAfterBeginTransaction(unitOfWork);
            stopwatch.Reset();
            stopwatch.Start();
        }

        public override void OnAfterCommit(IUnitOfWork unitOfWork)
        {
            base.OnAfterCommit(unitOfWork);
            stopwatch.Stop();
        }

        public override void OnAfterRollback(IUnitOfWork unitOfWork)
        {
            base.OnAfterRollback(unitOfWork);
            stopwatch.Stop();
        }
    }

    public interface IUnitOfWorkManager
    {
        List<IUnitOfWork> InTransactions { get; }
        void Add(IUnitOfWork unitOfWork);
        void Remove(IUnitOfWork unitOfWork);
    }

    public class UnitOfWorkManagerDefault : IUnitOfWorkManager
    {
        private List<IUnitOfWork> inTransactions { get; }
        public List<IUnitOfWork> InTransactions => inTransactions;

        public UnitOfWorkManagerDefault()
        {
            inTransactions = new List<IUnitOfWork>();
        }

        public void Add(IUnitOfWork unitOfWork)
        {
            inTransactions.Add(unitOfWork);
        }

        public void Remove(IUnitOfWork unitOfWork)
        {
            inTransactions.Remove(unitOfWork);
        }
    }

    public class UnitOfWork : IUnitOfWork
    {
        private string? name;

        private bool isDisposed;

        private IsolationLevel isolationLevel;
        private readonly UnitOfWorkEvent? unitOfWorkEvent;
        private readonly ConnectionContext connectionFactory;
        private readonly IUnitOfWorkManager unitOfWorkManager;
        private readonly ILogger<UnitOfWork> logger;
        private bool isTransactionStarted = false;
        private bool isTransactionCompleted = false; // commited or rollaback

        public bool InTransaction => isTransactionStarted;

        public int SessionId => connectionFactory.SessionId;

        public string? Name => this.name;

        private int transactionId = 0;
        public int TransactionId => transactionId;

        public IsolationLevel IsolationLevel => isolationLevel;

        public UnitOfWork(
            UnitOfWorkEvent? unitOfWorkEvent,
            ConnectionContext connectionFactory,
            ILogger<UnitOfWork> logger,
            IUnitOfWorkManager unitOfWorkManager)
        {
            this.unitOfWorkEvent = unitOfWorkEvent;
            this.logger = logger;

            this.connectionFactory = connectionFactory ??
                throw new ArgumentNullException(nameof(connectionFactory));

            this.unitOfWorkManager = unitOfWorkManager ??
                throw new ArgumentNullException(nameof(unitOfWorkManager));
        }

        public void BeginTransaction(string name = "")
        {
            BeginTransaction(IsolationLevel.ReadCommitted, name);
        }

        public void BeginTransaction(IsolationLevel isolationLevel, string name = "")
        {
            this.name = name;
            this.isolationLevel = isolationLevel;
            switch (isolationLevel)
            {
                case IsolationLevel.Unspecified:
                    throw new NotImplementedException(nameof(IsolationLevel.Unspecified));
                case IsolationLevel.Chaos:
                    throw new NotImplementedException(nameof(IsolationLevel.Chaos));
                case IsolationLevel.ReadUncommitted:
                    connectionFactory?.Connection?
                        .setTransactionIsolation(Connection.TRANSACTION_READ_UNCOMMITTED);
                    break;
                case IsolationLevel.ReadCommitted:
                    connectionFactory?.Connection?
                        .setTransactionIsolation(Connection.TRANSACTION_READ_COMMITTED);
                    break;
                case IsolationLevel.RepeatableRead:
                    connectionFactory?.Connection?
                        .setTransactionIsolation(Connection.TRANSACTION_REPEATABLE_READ);
                    break;
                case IsolationLevel.Serializable:
                    connectionFactory?.Connection?
                        .setTransactionIsolation(Connection.TRANSACTION_SERIALIZABLE);
                    break;
                case IsolationLevel.Snapshot:
                    throw new NotImplementedException(nameof(IsolationLevel.Snapshot));
                default:
                    connectionFactory?.Connection?
                        .setTransactionIsolation(Connection.TRANSACTION_READ_COMMITTED);
                    break;
            }
            this.setAutoCommit(false);
            this.isTransactionStarted = true;

            this.transactionId = connectionFactory?.TransactionId() ?? 0;

            unitOfWorkManager.Add(this);
            unitOfWorkEvent?.OnAfterBeginTransaction(this);
        }

        public void Commit()
        {
            if (!this.isTransactionStarted)
                return;

            connectionFactory?.Connection?.commit();
            this.setAutoCommit(true);

            this.isTransactionStarted = false;
            this.isTransactionCompleted = true;

            unitOfWorkManager.Remove(this);
            unitOfWorkEvent?.OnAfterCommit(this);
        }

        public void Rollback()
        {
            if (!this.isTransactionStarted)
                return;
            InternalRollback();
        }

        private void setAutoCommit(bool autoCommit)
            => connectionFactory?.Connection?.setAutoCommit(autoCommit);


        private void InternalRollback()
        {
            if (this.IsValidConnection())
            {
                connectionFactory?.Connection?.rollback();
                this.setAutoCommit(true);
                unitOfWorkManager.Remove(this);
                unitOfWorkEvent?.OnAfterRollback(this);
            }

            this.isTransactionStarted = false;
            this.isTransactionCompleted = true;
        }

        private bool IsValidConnection()
        {
            if (this.connectionFactory == null)
                return false;

            if (this.connectionFactory.Connection == null)
                return false;

            if (this.connectionFactory.Connection.isClosed())
                return false;

            return true;
        }


        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed) return;
            if (disposing)
            {
                bool startedTransactionIsNotCompleted = isTransactionStarted && !isTransactionCompleted;
                if (startedTransactionIsNotCompleted)
                    InternalRollback();
            }
            isDisposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Complete(bool bCommitOrRollbackFlag)
        {
            if (bCommitOrRollbackFlag)
                Commit();
            else
                Rollback();

            unitOfWorkEvent?.OnAfterComplete(this, bCommitOrRollbackFlag);
        }

        ~UnitOfWork()
        {
            Dispose(false);
        }
    }
}
