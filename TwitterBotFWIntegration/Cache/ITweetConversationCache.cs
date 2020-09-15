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
    public interface ITweetConversationCache
    {
        // For reply
        // TODO MessageIdAndTimestamp ではなく会話のIDにして最新の返信先を持つ
        // TODO ITweetではなく必要な情報を持つオブジェクトにする
        bool PutLatestTweetOfConversation(IdAndTimestamp conversationId, ITweet tweet);
        ITweet GetLatestTweetOfConversation(IdAndTimestamp conversationId);
        ITweet GetRootTweetOfConversation(IdAndTimestamp conversationId);

        // For tweet to converstion
        bool PutConversationOfTweet(IdAndTimestamp tweetId, string conversationId);
        string GetConversationOfTweet(IdAndTimestamp tweetId);
    }
}
