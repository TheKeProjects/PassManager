using System;
using System.Collections.Generic;

namespace PassManager.Models
{
    public class Account
    {
        public string Type { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public List<PasswordHistory> History { get; set; }

        public Account()
        {
            History = new List<PasswordHistory>();
        }

        public Account(string type, string email, string password)
        {
            Type = type;
            Email = email;
            Password = password;
            History = new List<PasswordHistory>
            {
                new PasswordHistory { Password = password, Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
            };
        }

        public void UpdatePassword(string newPassword)
        {
            Password = newPassword;
            History.Add(new PasswordHistory
            {
                Password = newPassword,
                Date = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss")
            });
        }
    }

    public class PasswordHistory
    {
        public string Password { get; set; }
        public string Date { get; set; }
    }
}
