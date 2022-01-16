using System.Threading.Tasks;
using Quantic.Core;
using Transfers.API.Model;
using Transfers.API.Query;

namespace Transfers.API.Controllers
{
    public class DoTransferRequest
    {
        public string From { get; set; }
        public string To { get; set; }
        public double Amount { get; set; }
    }
}
