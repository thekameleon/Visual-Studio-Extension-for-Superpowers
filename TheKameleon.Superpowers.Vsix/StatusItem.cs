using System.Runtime.Serialization;

namespace TheKameleon.Superpowers.Vsix
{
    [DataContract]
    internal sealed class StatusItem
    {
        public StatusItem(string level, string title, string message)
        {
            this.Level = level;
            this.Title = title;
            this.Message = message;
        }

        [DataMember]
        public string Level { get; }

        [DataMember]
        public string Title { get; }

        [DataMember]
        public string Message { get; }
    }
}
