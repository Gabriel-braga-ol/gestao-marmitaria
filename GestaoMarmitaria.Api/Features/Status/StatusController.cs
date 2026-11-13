using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoMarmitaria.Api.Features.Status
{
    [Authorize]
    [ApiController] //habilita comportamentos próprios de controllers de API
    [Route("api/[controller]")] //define a rota de acesso base
    public class StatusController : ControllerBase
    {
        [HttpGet] //define que o metodo atende requisições Get
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                sistema = "GestaoMarmitaria",
                status = "online"
            });   
        }
    }
}
