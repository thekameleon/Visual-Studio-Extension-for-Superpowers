using Microsoft.VisualStudio.Extensibility.UI;

namespace TheKameleon.Superpowers.Vsix
{
    internal sealed class AddModelDialogControl : RemoteUserControl
    {
        public AddModelDialogControl(AddModelDialogViewModel dataContext)
            : base(dataContext)
        {
        }
    }
}
