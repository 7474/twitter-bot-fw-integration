using Microsoft.Bot.Connector.DirectLine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Tweetinvi.Models;
using TwitterBotFWIntegration.Cache;
using TwitterBotFWIntegration.Models;

namespace TwitterBotFWIntegration
{
    /// <summary>
    /// The main class and API of the library.
    /// </summary>
    public class TwitterBotIntegrationManager : IDisposable
    {
        protected DirectLineManager _directLineManager;
        protected TwitterManager _twitterManager;
        protected ITweetConversationCache _conversationCache;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="directLineSecret">The Direct Line secret associated with the bot.</param>
        /// <param name="consumerKey">The Twitter consumer key.</param>
        /// <param name="consumerSecret">The Twitter consumer secret.</param>
        /// <param name="bearerToken"></param>
        /// <param name="accessToken">The Twitter app access token.</param>
        /// <param name="accessTokenSecret">The Twitter app secret.</param>
        /// <param name="conversationCache">A message and user ID cache implementation.</param>
        public TwitterBotIntegrationManager(
            string directLineSecret,
            string consumerKey, string consumerSecret,
            string bearerToken = null,
            string accessToken = null, string accessTokenSecret = null,
            IDirectLineConversationCache directLineConversationCache = null,
            ITweetConversationCache tweetConversationCache = null)
        {
            _directLineManager = new DirectLineManager(directLineSecret, directLineConversationCache ?? new InMemoryConversationCache());
            _twitterManager = new TwitterManager(consumerKey, consumerSecret, bearerToken, accessToken, accessTokenSecret);
            _conversationCache = tweetConversationCache ?? new InMemoryConversationCache();

            _directLineManager.ActivitiesReceived += OnActivitiesReceived;
            _twitterManager.TweetReceived += OnTweetReceivedAsync;
        }

        /// <summary>
        /// Starts the manager (starts listening for incoming tweets).
        /// </summary>
        public void Start()
        {
            _twitterManager.StartStream();
        }

        public void Dispose()
        {
            _directLineManager.ActivitiesReceived -= OnActivitiesReceived;
            _twitterManager.TweetReceived -= OnTweetReceivedAsync;

            _directLineManager.Dispose();
            _twitterManager.Dispose();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        protected virtual void ReplyTweetInTwitter(Activity activity, ITweet tweet)
        {
            if (activity == null || tweet == null)
            {
                throw new ArgumentNullException("Either the activity or the Twitter user identifier is null");
            }

            string conversationId = activity.Conversation.Id;
            var rootTweet = _conversationCache.GetRootTweetOfConversation(new IdAndTimestamp(conversationId));

            Debug.WriteLine(
                $"Replying to user '{tweet.CreatedBy.ScreenName}' using message in activity with conversation ID '{conversationId}'");

            var replyTweet = _twitterManager.SendReply(
                activity.Text, tweet.Id,
                tweet.CreatedBy.ScreenName, tweet.InReplyToScreenName, rootTweet?.CreatedBy.ScreenName);

            // 続きを呟くためのに入れておく
            _conversationCache.PutLatestTweetOfConversation(new IdAndTimestamp(conversationId), replyTweet);
            // XXX 転置は勝手に作ってもいいかもしれない
            _conversationCache.PutConversationOfTweet(new IdAndTimestamp(replyTweet.IdStr), conversationId);
        }

        /// <summary>
        /// Checks the list of received activities for message IDs matching the previously sent
        /// Direct Line messages. If we have a match, we know it's the bot's reply to the Twitter user.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="activities">A list of activities sent by the bot.</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        protected virtual void OnActivitiesReceived(object sender, IList<Activity> activities)
        {
            foreach (Activity activity in activities)
            {
                string conversationId = activity.Conversation.Id;

                var tweet = _conversationCache.GetLatestTweetOfConversation(new IdAndTimestamp(conversationId));
                if (tweet != null)
                {
                    ReplyTweetInTwitter(activity, tweet);
                }
                else
                {
                    // XXX Userじゃない単にペンディングなだけ
                    // TODO なんか処理する、とは言えどうにもならん気がする
                    //_conversationCache.AddPendingReplyFromBotToTwitterUser(messageIdAndTimestamp, activity);
                    Debug.WriteLine($"Stored activity with conversation ID '{conversationId}'");
                }
            }
        }

        /// <summary>
        /// Sends the message in the received tweet to the bot via Direct Line.
        /// If we get a valid response indicating that the message was received by the bot,
        /// we will store the Twitter user identifiers (to be able to reply back).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="messageEventArgs">Contains the Twitter message details.</param>
        protected virtual async void OnTweetReceivedAsync(object sender, Tweetinvi.Events.MatchedTweetReceivedEventArgs messageEventArgs)
        {
            var tweet = messageEventArgs.Tweet;
            var conversationId = string.IsNullOrEmpty(tweet.InReplyToStatusIdStr)
                ? null
                : _conversationCache.GetConversationOfTweet(new IdAndTimestamp(tweet.InReplyToStatusIdStr));
            var sendResult = await _directLineManager.SendMessageAsync(
                 conversationId,
                 tweet.Text,
                 tweet.CreatedBy.UserIdentifier.IdStr,
                 tweet.CreatedBy.UserIdentifier.ScreenName);

            if (sendResult == null)
            {
                Debug.WriteLine(
                    $"Failed to send the message from user '{tweet.CreatedBy.UserIdentifier.ScreenName}' to the bot - message text was '{tweet.Text}'");
            }
            else
            {
                Debug.WriteLine(
                    $"Message from user '{tweet.CreatedBy.UserIdentifier.ScreenName}' successfully sent to the bot - message ID is '{sendResult.MessageId}'");

                _conversationCache.PutLatestTweetOfConversation(new IdAndTimestamp(sendResult.Conversation.ConversationId), tweet);

                _directLineManager.StartPolling();
            }
        }
    }
}
