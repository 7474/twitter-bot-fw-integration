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
    public interface IConversationCache
    {
        /// <summary>
        /// Checks the pending replies ready to be sent and packages them into a list.
        /// </summary>
        /// <returns>The list of message-recipient bundles ready to be sent out.
        /// The list guaranteed to always be non-null, but can be empty.</returns>
        IList<ActivityForTwitterUserBundle> GetPendingRepliesToTwitterUsers();

        /// <param name="messageIdAndTimestamp">The Direct Line message ID (and timestamp) to
        /// identify the Twitter user to reply to.</param>
        /// <returns>The identifier of the Twitter user waiting for reply or null if not found.</returns>
        TwitterUserIdentifier GetTwitterUserWaitingForReply(IdAndTimestamp messageIdAndTimestamp);

        /// <summary>
        /// Adds the identifier of the Twitter user waiting for a reply to a message with
        /// the given ID (and timestamp).
        /// </summary>
        /// <param name="messageIdAndTimestamp">The Direct Line message ID (and timestamp) to
        /// identify the Twitter user to reply to.</param>
        /// <param name="twitterUserIdentifier"></param>
        /// <returns>True, if added successfully. False otherwise (e.g. alreay exists).</returns>
        bool AddTwitterUserWaitingForReply(
            IdAndTimestamp messageIdAndTimestamp, TwitterUserIdentifier twitterUserIdentifier);

        /// <summary>
        /// Removes the Twitter user (identifier) matching the given message ID.
        /// </summary>
        /// <param name="messageIdAndTimestamp">The Direct Line message ID (and timestamp) matching the record to remove.</param>
        /// <returns>True, if successfully removed. False otherwise.</returns>
        bool RemoveTwitterUserWaitingForReply(IdAndTimestamp messageIdAndTimestamp);

        /// <param name="messageIdAndTimestamp">The Direct Line message ID (and timestamp) to
        /// identify the pending reply (Activity).</param>
        /// <returns>The pending reply (Activity) matching the given ID or null if not found.</returns>
        Activity GetPendingReplyFromBotToTwitterUser(IdAndTimestamp messageIdAndTimestamp);

        /// <summary>
        /// Adds the given pending reply (Activity) with the message ID as a key.
        /// </summary>
        /// <param name="messageIdAndTimestamp">The Direct Line message ID (and timestamp) to
        /// identify the pending reply.</param>
        /// <param name="pendingReplyActivity">The pending reply.</param>
        /// <returns>True, if added successfully. False otherwise (e.g. alreay exists).</returns>
        bool AddPendingReplyFromBotToTwitterUser(
            IdAndTimestamp messageIdAndTimestamp, Activity pendingReplyActivity);

        /// <summary>
        /// Removes the pending reply (Activity) from the collection matching the given ID.
        /// </summary>
        /// <param name="messageIdAndTimestamp">The Direct Line message ID (and timestamp) matching the record to remove.</param>
        /// <returns>True, if successfully removed. False otherwise.</returns>
        bool RemovePendingReplyFromBotToTwitterUser(IdAndTimestamp messageIdAndTimestamp);


        // For reply
        // TODO MessageIdAndTimestamp ではなく会話のIDにして最新の返信先を持つ
        // TODO ITweetではなく必要な情報を持つオブジェクトにする
        bool PutLatestTweetOfConversation(IdAndTimestamp conversationIdAndTimestamp, ITweet tweet);
        ITweet GetLatestTweetOfConversation(IdAndTimestamp conversationIdAndTimestamp);

        // For tweet to converstion
        bool PutConversationOfTweet(IdAndTimestamp tweetId, string conversationId);
        string GetConversationOfTweet(IdAndTimestamp tweetId);
    }
}
