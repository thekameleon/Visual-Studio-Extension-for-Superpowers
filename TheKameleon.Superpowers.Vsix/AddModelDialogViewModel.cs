using System.Collections.Generic;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.Extensibility.UI;

namespace TheKameleon.Superpowers.Vsix
{
    [DataContract]
    internal sealed class AddModelDialogViewModel : NotifyPropertyChangedObject
    {
        private string selectedModel = string.Empty;

        public AddModelDialogViewModel(string stepLabel, IEnumerable<string> modelOptions)
        {
            this.Prompt = $"Choose a Copilot CLI model to suggest for sub-agents during \"{stepLabel}\". You can also type a model name.";
            this.ModelOptions.AddRange(modelOptions);
        }

        [DataMember]
        public string Prompt { get; }

        [DataMember]
        public ObservableList<string> ModelOptions { get; } = new();

        [DataMember]
        public string SelectedModel
        {
            get => this.selectedModel;
            set => this.SetProperty(ref this.selectedModel, value);
        }
    }
}
