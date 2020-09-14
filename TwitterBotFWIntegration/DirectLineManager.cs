using Microsoft.Bot.Connector.DirectLine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitterBotFWIntegration.Cache;
using TwitterBotFWIntegration.Models;

namespace TwitterBotFWIntegration
{
    public class DirectLineManager : IDisposable
    {
        /// <summary>
        /// An event that is fired when new messages are received.
        /// </summary>
        public event EventHandler<IList<Activity>> ActivitiesReceived;

        private const int DefaultPollingIntervalInMilliseconds = 2000;

        private BackgroundWorker _backgroundWorker;
        private SynchronizationContext _synchronizationContext;
        private string _directLineSecret;
        private int _pollingIntervalInMilliseconds;

        private IDirectLineConversationCache _conversationCache;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="directLineSecret">The Direct Line secret associated with the bot.</param>
        public DirectLineManager(string directLineSecret, IDirectLineConversationCache conversationCache)
        {
            if (string.IsNullOrEmpty(directLineSecret))
            {
                throw new ArgumentNullException("Direct Line secret is null or empty");
            }
            if (conversationCache == null)
            {
                throw new ArgumentNullException("conversationCache is null");
            }

            _backgroundWorker = new BackgroundWorker();
            _backgroundWorker.DoWork += new DoWorkEventHandler(RunPollMessagesLoopAsync);
            _backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(BackgroundWorkerDone);

            _directLineSecret = directLineSecret;
            _conversationCache = conversationCache;
        }

        public void Dispose()
        {
            _backgroundWorker.DoWork -= new DoWorkEventHandler(RunPollMessagesLoopAsync);
            _backgroundWorker.RunWorkerCompleted -= new RunWorkerCompletedEventHandler(BackgroundWorkerDone);
            _backgroundWorker.CancelAsync();
            _backgroundWorker.Dispose();
        }

        /// <summary>
        /// Sends the given message to the bot.
        /// </summary>
        /// <param name="conversation"></param>
        /// <param name="messageText">The message to send.</param>
        /// <param name="senderId">The sender ID.</param>
        /// <param name="senderName">The sender name.</param>
        /// <returns>Message ID if successful. Null otherwise.</returns>
        public async Task<DirectLineSendResult> SendMessageAsync(string conversationId, string messageText, string senderId = null, string senderName = null)
        {
            Debug.WriteLine(
                $"Sending DL message from {(string.IsNullOrEmpty(senderName) ? "sender" : senderName)}, ID '{senderId}'");

            Activity activityToSend = new Activity
            {
                From = new ChannelAccount(senderId, senderName),
                Type = ActivityTypes.Message,
                Text = messageText
            };

            var res = await PostActivityAsync(conversationId, activityToSend);

            // TODO Polling見直しからStreamへ段階的に移行していく
            if (res != null)
            {
                StartPolling();
            }

            return res;
        }

        /// <summary>
        /// Polls for new messages (activities).
        /// </summary>
        /// <param name="conversationId">The ID of the conversation.</param>
        /// <returns></returns>
        public async Task PollMessagesAsync(string conversationId)
        {
            if (string.IsNullOrEmpty(conversationId))
            {
                return;
            }

            ActivitySet activitySet = null;

            using (DirectLineClient directLineClient = new DirectLineClient(_directLineSecret))
            {
                var watermark = _conversationCache.GetConversation(new IdAndTimestamp(conversationId))?.ActivityWaterMark;
                var conversation = directLineClient.Conversations.ReconnectToConversation(conversationId);
                activitySet = await directLineClient.Conversations.GetActivitiesAsync(conversationId, watermark);
                // XXX WarterMarkこの段階で処理してしまうのはちょっと嫌。
                _conversationCache.PutConversation(
                    new IdAndTimestamp(conversationId),
                    new ConversationContext(conversation, activitySet?.Watermark));
            }

            if (activitySet != null)
            {
                Debug.WriteLine($"conversationId {conversationId} {activitySet.Activities?.Count} activity/activities received");
                if (activitySet.Activities?.Count > 0)
                {
                    Debug.WriteLine(JsonConvert.SerializeObject(activitySet.Activities));
                }

                // ボットへ送る方向のアクティビティは処理しない。
                // TODO このDirectLineClientではその方向のアクティビティに ReplyToId を指定していないためそれでフィルタしているが、恐らく正しいフィルタではない。
                var activities = activitySet.Activities
                    .Where(x => !string.IsNullOrEmpty(x.ReplyToId))
                    .ToList();

                if (_synchronizationContext != null)
                {
                    _synchronizationContext.Post((o) => ActivitiesReceived?.Invoke(this, activities), null);
                }
                else
                {
                    ActivitiesReceived?.Invoke(this, activities);
                }
            }
        }

        /// <summary>
        /// Starts polling for the messages.
        /// </summary>
        /// <param name="pollingIntervalInMilliseconds">The polling interval in milliseconds.</param>
        /// <returns>True, if polling was started. False otherwise (e.g. if already running).</returns>
        public bool StartPolling(int pollingIntervalInMilliseconds = DefaultPollingIntervalInMilliseconds)
        {
            if (_backgroundWorker.IsBusy)
            {
                Debug.WriteLine("Already polling");
                return false;
            }

            _synchronizationContext = SynchronizationContext.Current;
            _pollingIntervalInMilliseconds = pollingIntervalInMilliseconds;
            _backgroundWorker.RunWorkerAsync();
            return true;
        }

        /// <summary>
        /// Stops polling for the messages.
        /// </summary>
        public void StopPolling()
        {
            try
            {
                _backgroundWorker.CancelAsync();
            }
            catch (InvalidOperationException e)
            {
                Debug.WriteLine($"Failed to stop polling: {e.Message}");
            }
        }

        /// <summary>
        /// Posts the given activity to the bot using Direct Line client.
        /// </summary>
        /// <param name="activity">The activity to send.</param>
        /// <returns>The resoure response.</returns>
        private async Task<DirectLineSendResult> PostActivityAsync(string conversationId, Activity activity)
        {
            ResourceResponse resourceResponse = null;
            var conversationContext = string.IsNullOrEmpty(conversationId)
                ? null
                : _conversationCache.GetConversation(new IdAndTimestamp(conversationId));
            var conversation = conversationContext?.Conversation;

            using (DirectLineClient directLineClient = new DirectLineClient(_directLineSecret))
            {
                // TODO conversation.ExpiresIn
                if (conversation == null)
                {
                    conversation = directLineClient.Conversations.StartConversation();
                    _conversationCache.PutConversation(
                        new IdAndTimestamp(conversation.ConversationId),
                        new ConversationContext(conversation, ""));
                }
                else
                {
                    directLineClient.Conversations.ReconnectToConversation(conversation.ConversationId);
                }

                resourceResponse = await directLineClient.Conversations.PostActivityAsync(conversation.ConversationId, activity);
            }

            return new DirectLineSendResult(conversation, resourceResponse.Id);
        }

        private async void RunPollMessagesLoopAsync(object sender, DoWorkEventArgs e)
        {
            while (!e.Cancel)
            {
                foreach (var conversation in _conversationCache.GetConversations())
                {
                    await PollMessagesAsync(conversation?.Conversation?.ConversationId);
                    Thread.Sleep(_pollingIntervalInMilliseconds);
                }
            }
        }

        private void BackgroundWorkerDone(object sender, RunWorkerCompletedEventArgs e)
        {
            Debug.WriteLine("Background worker finished");
        }
    }
}
