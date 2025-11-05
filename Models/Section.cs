using System.Collections.Generic;

namespace PassManager.Models
{
    public class Section
    {
        public string Name { get; set; }
        public List<Account> Accounts { get; set; }

        public Section()
        {
            Accounts = new List<Account>();
        }

        public Section(string name)
        {
            Name = name;
            Accounts = new List<Account>();
        }

        public void AddAccount(Account account)
        {
            Accounts.Add(account);
        }

        public void RemoveAccount(Account account)
        {
            Accounts.Remove(account);
        }
    }
}
