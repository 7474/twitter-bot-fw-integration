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
        protected IConversationCache _conversationCache;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="directLineSecret">The Direct Line secret associated with the bot.</param>
        /// <param name="consumerKey">The Twitter consumer key.</param>
        /// <param name="consumerSecret">The Twitter consumer secret.</param>
        /// <param name="accessToken">The Twitter app access token.</param>
        /// <param name="accessTokenSecret">The Twitter app secret.</param>
        /// <param name="conversationCache">A message and user ID cache implementation.</param>
        public TwitterBotIntegrationManager(
            string directLineSecret,
            string consumerKey, string consumerSecret,
            string bearerToken = null,
            string accessToken = null, string accessTokenSecret = null,
            IConversationCache conversationCache = null)
        {
            _directLineManager = new DirectLineManager(directLineSecret);
            _twitterManager = new TwitterManager(consumerKey, consumerSecret, bearerToken, accessToken, accessTokenSecret);
            _conversationCache = conversationCache ?? new InMemoryConversationCache();

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

        ///// <summary>
        ///// Sends the message in the given activity to the user in Twitter with the given identity.
        ///// Both the activity and the Twitter user identifier are removed from the collections.
        ///// </summary>
        ///// <param name="activity">The activity containing the message.</param>
        ///// <param name="twitterUserIdentifier">The IDs of the Twitter user to reply to.</param>
        //[MethodImpl(MethodImplOptions.Synchronized)]
        //protected virtual void ReplyInTwitter(Activity activity, TwitterUserIdentifier twitterUserIdentifier)
        //{
        //    if (activity == null || twitterUserIdentifier == null)
        //    {
        //        throw new ArgumentNullException("Either the activity or the Twitter user identifier is null");
        //    }

        //    string messageId = activity.ReplyToId;

        //    if (string.IsNullOrEmpty(messageId))
        //    {
        //        throw new ArgumentNullException("The activity is missing the 'reply to ID'");
        //    }

        //    Debug.WriteLine(
        //        $"Replying to user '{twitterUserIdentifier.ScreenName}' using message in activity with message ID '{messageId}'");

        //    _twitterManager.SendMessage(activity.Text, twitterUserIdentifier.TwitterUserId, twitterUserIdentifier.ScreenName);
        //}

        [MethodImpl(MethodImplOptions.Synchronized)]
        protected virtual void ReplyTweetInTwitter(Activity activity, ITweet tweet)
        {
            if (activity == null || tweet == null)
            {
                throw new ArgumentNullException("Either the activity or the Twitter user identifier is null");
            }

            string conversationId = activity.Conversation.Id;

            if (string.IsNullOrEmpty(conversationId))
            {
                throw new ArgumentNullException("The activity is missing the 'Conversation'");
            }

            Debug.WriteLine(
                $"Replying to user '{tweet.CreatedBy.ScreenName}' using message in activity with conversation ID '{conversationId}'");

            var replyTweet = _twitterManager.SendReply(
                activity.Text, tweet.Id,
                tweet.CreatedBy.ScreenName, tweet.InReplyToScreenName);

            // 続きを呟くためのに入れておく
            _conversationCache.PutLatestTweetOfConversation(new IdAndTimestamp(conversationId), replyTweet);
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
                // TODO Botとのやり取りはConversationでまとめたい。
                string messageId = activity.ReplyToId;
                string conversationId = activity.Conversation.Id;

                if (!string.IsNullOrEmpty(messageId))
                {
                    var messageIdAndTimestamp = new IdAndTimestamp(messageId);
                    var tweet = _conversationCache.GetLatestTweetOfConversation(new IdAndTimestamp(conversationId));
                    if (tweet == null)
                    {
                        tweet = _conversationCache.GetLatestTweetOfConversation(messageIdAndTimestamp);
                    }
                    if (tweet != null)
                    {
                        ReplyTweetInTwitter(activity, tweet);
                    }
                    else
                    {
                        // XXX Userじゃない単にペンディングなだけ
                        _conversationCache.AddPendingReplyFromBotToTwitterUser(messageIdAndTimestamp, activity);
                        Debug.WriteLine($"Stored activity with message ID '{messageId}'");
                    }
                }

                //string messageId = activity.ReplyToId;
                //TwitterUserIdentifier twitterUserIdentifier =
                //    _conversationCache.GetTwitterUserWaitingForReply(messageIdAndTimestamp);

                //if (twitterUserIdentifier != null)
                //{
                //    ReplyInTwitter(activity, twitterUserIdentifier);
                //}
                //else
                //{
                //    // Looks like we received the reply activity before we got back
                //    // the response from sending the original message to the bot
                //    _conversationCache.AddPendingReplyFromBotToTwitterUser(messageIdAndTimestamp, activity);

                //    Debug.WriteLine($"Stored activity with message ID '{messageId}'");
                //}
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
            string messageId = await _directLineManager.SendMessageAsync(
                messageEventArgs.Tweet.Text,
                messageEventArgs.Tweet.CreatedBy.UserIdentifier.IdStr,
                messageEventArgs.Tweet.CreatedBy.UserIdentifier.ScreenName);

            if (string.IsNullOrEmpty(messageId))
            {
                Debug.WriteLine(
                    $"Failed to send the message from user '{messageEventArgs.Tweet.CreatedBy.UserIdentifier.ScreenName}' to the bot - message text was '{messageEventArgs.Tweet.Text}'");
            }
            else
            {
                Debug.WriteLine(
                    $"Message from user '{messageEventArgs.Tweet.CreatedBy.UserIdentifier.ScreenName}' successfully sent to the bot - message ID is '{messageId}'");

                IdAndTimestamp messageIdAndTimestamp = new IdAndTimestamp(messageId);

                // Store the Twitter user details so that we know who to reply to
                // XXX DM時にメッセージを入れる
                //TwitterUserIdentifier twitterUserIdentifier = new TwitterUserIdentifier()
                //{
                //    TwitterUserId = messageEventArgs.Tweet.CreatedBy.UserIdentifier.Id,
                //    ScreenName = messageEventArgs.Tweet.CreatedBy.UserIdentifier.ScreenName
                //};
                //_conversationCache.AddTwitterUserWaitingForReply(messageIdAndTimestamp, twitterUserIdentifier);
                _conversationCache.PutLatestTweetOfConversation(messageIdAndTimestamp, messageEventArgs.Tweet);

                _directLineManager.StartPolling();
            }

            //// Check for pending activities
            //foreach (ActivityForTwitterUserBundle pendingMessage
            //    in _conversationCache.GetPendingRepliesToTwitterUsers())
            //{
            //    ReplyInTwitter(
            //        pendingMessage.ActivityForTwitterUser,
            //        pendingMessage.TwitterUserIdentifier);
            //}
        }
    }
}
