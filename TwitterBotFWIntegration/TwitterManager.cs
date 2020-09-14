using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Tweetinvi.Models;
using TwitterBotFWIntegration.Extensions;

namespace TwitterBotFWIntegration
{
    public class TwitterManager : IDisposable
    {
        /// <summary>
        /// True, if the Twitter stream is ready (started). False otherwise.
        /// </summary>
        public bool IsReady
        {
            get;
            private set;
        }

        /// <summary>
        /// Fired when a @ tweet is received.
        /// </summary>
        public event EventHandler<Tweetinvi.Events.MatchedTweetReceivedEventArgs> TweetReceived;

        private Tweetinvi.Streaming.IFilteredStream _filteredStream;

        // TODO to configurable
        private long _botUserId = 1305161053978779650;
        private string _botScreenName = "RealDiceBot";

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="consumerKey">The Twitter consumer key.</param>
        /// <param name="consumerSecret">The Twitter consumer secret.</param>
        /// <param name="accessToken">The Twitter app access token.</param>
        /// <param name="accessTokenSecret">The Twitter app secret.</param>
        public TwitterManager(string consumerKey, string consumerSecret, string bearerToken = null, string accessToken = null, string accessTokenSecret = null)
        {
            if (string.IsNullOrEmpty(consumerKey) || string.IsNullOrEmpty(consumerSecret))
            {
                throw new ArgumentNullException("Both consumer key and secret must be valid");
            }
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(accessTokenSecret))
            {
                if (string.IsNullOrEmpty(bearerToken))
                {
                    Tweetinvi.Auth.SetApplicationOnlyCredentials(consumerKey, consumerSecret);
                }
                else
                {
                    Tweetinvi.Auth.SetApplicationOnlyCredentials(consumerKey, consumerSecret, bearerToken);
                }
            }
            else
            {
                Tweetinvi.Auth.SetUserCredentials(consumerKey, consumerSecret, accessToken, accessTokenSecret);
            }
        }

        public void Dispose()
        {
            if (_filteredStream != null)
            {
                _filteredStream.StreamStarted -= OnStreamStarted;
                _filteredStream.MatchingTweetReceived -= OnMatchingTweetReceived;
                _filteredStream.StopStream();
                _filteredStream = null;
            }
        }

        public void StartStream()
        {
            if (_filteredStream == null)
            {
                // For Reply
                _filteredStream = Tweetinvi.Stream.CreateFilteredStream();
                _filteredStream.AddTrack("@" + _botScreenName);
                _filteredStream.StreamStarted += OnStreamStarted;
                _filteredStream.MatchingTweetReceived += OnMatchingTweetReceived;
                _filteredStream.StartStreamMatchingAllConditions();
            }
            else
            {
                Debug.WriteLine("Twitter stream already started");
            }
        }

        private void OnStreamStarted(object sender, EventArgs e)
        {
            Debug.WriteLine("Twitter stream started");
            IsReady = true;
        }

        private void OnMatchingTweetReceived(object sender, Tweetinvi.Events.MatchedTweetReceivedEventArgs e)
        {
            Debug.WriteLine("OnMatchingTweetReceived Twitter message received");
            Debug.WriteLine(JsonConvert.SerializeObject(e));
            if (e.Tweet.CreatedBy.Id == _botUserId)
            {
                Debug.WriteLine("Skip. Tweet is created by bot.");
                return;
            }
            TweetReceived?.Invoke(this, e);
        }

        public ITweet SendReply(string messageText, long replyToId, params string[] toScreanNames)
        {
            var replyTo = new TweetIdentifier(replyToId);
            var atNames = string.Join(" ",
                toScreanNames
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Where(x => x != _botScreenName)
                    .Distinct()
                    .Select(x => "@" + x));

            // TODO メッセージをURLなどを考慮した長さに正規化する
            // https://github.com/linvi/tweetinvi/issues/53
            return Tweetinvi.Tweet.PublishTweetInReplyTo(
                $"{atNames} {messageText}".SafeSubstring(0, 140),
                replyTo);
        }

        #region Ref For UserStream...
        ///// <summary>
        ///// Sends the given message (text) to the user matching the given IDs.
        ///// </summary>
        ///// <param name="messageText">The message to send.</param>
        ///// <param name="recipientId">The Twitter recipient ID.</param>
        ///// <param name="recipientScreenName">The Twitter recipient screen name.</param>
        //public void SendMessage(string messageText, long recipientId = 0, string recipientScreenName = null)
        //{
        //    Debug.WriteLine(
        //        $"Sending message to {(string.IsNullOrEmpty(recipientScreenName) ? "user" : recipientScreenName)} with ID '{recipientId.ToString()}'");

        //    Tweetinvi.Models.IUserIdentifier userIdentifier = new Tweetinvi.Models.UserIdentifier(recipientId)
        //    {
        //        ScreenName = recipientScreenName
        //    };

        //    Tweetinvi.Message.PublishMessage(messageText, userIdentifier.Id);
        //}

        //private void OnMessageSent(object sender, Tweetinvi.Events.MessageEventArgs e)
        //{
        //    Debug.WriteLine("Twitter message sent");
        //}

        //private void OnMessageReceived(object sender, Tweetinvi.Events.MessageEventArgs e)
        //{
        //    Debug.WriteLine("Twitter message received");
        //    TweetReceived?.Invoke(this, e);
        //}

        //private void OnTweetFavouritedByMe(object sender, Tweetinvi.Events.TweetFavouritedEventArgs e)
        //{
        //    Debug.WriteLine("Tweet favourited by 'me'");
        //}
        #endregion
    }
}
