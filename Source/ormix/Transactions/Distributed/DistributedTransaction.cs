/* 
    Distributed Transactions
    XA = eXtended Architecture

    Overview::
    A distributed transaction, which is also called a global transaction, is a set of one or more transactions that are managed together. 
    Transactions in a distributed transaction may be located in the same database or in different databases. 
    Each transaction is called a transaction branch.

    Distributed transactions, an extended API of the JDBC 2.0 standard, 
    are provided with connection pooling functionality or the XA standard of the X/Open DTP (Distributed Transaction Processing) standards.



    Components::
    The following describes functions of XA components.

    XADataSource:
    The concepts and functionality of XADataSources are similar to those of Connection Pool DataSources and other DataSources. In each resource manager (database) used for a distributed transaction, a single XADataSource object exists, and this DataSource creates an XAConnection.

    XAConnection:
    An XAConnection is an extended pooled connection. It is conceptually and functionally similar to a pooled connection.
    An XAConnection works as a temporary handle for the physical database connection, and a single XAConnection corresponds to a single database session.

    XAResource:
    An XAResource is used by the transaction manager to combine each transaction branch of a distributed transaction. One XAResource object can be obtained from each XAConnection, and they have a 1:1 relationship.
    This means that one XAResource object corresponds to one database session. One XAResource object can have only a single running transaction branch, but it may also have stopped transactions.
    Each XAResource object operates in each relevant session, and performs functions such as start, end, prepare, commit, and rollback.

    XID:
    To identify each transaction branch, an XID (transaction ID) is used, and one XID consists of a transaction branch ID and a distributed transaction ID.


    https://en.wikipedia.org/wiki/X/Open_XA 
    https://docs.oracle.com/javase/8/docs/api/javax/sql/XAConnection.html
    https://www.ibm.com/docs/en/i/7.2?topic=transactions-example-using-jta-handle-transaction
    https://www.ibm.com/docs/en/i/7.2?topic=transactions-example-multiple-connections-that-work-transaction
    https://www.ibm.com/docs/vi/i/7.3?topic=classes-jdbc-xa-distributed-transaction-management
    https://technet.tmaxsoft.com/upload/download/online/tibero/pver-20150504-000002/tibero_jdbc/ch05.html
 */
using java.lang;
using java.net;
using javax.transaction.xa;

namespace Ormix.Transactions.Distributed
{
    public interface IDistributedTransaction
    {
        bool TwoPhaseCommit();
        bool TwoPhaseComplete(bool bCommitOrRollbackFlag);
        void Rollback();
    }

    public class DistributedTransactionManager
    {
        private readonly List<KeyValuePair<Xid, XAResource>> xaResources;

        public DistributedTransactionManager()
        {
            this.xaResources = new List<KeyValuePair<Xid, XAResource>>();
        }

        public void Add(KeyValuePair<Xid, XAResource> keyValuePair)
        {
            this.xaResources.Add(keyValuePair);
        }

        public List<KeyValuePair<Xid, XAResource>> XAResources => xaResources;
    }



    [Serializable]
    public class RollBackForDataInconsistencyException : System.Exception
    {
        public RollBackForDataInconsistencyException()
        {
        }

        public RollBackForDataInconsistencyException(string message)
            : base(message)
        {
        }

        public RollBackForDataInconsistencyException(string message, System.Exception inner)
            : base(message, inner)
        {
        }
    }

    public class DistributedTransaction(DistributedTransactionManager distributedTransactionManager)
        : IDistributedTransaction
    {
        public bool TwoPhaseCommit()
        {
            try
            {
                this.Prepare();
                this.Commit();
                return true;
            }
            catch (System.Exception ex)
            {
                this.Rollback();

                if (!(ex is RollBackForDataInconsistencyException))
                    throw;

                return false;
            }
        }


        public bool TwoPhaseComplete(bool bCommitOrRollbackFlag)
        {
            try
            {
                this.Prepare();

                if (bCommitOrRollbackFlag)
                    this.Commit();
                else
                    this.Rollback();

                return true;
            }
            catch (System.Exception ex)
            {
                this.Rollback();

                if (!(ex is RollBackForDataInconsistencyException))
                    throw;

                return false;
            }
        }



        /// <summary>
        /// First phase of a two-phase commit process.
        /// </summary>
        /// <exception cref="RollBackForDataInconsistencyException"></exception>
        private void Prepare()
        {
            foreach (var xaResource in distributedTransactionManager.XAResources)
            {
                /*
                Prepare:
                Prepares the modifications performed in the current transaction branch,
                and is the first phase of a two-phase commit process.

                If there is only one transaction in a distributed transaction, 
                it is not necessary to call the prepare() method.
                */
                if (xaResource.Value.prepare(xaResource.Key) != XAResource.XA_OK)
                    throw new RollBackForDataInconsistencyException($"Branch({xaResource.Key}) is not prepared");
            }
        }

        /// <summary>
        /// Second phase of a two-phase commit process.
        /// </summary>
        private void Commit()
        {
            foreach (var xaResource in distributedTransactionManager.XAResources)
            {
                /*
                 Commits modifications of the current transaction branch, 
                 and is the second phase of a two-phase commit process.
                 This is performed after all transaction branches are prepared.

                onePhase:
                 - true: Performs a one-phase commit operation rather than a two-phase commit.
                 - false: Performs a two-phase commit operation.
                */
                xaResource.Value.commit(xid: xaResource.Key, onePhase: false);
            }
            distributedTransactionManager.XAResources.Clear();
        }

        /// <summary>
        /// Rollback çağırısı database connection kapatılmadan önce çağırılmalıdır. 
        /// Bağlantı kapandıktan sonra çağırılması durumunda, tablo sorgulanmak istendiğinde kilitli kalmış olma ihtimali var.
        /// [Code: -244, SQL State: IX000]  Could not do a physical-order read to fetch next row.  [Script position: 765 - 766]
        /// </summary>
        public void Rollback()
        {
            foreach (var xaResource in distributedTransactionManager.XAResources)
                xaResource.Value.rollback(xid: xaResource.Key);
            distributedTransactionManager.XAResources.Clear();
        }
    }



