using System.Net.Mime;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Quantic.Core;
using Quantic.Web;
using Transfers.API.Commands;

namespace Transfers.API.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{v:apiVersion}")]
    [ApiController]
    public class TransfersController : BaseController
    {
        private readonly ICommandHandler<DoTransfer> doTransferHandler;

        public TransfersController(ICommandHandler<DoTransfer> doTransferHandler)
        {
            this.doTransferHandler = doTransferHandler;
        }

        [HttpPost("transfers")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(Error[]), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(Error[]), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(Error[]), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAccount([FromBody] DoTransferRequest request)
        {
            var doTransfer = await doTransferHandler.Handle(new DoTransfer(request.From, request.To, request.Amount), Context);
            return doTransfer.Ok();
        }
    }
}
