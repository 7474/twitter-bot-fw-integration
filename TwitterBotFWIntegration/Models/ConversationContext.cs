using Microsoft.Bot.Connector.DirectLine;
using System;
using System.Collections.Generic;
using System.Text;

namespace TwitterBotFWIntegration.Models
{
    public class ConversationContext
    {
        public ConversationContext(Conversation conversation, string activityWaterMark)
        {
            Conversation = conversation;
            ActivityWaterMark = activityWaterMark;
        }

        public Conversation Conversation { get; private set; }
        public string ActivityWaterMark { get; private set; }
    }
}
