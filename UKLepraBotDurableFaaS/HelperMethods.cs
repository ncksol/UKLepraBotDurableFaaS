﻿using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Bot.Types;

namespace UKLepraBotDurableFaaS
{
    public class HelperMethods
    {
        public static bool YesOrNo()
        {
            var rnd = new Random();
            return rnd.Next() % 2 == 0;
        }

        public static int RandomInt(int max)
        {
            var rnd = new Random();
            return rnd.Next(max);
        }

        public static bool MentionsId(Message message, string id)
        {
            if (string.IsNullOrEmpty(message.Text)) return false;

            return message.Text.Contains($"@{id}");
        }

        public static bool MentionsBot(Message message)
        {
            return MentionsId(message, Configuration.Instance.TelegramBotId);
        }
    }
}
