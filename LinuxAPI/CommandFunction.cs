
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace LinuxAPI {
    public class CommandFunction {

        [Function("ExecuteCommand")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "execute")]
            HttpRequestData req,
            FunctionContext ctx,
            CancellationToken cancellationToken = default) {

            var logger = ctx.GetLogger(nameof(CommandFunction));
            logger.LogInformation($"{nameof(CommandFunction)}.{nameof(Run)} began processing a request.");

            var body = await req.ReadFromJsonAsync<Command>(cancellationToken);



            var resp = req.CreateResponse();
            await resp.WriteAsJsonAsync("Welcome to Azure Functions!", cancellationToken);
            return resp;
        }
    }
}
