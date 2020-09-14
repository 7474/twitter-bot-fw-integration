using Microsoft.Bot.Connector.DirectLine;
using System.Collections.Generic;
using Tweetinvi.Models;
using TwitterBotFWIntegration.Models;

namespace TwitterBotFWIntegration.Cache
{
    /// <summary>
    /// Interface for keeping the message and user identifiers in memory to ensure successful
    /// message routing between Twitter and Direct Line despite the asynchronous behaviour, which
    /// otherwise might cause missing replies to the user.
    /// </summary>
    public interface IDirectLineConversationCache
    {
        // For Conversation
        bool PutConversation(IdAndTimestamp conversationId, ConversationContext conversationContext);
        ConversationContext GetConversation(IdAndTimestamp conversationId);
        IEnumerable<ConversationContext> GetConversations();
    }
}
