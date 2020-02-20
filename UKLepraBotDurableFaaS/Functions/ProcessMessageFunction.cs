using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace UKLepraBotDurableFaaS.Functions
{
    public static class ProcessMessageFunction
    {
        private static ILogger _log;
        private static TelegramBotClient _bot;
        private static IDurableOrchestrationContext _context;
        private static Random _rnd = new Random();

        private static readonly List<string> _settingsFunctionActivators = new List<string> { "/status", "/huify", "/unhuify", "/uptime", "/delay", "/secret", "/reload" };
        public static readonly List<string> _aiFunctionActivators = new List<string> { "погугли" };

        private static ReactionsList _reactions;
        private static ChatSettings _chatSettings;

        [FunctionName("ProcessMessageFunction")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            [Blob(Constants.ReactionsBlobPath)] string reactionsString,
            [Blob(Constants.ChatSettingsBlobPath)] string chatSettingsString,
            [Blob(Constants.DataBlobPath)] CloudBlobContainer chatSettingsOutput,
            ILogger log)
        {
            _log = log;
            _context = context;

            try
            {
                _bot = new TelegramBotClient(Configuration.Instance.BotToken);
                _reactions = JsonConvert.DeserializeObject<ReactionsList>(reactionsString);
                _chatSettings = JsonConvert.DeserializeObject<ChatSettings>(chatSettingsString);

                var me = await _bot.GetMeAsync();
                if (me == null)
                    throw new Exception("Bot initialisation failed");

                var message = context.GetInput<Message>();
                var reply = await BotOnMessageReceived(message);
                await _bot.MakeRequestAsync(reply);                
            }
            catch (Exception e)
            {
                log.LogError(e, "Error while running output function");
            }
        }

        private static async Task<RequestBase<Message>> BotOnMessageReceived(Message message)
        {
            RequestBase<Message> reply = null;

            if (message.Type == MessageType.ChatMembersAdded || message.Type == MessageType.ChatMemberLeft)
            {
                _log.LogInformation("Matched chatmemebersupdate queue");

                reply = await _context.CallActivityAsync<RequestBase<Message>>("ChatMembersUpdateFunction", message);
            }
            else if (message.Type == MessageType.Text)
            {
                reply = await ProcessTextMessage(message);
            }

            return reply;
        }

        private async static Task<RequestBase<Message>> ProcessTextMessage(Message message)
        {
            RequestBase<Message> reply = null;

            if (HelperMethods.MentionsBot(message) && !string.IsNullOrEmpty(message.Text) && _settingsFunctionActivators.Any(x => message.Text.ToLower().Contains(x)))
            {
                _log.LogInformation("Matched settings function");

                var tuple = await _context.CallActivityAsync<Tuple<RequestBase<Message>, ChatSettings>>("SettingsFunction", new Tuple<Message, ChatSettings>(message, _chatSettings));
                _chatSettings = tuple.Item2;
                reply = tuple.Item1;
            }
            else if (!string.IsNullOrEmpty(message.Text) && _aiFunctionActivators.Any(x => message.Text.ToLower().Contains(x)))
            {
                _log.LogInformation("Matched GoogleIt function");

                reply = await _context.CallActivityAsync<RequestBase<Message>>("GoogleItFunction", message);
            }
            else
            {
                _log.LogInformation("Matched reaction function");

                if (IsReaction(message, out var reaction))
                {
                    reply = await _context.CallActivityAsync<RequestBase<Message>>("ReactionFunction", new Tuple<Message, Reaction>(message, reaction));
                }
                //if not a special reaction check chat settings if should react to the message
                else if (ShouldProcessMessage(_chatSettings, message))
                {
                    reply = await _context.CallActivityAsync<RequestBase<Message>>("ReactionFunction", new Tuple<Message, Reaction>(message, null));
                }
            }

            return reply;
        }


        private static bool IsReaction(Message message, out Reaction reaction)
        {
            var messageText = message.Text?.ToLower() ?? string.Empty;

            reaction = _reactions.Items.FirstOrDefault(x => x.Triggers.Any(messageText.Contains));

            if (reaction == null) return false;
            if (reaction.IsMentionReply && HelperMethods.MentionsBot(message) == false) return false;
            if (reaction.IsAlwaysReply == false && HelperMethods.YesOrNo() == false) return false;

            return true;
        }


        private static bool ShouldProcessMessage(ChatSettings chatSettings, Message message)
        {
            var conversationId = message.Chat.Id.ToString();

            var state = chatSettings.State;
            var delay = chatSettings.Delay;
            var delaySettings = chatSettings.DelaySettings;
            if (!state.ContainsKey(conversationId) || !state[conversationId])//huify is not active or was never activated
                return false;

            if (delay.ContainsKey(conversationId))
            {
                if (delay[conversationId] > 0)
                    delay[conversationId] -= 1;
            }
            else
            {
                Tuple<int, int> delaySetting;
                if (delaySettings.TryGetValue(conversationId, out delaySetting))
                    delay[conversationId] = _rnd.Next(delaySetting.Item1, delaySetting.Item2 + 1);
                else
                    delay[conversationId] = _rnd.Next(4);
            }

            if (delay[conversationId] != 0 || message.From.Id.ToString() == Configuration.Instance.MasterId) return false;

            delay.Remove(conversationId);

            return true;
        }
    }
}