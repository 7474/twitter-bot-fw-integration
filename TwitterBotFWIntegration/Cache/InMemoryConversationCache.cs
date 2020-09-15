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
        protected const int DefaultMinCacheExpiryInSeconds = 300;
        protected Dictionary<IdAndTimestamp, ITweet> _converstionToLatestTweet;
        protected Dictionary<IdAndTimestamp, ITweet> _converstionToRootTweet;
        protected Dictionary<IdAndTimestamp, string> _tweetToConversation;
        protected Dictionary<IdAndTimestamp, ConversationContext> _conversationContexts;
        protected int _minCacheExpiryInSeconds;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="minCacheExpiryInSeconds">The minimum cache expiry time in seconds.
        /// If not provided, the default value is used.</param>
        public InMemoryConversationCache(int minCacheExpiryInSeconds = DefaultMinCacheExpiryInSeconds)
        {
            _converstionToLatestTweet = new Dictionary<IdAndTimestamp, ITweet>();
            _converstionToRootTweet = new Dictionary<IdAndTimestamp, ITweet>();
            _tweetToConversation = new Dictionary<IdAndTimestamp, string>();
            _conversationContexts = new Dictionary<IdAndTimestamp, ConversationContext>();
            _minCacheExpiryInSeconds = minCacheExpiryInSeconds;
        }

        protected virtual void RemoveExpiredData()
        {
            RemoveExpiredDataFromDict(_converstionToLatestTweet);
            RemoveExpiredDataFromDict(_converstionToRootTweet);
            RemoveExpiredDataFromDict(_tweetToConversation);
            RemoveExpiredDataFromDict(_conversationContexts);
        }

        private void RemoveExpiredDataFromDict<T>(IDictionary<IdAndTimestamp, T> dict)
        {
            DateTime dateTimeNow = DateTime.Now;

            foreach (var id in dict.Keys.ToList().Where(x => x.Timestamp.AddSeconds(_minCacheExpiryInSeconds) < dateTimeNow))
            {
                dict.Remove(id);
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
            // XXX ここで破棄でいいのか？
            RemoveExpiredData();

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
