using Microsoft.VisualStudio.Extensibility.UI;

namespace TheKameleon.Superpowers.Vsix
{
    internal sealed class SuperpowersToolWindowControl : RemoteUserControl
    {
        public SuperpowersToolWindowControl(SuperpowersWorkflowViewModel dataContext)
            : base(dataContext)
        {
        }
    }
}
