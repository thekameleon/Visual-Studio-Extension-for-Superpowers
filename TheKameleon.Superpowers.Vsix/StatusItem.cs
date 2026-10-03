using System.Runtime.Serialization;

namespace TheKameleon.Superpowers.Vsix
{
    [DataContract]
    internal sealed class StatusItem
    {
        public StatusItem(string level, string title, string message, string symbol, string color)
        {
            this.Level = level;
            this.Title = title;
            this.Message = message;
            this.Symbol = symbol;
            this.Color = color;
        }

        [DataMember]
        public string Symbol { get; }

        [DataMember]
        public string Color { get; }

        [DataMember]
        public string Level { get; }

        [DataMember]
        public string Title { get; }

        [DataMember]
        public string Message { get; }
    }
}
