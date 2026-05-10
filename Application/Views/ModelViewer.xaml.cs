using System.ComponentModel;
using System.Windows.Controls;

namespace ToolKitV.Views
{
    public partial class ModelViewer : UserControl
    {
        public ModelViewer()
        {
            InitializeComponent();
            ToggleViewportBtn.IsChecked = true; // Default to ON for the prototype demo
        }

        private void ToggleViewportBtn_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsChecked")
            {
                Viewport3D.SetActive(ToggleViewportBtn.IsChecked);
            }
        }
    }
}
