using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot.Types;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Telegram.Bot.Requests;
using Telegram.Bot.Types.InputFiles;
using System.Text.RegularExpressions;

namespace UKLepraBotDurableFaaS.Functions
{
    public static class ReactionFunction
    {
        private static Random _rnd = new Random();

        [FunctionName("ReactionFunction")]
        public static RequestBase<Message> Run([ActivityTrigger] IDurableActivityContext context, ILogger log)
        {
            log.LogInformation("Processing ReactionFunction");

            RequestBase<Message> reply = null;

            try
            {
                var input = context.GetInput<Tuple<Message, ReactionsList, ChatSettings>>();
                var message = input.Item1;
                var reactionsList = input.Item2;
                var chatSettings = input.Item3;

                if(IsReaction(message, reactionsList, out var reaction))
                {
                    var reactionReply = DoReaction(reaction);
                    if(string.IsNullOrEmpty(reactionReply.Text) == false)
                    {
                        reply = new SendMessageRequest(message.Chat.Id, reactionReply.Text) { ReplyToMessageId = message.MessageId};
                    }
                    else if(string.IsNullOrEmpty(reactionReply.Sticker) == false)
                    {
                        var sticker = new InputOnlineFile(reactionReply.Sticker);
                        reply = new SendStickerRequest(message.Chat.Id, sticker) {ReplyToMessageId = message.MessageId };
                    }
                }
                else
                {
                    var huifiedMessage = HuifyMeInternal(message.Text);
                    reply = new SendMessageRequest(message.Chat.Id, huifiedMessage) { ReplyToMessageId = message.MessageId };
                }
            }
            catch (Exception e)
            {
                log.LogError(e, "Error while processing Reaction function");
            }

            return reply;
        }

        private static Reply DoReaction(Reaction reaction)
        {
            var reactionReply = reaction.Replies.Count <= 1 ? reaction.Replies.FirstOrDefault() : reaction.Replies[HelperMethods.RandomInt(reaction.Replies.Count)];
            return reactionReply;
        }

        private static bool IsReaction(Message message, ReactionsList reactionsList, out Reaction reaction)
        {
            var messageText = message.Text?.ToLower() ?? string.Empty;

            reaction = reactionsList.Items.FirstOrDefault(x => x.Triggers.Any(messageText.Contains));

            if (reaction == null) return false;
            if (reaction.IsMentionReply && HelperMethods.MentionsBot(message) == false) return false;
            if (reaction.IsAlwaysReply == false && HelperMethods.YesOrNo() == false) return false;

            return true;
        }

        private static string HuifyMeInternal(string message)
        {
            var vowels = "оеаяуюы";
            var rulesValues = "еяюи";
            var rules = new Dictionary<string, string>
            {
                {"о", "е"},
                {"а", "я"},
                {"у", "ю"},
                {"ы", "и"}
            };
            var nonLettersPattern = new Regex("[^а-яё-]+");
            var onlyDashesPattern = new Regex("^-*$");
            var prefixPattern = new Regex("^[бвгджзйклмнпрстфхцчшщьъ]+");

            var messageParts = message.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            if (messageParts.Length < 1) return String.Empty;

            var word = messageParts[messageParts.Length - 1];
            var prefix = string.Empty;

            if (messageParts.Length > 1 && _rnd.Next(0, 1) == 1)
            {
                prefix = messageParts[messageParts.Length - 2];
            }

            word = nonLettersPattern.Replace(word.ToLower(), "");
            if (word == "бот")
            {
                return (string.IsNullOrEmpty(prefix) ? "" : prefix + " ") + "хуебот";
            }

            if (onlyDashesPattern.IsMatch(word))
                return string.Empty;

            var postFix = prefixPattern.Replace(word, "");
            if (postFix.Length < 3) return string.Empty;

            var foo = postFix.Substring(1, 1);
            if (word.Substring(2) == "ху" && rulesValues.Contains(foo))
            {
                return string.Empty;
            }

            if (rules.ContainsKey(postFix.Substring(0, 1)))
            {
                if (!vowels.Contains(foo))
                {
                    return (string.IsNullOrEmpty(prefix) ? "" : prefix + " ") + "ху" + rules[postFix.Substring(0, 1)] + postFix.Substring(1);
                }
                else
                {
                    if (rules.ContainsKey(foo))
                    {
                        return (string.IsNullOrEmpty(prefix) ? "" : prefix + " ") + "ху" + rules[foo] + postFix.Substring(2);
                    }
                    else
                    {
                        return (string.IsNullOrEmpty(prefix) ? "" : prefix + " ") + "ху" + postFix.Substring(1);
                    }
                }
            }
            else
            {
                return (string.IsNullOrEmpty(prefix) ? "" : prefix + " ") + "ху" + postFix;
            }
        }
    }
}
