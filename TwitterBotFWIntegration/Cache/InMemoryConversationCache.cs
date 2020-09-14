using Microsoft.Bot.Connector.DirectLine;
using System;
using System.Collections.Generic;
using System.Linq;
using Tweetinvi.Core.Extensions;
using Tweetinvi.Models;
using TwitterBotFWIntegration.Models;

namespace TwitterBotFWIntegration.Cache
{
    /// <summary>
    /// An IN-MEMORY implementation of IConversationCache interface.
    /// 
    /// This class is OK to be used for prototyping and testing, but a cloud storage based
    /// implementation is recommended to ensure reliable service in production enviroment.
    /// </summary>
    public class InMemoryConversationCache : IDirectLineConversationCache, ITweetConversationCache
    {
        // XXX 遅延応答には30秒はちょっと短い。
        protected const int DefaultMinCacheExpiryInSeconds = 30;
        protected Dictionary<IdAndTimestamp, ITweet> _converstionToLatestTweet;
        protected Dictionary<IdAndTimestamp, ITweet> _converstionToRootTweet;
        protected Dictionary<IdAndTimestamp, string> _tweetToConversation;
        protected Dictionary<IdAndTimestamp, ConversationContext> _conversationContexts;
        protected int _minCacheExpiryInSeconds;

        protected Dictionary<IdAndTimestamp, TwitterUserIdentifier> _twitterUsersWaitingForReply;
        protected Dictionary<IdAndTimestamp, Activity> _pendingRepliesFromBotToTwitterUser;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="minCacheExpiryInSeconds">The minimum cache expiry time in seconds.
        /// If not provided, the default value is used.</param>
        public InMemoryConversationCache(int minCacheExpiryInSeconds = DefaultMinCacheExpiryInSeconds)
        {
            _twitterUsersWaitingForReply = new Dictionary<IdAndTimestamp, TwitterUserIdentifier>();
            _pendingRepliesFromBotToTwitterUser = new Dictionary<IdAndTimestamp, Activity>();
            _converstionToLatestTweet = new Dictionary<IdAndTimestamp, ITweet>();
            _converstionToRootTweet = new Dictionary<IdAndTimestamp, ITweet>();
            _tweetToConversation = new Dictionary<IdAndTimestamp, string>();
            _conversationContexts = new Dictionary<IdAndTimestamp, ConversationContext>();
            _minCacheExpiryInSeconds = minCacheExpiryInSeconds;
        }

        public IList<ActivityForTwitterUserBundle> GetPendingRepliesToTwitterUsers()
        {
            RemoveExpiredData(); // Lazy clean-up

            IList<ActivityForTwitterUserBundle> messageToTwitterUserBundles =
                new List<ActivityForTwitterUserBundle>();

            if (_twitterUsersWaitingForReply.Count > 0)
            {
                foreach (IdAndTimestamp messageIdAndTimestamp
                    in _pendingRepliesFromBotToTwitterUser.Keys)
                {
                    if (_twitterUsersWaitingForReply.ContainsKey(messageIdAndTimestamp))
                    {
                        messageToTwitterUserBundles.Add(
                            new ActivityForTwitterUserBundle(
                                _pendingRepliesFromBotToTwitterUser[messageIdAndTimestamp],
                                _twitterUsersWaitingForReply[messageIdAndTimestamp]));
                    }
                }
            }

            return messageToTwitterUserBundles;
        }

        public TwitterUserIdentifier GetTwitterUserWaitingForReply(IdAndTimestamp messageIdAndTimestamp)
        {
            if (messageIdAndTimestamp != null
                && _twitterUsersWaitingForReply.ContainsKey(messageIdAndTimestamp))
            {
                return _twitterUsersWaitingForReply[messageIdAndTimestamp];
            }

            return null;
        }

        public bool AddTwitterUserWaitingForReply(
            IdAndTimestamp messageIdAndTimestamp, TwitterUserIdentifier twitterUserIdentifier)
        {
            if (messageIdAndTimestamp != null
                && twitterUserIdentifier != null
                && !_twitterUsersWaitingForReply.ContainsKey(messageIdAndTimestamp))
            {
                _twitterUsersWaitingForReply.Add(messageIdAndTimestamp, twitterUserIdentifier);
                return true;
            }

            return false;
        }

        public bool RemoveTwitterUserWaitingForReply(IdAndTimestamp messageIdAndTimestamp)
        {
            return _twitterUsersWaitingForReply.Remove(messageIdAndTimestamp);
        }

