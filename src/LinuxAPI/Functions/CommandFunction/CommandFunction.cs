using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Scriptable.LinuxAPI.Functions.CommandFunction.Models;
using Scriptable.LinuxAPI.Services;

namespace Scriptable.LinuxAPI.Functions.CommandFunction {
    public class CommandFunction {

        [Function("ExecuteCommand")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "execute")]
            HttpRequestData req,
            FunctionContext ctx,
            IShellProvider shellProvider,
            CancellationToken cancellationToken = default) {

            var logger = ctx.GetLogger(nameof(CommandFunction));
            logger.LogInformation($"{nameof(CommandFunction)}.{nameof(Run)} began processing a request.");

            var body = await req.ReadFromJsonAsync<CommandModel>(cancellationToken);
            if (body == null) {
                req.CreateResponse(System.Net.HttpStatusCode.NotFound);
            }

            var shell = shellProvider.GetShell(body!.Options?.Shell ?? ShellType.Bash, cancellationToken);
            var cmd = await shell.Run(body.FilePath, body.Arguments).Task;

            var result = new CommandResultModel() {
                Success = cmd.Success,
                ExitCode = cmd.ExitCode,
                StdOut = cmd.StandardOutput,
                StdErr = cmd.StandardError
            };

            var resp = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await resp.WriteAsJsonAsync(result, cancellationToken);
            return resp;
        }
    }
}
