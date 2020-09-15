using System;

namespace TwitterBotFWIntegration.Models
{
    /// <summary>
    /// Contains a Direct Line message ID (used to match a sent message to the bot with
    /// an incoming reply from the bot) and a timestamp.
    /// 
    /// Note that all comparisons are done using the message ID only!
    /// 
    /// XXX サマリは古い
    /// </summary>
    public class IdAndTimestamp : IEquatable<IdAndTimestamp>
    {
        public IdAndTimestamp(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException("Message ID cannot be null or empty");
            }

            Id = id;
            Timestamp = DateTime.Now;
        }

        public string Id
        {
            get;
            private set;
        }

        public DateTime Timestamp
        {
            get;
            private set;
        }

        public bool Equals(IdAndTimestamp other)
        {
            return (other.Id.Equals(Id));
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}
