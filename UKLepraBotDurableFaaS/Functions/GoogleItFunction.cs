using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Linq;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Telegram.Bot.Requests;

namespace UKLepraBotDurableFaaS.Functions
{
    public static class GoogleItFunction
    {
        private static string[] _rubbish = new[] { ".", ",", "-", "=", "#", "!", "?", "%", "@", "\"", "£", "$", "^", "&", "*", "(", ")", "_", "+", "]", "[", "{", "}", ";", ":", "~", "/", "<", ">", };

        [FunctionName("GoogleItFunction")]
        public static SendMessageRequest Run([ActivityTrigger] IDurableActivityContext context, ILogger log)
        {
            log.LogInformation("Processing GoogleItFunction");

            SendMessageRequest reply = null;

            try
            {
                var input = context.GetInput<Message>();

                var messageText = input.Text.ToLower() ?? string.Empty;

                if (messageText.ToLower().Contains("погугли"))
                {
                    var text = GoogleCommand(input);

                    reply = new SendMessageRequest(input.Chat.Id, text) { ReplyToMessageId = input.MessageId, DisableWebPagePreview = true, ParseMode = ParseMode.MarkdownV2};
                }
            }
            catch (Exception e)
            {
                log.LogError(e, "Error while processing AI function");
            }

            return reply;
        }

        private static string GoogleCommand(Message message)
        {
            var activationWord = "погугли";

            var cleanedMessageText = message.Text;
            _rubbish.ToList().ForEach(x => cleanedMessageText = cleanedMessageText.Replace(x, " "));

            var messageParts = cleanedMessageText.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var activationWordPosition = messageParts.FindIndex(x => x.Equals(activationWord));
            if (activationWordPosition == -1 || activationWordPosition > 3) return string.Empty;

            var queryParts = messageParts.Skip(activationWordPosition + 1);
            if (!queryParts.Any()) return string.Empty;

            var query = string.Join("%20", queryParts);
            var reply = $"[Самому слабо было?](http://google.co.uk/search?q={query})";

            return reply;
        }
    }
}
