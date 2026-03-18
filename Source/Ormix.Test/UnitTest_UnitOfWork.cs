using Microsoft.Extensions.DependencyInjection;
using Ormix.Transactions.UnitOfWork;
using Xunit.Extensions.AssemblyFixture;

namespace Ormix.Test
{
    public class UnitTest_UnitOfWork : IAssemblyFixture<ConnectionFixture>
    {
        private readonly IUnitOfWork unitOfWork;

        public UnitTest_UnitOfWork(ConnectionFixture connectionFixture)
        {
            unitOfWork = connectionFixture
                .ConnectionScope
                .ServiceProvider
                .GetRequiredService<IUnitOfWork>();
        }

        [Fact]
        public void Test_Resolve()
        {
            Assert.NotNull(unitOfWork);
        }


        [Fact]
        public void Test_Transaction()
        {
            try
            {
                unitOfWork.BeginTransaction();

                unitOfWork.Complete(true);
                Assert.True(true);
            }
            catch (Exception ex)
            {
                unitOfWork.Rollback();
                throw;
            }
        }


        [Fact]
        public void Test_NotInTransaction()
        {
            try
            {
                unitOfWork.Complete(true);
                Assert.True(true);
            }
            catch (Exception ex)
            {
                unitOfWork.Rollback();
                throw;
            }
        }
    }
}
