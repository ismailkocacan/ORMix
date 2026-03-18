using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ormix.Extensions
{
    public interface IEntityValidation
    {
        void Validate();
    }

    [Serializable]
    public class EntityValidationException : Exception
    {
        public EntityValidationException()
        {
        }

        public EntityValidationException(string message)
            : base(message)
        {
        }

        public EntityValidationException(string message, Exception inner)
            : base(message, inner)
        {
        }

        public static void Throw(string message)
        {
            throw new EntityValidationException(message);
        }
    }
}
