using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Domain.Exceptions
{
    public class NotFoundException : BaseException
    {
        public NotFoundException(string entityName, object key)
            : base($"{entityName} with ID ({key}) not exist.", 404) { }
    }
}
