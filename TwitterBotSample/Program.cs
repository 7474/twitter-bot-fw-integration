using System;
using System.Configuration;
using System.Threading;
using TwitterBotFWIntegration;

namespace TwitterBotSample
{
    class Program
    {
        private static TwitterBotIntegrationManager CreateTwitterBotIntegrationManager()
        {
            string directLineSecret = ConfigurationManager.AppSettings["directLineSecret"];
            string consumerKey = ConfigurationManager.AppSettings["consumerKey"];
            string consumerSecret = ConfigurationManager.AppSettings["consumerSecret"];
            string bearerToken = ConfigurationManager.AppSettings["bearerToken"];
            string accessToken = ConfigurationManager.AppSettings["accessToken"];
            string accessTokenSecret = ConfigurationManager.AppSettings["accessTokenSecret"];

            return new TwitterBotIntegrationManager(
                directLineSecret, consumerKey, consumerSecret, bearerToken, accessToken, accessTokenSecret);

        }
        static void Main()
        {
            using (TwitterBotIntegrationManager twitterBotConnection = CreateTwitterBotIntegrationManager())
            {
                twitterBotConnection.Start();

                while (true)
                {
                    Thread.Sleep(1000);
                }
            }
        }
    }
}
