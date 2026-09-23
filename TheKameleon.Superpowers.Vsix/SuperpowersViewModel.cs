using System.Runtime.Serialization;
using Microsoft.VisualStudio.Extensibility.UI;

namespace TheKameleon.Superpowers.Vsix
{
    [DataContract]
    internal sealed class SuperpowersViewModel : NotifyPropertyChangedObject
    {
        private string statusText = "Superpowers is being rebuilt as a skills installer.";

        [DataMember]
        public string StatusText
        {
            get => this.statusText;
            set => this.SetProperty(ref this.statusText, value);
        }
    }
}
