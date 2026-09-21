using System;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.Extensibility.UI;

namespace TheKameleon.Superpowers.Vsix
{
    [DataContract]
    internal sealed class ProbeResultsViewModel : NotifyPropertyChangedObject
    {
        private string probeName = "No probe run";
        private string timestamp = "Not available";
        private string status = "Use a probe command from the Superpowers menu.";
        private string details = $"{BuildIdentity.Describe()}{Environment.NewLine}{Environment.NewLine}No capability evidence has been collected in this session.";
        private string notes = "Probe output is temporary and is not persisted.";

        [DataMember]
        public string ProbeName
        {
            get => this.probeName;
            private set => this.SetProperty(ref this.probeName, value);
        }

        [DataMember]
        public string Timestamp
        {
            get => this.timestamp;
            private set => this.SetProperty(ref this.timestamp, value);
        }

        [DataMember]
        public string Status
        {
            get => this.status;
            private set => this.SetProperty(ref this.status, value);
        }

        [DataMember]
        public string Details
        {
            get => this.details;
            private set => this.SetProperty(ref this.details, value);
        }

        [DataMember]
        public string Notes
        {
            get => this.notes;
            private set => this.SetProperty(ref this.notes, value);
        }

        public void Update(string probeName, string status, string details, string notes)
        {
            this.ProbeName = probeName;
            this.Timestamp = DateTimeOffset.UtcNow.ToString("O");
            this.Status = status;
            this.Details = $"{BuildIdentity.Describe()}{Environment.NewLine}{Environment.NewLine}{details}";
            this.Notes = notes;
        }
    }
}
