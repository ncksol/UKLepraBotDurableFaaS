using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace UKLepraBotDurableFaaS.Functions
{
    public static class InputFunction
    {
        [FunctionName("InputFunction")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            [DurableClient] IDurableOrchestrationClient orchestrationClient,
            ILogger log)
        {
            log.LogInformation("Processing InputFuction");

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();                
                var update = JsonConvert.DeserializeObject<Update>(requestBody);
                if(update.Type == UpdateType.Message)
                    await orchestrationClient.StartNewAsync("ProcessMessageFunction", update.Message);
            }
            catch (Exception e)
            {
                log.LogError(e, "Error while incoming message");
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }
    }
}
