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

        private IUser _botUser;
        private Tweetinvi.Streaming.IFilteredStream _filteredStream;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="consumerKey">The Twitter consumer key.</param>
        /// <param name="consumerSecret">The Twitter consumer secret.</param>
        /// <param name="bearerToken"></param>
        /// <param name="accessToken">The Twitter app access token.</param>
        /// <param name="accessTokenSecret">The Twitter app secret.</param>
        public TwitterManager(
            string consumerKey,
            string consumerSecret,
            string bearerToken = null,
            string accessToken = null,
            string accessTokenSecret = null)
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
            _botUser = Tweetinvi.User.GetAuthenticatedUser();
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
                _filteredStream.AddTrack("@" + _botUser.ScreenName);
                _filteredStream.StreamStarted += OnStreamStarted;
                _filteredStream.MatchingTweetReceived += OnMatchingTweetReceived;
                // TODO これに限らず例外処理と継続は見ておく
                _filteredStream.StartStreamMatchingAllConditionsAsync();
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
            if (e.Tweet.CreatedBy.Id == _botUser.Id)
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
                    .Where(x => x != _botUser.ScreenName)
                    .Distinct()
                    .Select(x => "@" + x));

            // TODO メッセージをURLなどを考慮した長さに正規化する
            // TODO 添付やカードを展開する
            // https://github.com/linvi/tweetinvi/issues/53
            return Tweetinvi.Tweet.PublishTweetInReplyTo(
                $"{atNames} {messageText}".SafeSubstring(0, 140),
                replyTo);
        }
    }
}
