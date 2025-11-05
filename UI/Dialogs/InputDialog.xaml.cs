using System.Windows;

namespace PassManager.UI
{
    public partial class InputDialog : Window
    {
        public string ResponseText => InputTextBox.Text;

        public InputDialog(string title, string prompt)
        {
            InitializeComponent();
            Title = title;
            PromptText.Text = prompt;
            InputTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