    /// <summary>
    /// The Xid interface is a Java mapping of the X/Open transaction identifier XID structure. 
    /// 
    /// This interface specifies three accessor methods to retrieve a global transaction format ID, 
    /// global transaction ID, and branch qualifier. 
    /// 
    /// The Xid interface is used by the transaction manager and the resource managers. 
    /// 
    /// https://learn.microsoft.com/en-us/sql/connect/jdbc/understanding-xa-transactions?view=sql-server-ver16
    /// </summary>
    internal class OpenTransactionBranchIdentifier : Xid
    {
        public int formatId;
        public byte[] gtrid;
        public byte[] bqual;

        public byte[] getGlobalTransactionId()
        {
            return gtrid;
        }

        public byte[] getBranchQualifier()
        {
            return bqual;
        }

        public int getFormatId()
        {
            return formatId;
        }

        OpenTransactionBranchIdentifier(int formatId, byte[] gtrid, byte[] bqual)
        {
            this.formatId = formatId;
            this.gtrid = gtrid;
            this.bqual = bqual;
        }

        public string toString()
        {
            StringBuffer sb = new StringBuffer(512);
            sb.append("formatId=" + formatId);
            sb.append(" gtrid(" + gtrid.Length + ")={0x");
            int hexVal;
            for (int i = 0; i < gtrid.Length; i++)
            {
                hexVal = gtrid[i] & 0xFF;
                if (hexVal < 0x10)
                    sb.append("0" + Integer.toHexString(gtrid[i] & 0xFF));
                else
                    sb.append(Integer.toHexString(gtrid[i] & 0xFF));
            }
            sb.append("} bqual(" + bqual.Length + ")={0x");
            for (int i = 0; i < bqual.Length; i++)
            {
                hexVal = bqual[i] & 0xFF;
                if (hexVal < 0x10)
                    sb.append("0" + Integer.toHexString(bqual[i] & 0xFF));
                else
                    sb.append(Integer.toHexString(bqual[i] & 0xFF));
            }
            sb.append("}");
            return sb.toString();
        }

        // Returns a globally unique transaction id.
        static byte[] localIP = null;
        static int txnUniqueID = 0;

        public static Xid GetUniqueXid(int tid)
        {
            var rnd = new java.util.Random(java.lang.System.currentTimeMillis());
            txnUniqueID++;
            int txnUID = txnUniqueID;
            int tidID = tid;
            int randID = rnd.nextInt();
            byte[] gtrid = new byte[64];
            byte[] bqual = new byte[64];
            if (null == localIP)
            {
                try
                {
                    localIP = Inet4Address.getLocalHost().getAddress();
                }
                catch (java.lang.Exception ex)
                {
                    localIP = new byte[] { 0x01, 0x02, 0x03, 0x04 };
                }
            }

            java.lang.System.arraycopy(localIP, 0, gtrid, 0, 4);
            java.lang.System.arraycopy(localIP, 0, bqual, 0, 4);

            // Bytes 4 -> 7 - unique transaction id.
            // Bytes 8 ->11 - thread id.
            // Bytes 12->15 - random number generated by using seed from current time in milliseconds.
            for (int i = 0; i <= 3; i++)
            {
                gtrid[i + 4] = (byte)(txnUID % 0x100);
                bqual[i + 4] = (byte)(txnUID % 0x100);
                txnUID >>= 8;
                gtrid[i + 8] = (byte)(tidID % 0x100);
                bqual[i + 8] = (byte)(tidID % 0x100);
                tidID >>= 8;
                gtrid[i + 12] = (byte)(randID % 0x100);
                bqual[i + 12] = (byte)(randID % 0x100);
                randID >>= 8;
            }
            return new OpenTransactionBranchIdentifier(0x1234, gtrid, bqual);
        }
    }
}