        public Activity GetPendingReplyFromBotToTwitterUser(IdAndTimestamp messageIdAndTimestamp)
        {
            if (messageIdAndTimestamp != null
                && _pendingRepliesFromBotToTwitterUser.ContainsKey(messageIdAndTimestamp))
            {
                return _pendingRepliesFromBotToTwitterUser[messageIdAndTimestamp];
            }

            return null;
        }

        public bool AddPendingReplyFromBotToTwitterUser(
            IdAndTimestamp messageIdAndTimestamp, Activity pendingReplyActivity)
        {
            if (messageIdAndTimestamp != null
                && !string.IsNullOrEmpty(messageIdAndTimestamp.Id)
                && pendingReplyActivity != null
                && !_pendingRepliesFromBotToTwitterUser.ContainsKey(messageIdAndTimestamp))
            {
                _pendingRepliesFromBotToTwitterUser.Add(messageIdAndTimestamp, pendingReplyActivity);
                return true;
            }

            return false;
        }

        public bool RemovePendingReplyFromBotToTwitterUser(IdAndTimestamp messageIdAndTimestamp)
        {
            return _pendingRepliesFromBotToTwitterUser.Remove(messageIdAndTimestamp);
        }

        /// <summary>
        /// Clears any records (pending replies and Twitter user identifiers) where the timestamp
        /// (in MessageIdAndTimestamp) is expired.
        /// 
        /// TODO: While the current implementation works, make it nicer e.g. with Linq.
        /// </summary>
        protected virtual void RemoveExpiredData()
        {
            DateTime dateTimeNow = DateTime.Now;
            bool wasRemoved = true;

            while (wasRemoved)
            {
                wasRemoved = false;

                foreach (IdAndTimestamp messageIdAndTimestamp in _pendingRepliesFromBotToTwitterUser.Keys)
                {
                    if (messageIdAndTimestamp.Timestamp.AddSeconds(_minCacheExpiryInSeconds) < dateTimeNow)
                    {
                        _pendingRepliesFromBotToTwitterUser.Remove(messageIdAndTimestamp);
                        wasRemoved = true;
                        break;
                    }
                }

                foreach (IdAndTimestamp messageIdAndTimestamp in _twitterUsersWaitingForReply.Keys)
                {
                    if (messageIdAndTimestamp.Timestamp.AddSeconds(_minCacheExpiryInSeconds) < dateTimeNow)
                    {
                        _twitterUsersWaitingForReply.Remove(messageIdAndTimestamp);
                        wasRemoved = true;
                        break;
                    }
                }
            }
        }

        public bool PutConversation(IdAndTimestamp conversationId, ConversationContext conversationContext)
        {
            if (conversationId != null
                && conversationContext != null)
            {
                _conversationContexts.AddOrUpdate(conversationId, conversationContext);
                return true;
            }

            return false;
        }

        public ConversationContext GetConversation(IdAndTimestamp conversationId)
        {
            if (conversationId != null
                && _conversationContexts.ContainsKey(conversationId))
            {
                return _conversationContexts[conversationId];
            }

            return null;
        }

        public IEnumerable<ConversationContext> GetConversations()
        {
            // XXX 複製にならないかも？
            return _conversationContexts.Values.ToList();
        }

        public bool PutLatestTweetOfConversation(IdAndTimestamp conversationId, ITweet tweet)
        {
            if (conversationId != null
                && tweet != null)
            {
                _converstionToLatestTweet.AddOrUpdate(conversationId, tweet);
                if (!_converstionToRootTweet.ContainsKey(conversationId))
                {
                    _converstionToRootTweet.Add(conversationId, tweet);
                }
                return true;
            }

            return false;
        }

        public ITweet GetLatestTweetOfConversation(IdAndTimestamp conversationId)
        {
            if (conversationId != null
                && _converstionToLatestTweet.ContainsKey(conversationId))
            {
                return _converstionToLatestTweet[conversationId];
            }

            return null;
        }

        public ITweet GetRootTweetOfConversation(IdAndTimestamp conversationId)
        {
            if (conversationId != null
                && _converstionToRootTweet.ContainsKey(conversationId))
            {
                return _converstionToRootTweet[conversationId];
            }

            return null;
        }

        public bool PutConversationOfTweet(IdAndTimestamp tweetId, string conversationId)
        {
            if (tweetId != null
                && conversationId != null)
            {
                _tweetToConversation.AddOrUpdate(tweetId, conversationId);
                return true;
            }

            return false;
        }

        public string GetConversationOfTweet(IdAndTimestamp tweetId)
        {
            if (tweetId != null
                && _tweetToConversation.ContainsKey(tweetId))
            {
                return _tweetToConversation[tweetId];
            }

            return null;
        }
    }
}
