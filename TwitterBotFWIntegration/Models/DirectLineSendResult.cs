using Microsoft.Bot.Connector.DirectLine;
using System;
using System.Collections.Generic;
using System.Text;

namespace TwitterBotFWIntegration.Models
{
    public class DirectLineSendResult
    {
        public DirectLineSendResult(Conversation conversation, string messageId)
        {
            Conversation = conversation;
            MessageId = messageId;
        }

        public Conversation Conversation { get; private set; }
        public string MessageId { get; private set; }
    }
}
