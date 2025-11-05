using System.Windows;
using PassManager.Models;
using PassManager.Security;

namespace PassManager.UI
{
    public partial class AccountDialog : Window
    {
        public string AccountType => TypeTextBox.Text;
        public string Email => EmailTextBox.Text;
        public string Password => PasswordTextBox.Text;

        private Account _editingAccount;

        public AccountDialog()
        {
            InitializeComponent();
            Title = "Add Account";
            TypeTextBox.Focus();
        }

        public AccountDialog(Account account)
        {
            InitializeComponent();
            Title = "Edit Account";
            _editingAccount = account;

            TypeTextBox.Text = account.Type;
            EmailTextBox.Text = account.Email;
            PasswordTextBox.Text = account.Password;

            // Show history
            if (account.History != null && account.History.Count > 1)
            {
                HistoryPanel.Visibility = Visibility.Visible;
                HistoryList.ItemsSource = account.History;
            }

            TypeTextBox.Focus();
            TypeTextBox.SelectAll();
        }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            PasswordTextBox.Text = PasswordHasher.GeneratePassword(16);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TypeTextBox.Text))
            {
                MessageBox.Show("Please enter account type", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(EmailTextBox.Text))
            {
                MessageBox.Show("Please enter email/username", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(PasswordTextBox.Text))
            {
                MessageBox.Show("Please enter password", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }
    }
}
