using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using PassManager.Models;
using PassManager.Security;

namespace PassManager
{
    /// <summary>
    /// Manages data storage, encryption, and persistence
    /// </summary>
    public class DataManager
    {
        private static DataManager _instance;
        public static DataManager Instance => _instance ??= new DataManager();

        private string _appDataPath;
        private string _masterKeyPath;
        private string _secretKeyPath;
        private string _passwordsPath;
        private string _settingsPath;
        private string _versionPath;

        private CryptoManager _cryptoManager;
        private bool _isInitialized;

        public List<Section> Sections { get; private set; }
        public Settings Settings { get; private set; }

        private DataManager()
        {
            InitializePaths();
            Sections = new List<Section>();
            Settings = new Settings();
        }

        private void InitializePaths()
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _appDataPath = Path.Combine(documentsPath, "PassManager", "AppData");

            _masterKeyPath = Path.Combine(_appDataPath, "master.key");
            _secretKeyPath = Path.Combine(_appDataPath, "secret.key");
            _passwordsPath = Path.Combine(_appDataPath, "passwords.enc");
            _settingsPath = Path.Combine(_appDataPath, "settings.json");
            _versionPath = Path.Combine(_appDataPath, "version.txt");

            // Create directory if it doesn't exist
            if (!Directory.Exists(_appDataPath))
            {
                Directory.CreateDirectory(_appDataPath);
            }
        }

        /// <summary>
        /// Checks if master password exists
        /// </summary>
        public bool MasterPasswordExists()
        {
            return File.Exists(_masterKeyPath);
        }

        /// <summary>
        /// Sets up a new master password
        /// </summary>
        public void SetupMasterPassword(string password)
        {
            if (!PasswordHasher.IsPasswordValid(password))
            {
                throw new ArgumentException("Password must be at least 8 characters and contain letters and digits");
            }

            string hashedPassword = PasswordHasher.HashPassword(password);
            File.WriteAllText(_masterKeyPath, hashedPassword, Encoding.UTF8);

            // Generate encryption key
            byte[] encryptionKey = CryptoManager.GenerateKey();
            File.WriteAllBytes(_secretKeyPath, encryptionKey);

            // Initialize crypto manager
            _cryptoManager = new CryptoManager(encryptionKey);
            _isInitialized = true;

            // Save initial empty data
            SavePasswords();
            SaveSettings();

            // Save version
            File.WriteAllText(_versionPath, "1.4.12", Encoding.UTF8);
        }

        /// <summary>
        /// Verifies master password and initializes encryption
        /// </summary>
        public bool VerifyMasterPassword(string password)
        {
            if (!File.Exists(_masterKeyPath))
                return false;

            string storedHash = File.ReadAllText(_masterKeyPath, Encoding.UTF8);
            bool isValid = PasswordHasher.VerifyPassword(password, storedHash);

            if (isValid)
            {
                // Load encryption key
                byte[] encryptionKey = File.ReadAllBytes(_secretKeyPath);
                _cryptoManager = new CryptoManager(encryptionKey);
                _isInitialized = true;

                // Load data
                LoadPasswords();
                LoadSettings();
            }

            return isValid;
        }

        /// <summary>
        /// Loads passwords from encrypted file
        /// </summary>
        private void LoadPasswords()
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Data manager not initialized");

            if (!File.Exists(_passwordsPath))
            {
                Sections = new List<Section>();
                return;
            }

            try
            {
                byte[] encryptedData = File.ReadAllBytes(_passwordsPath);
                string decryptedJson = _cryptoManager.Decrypt(encryptedData);
                Sections = JsonConvert.DeserializeObject<List<Section>>(decryptedJson) ?? new List<Section>();
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to load passwords: " + ex.Message);
            }
        }

        /// <summary>
        /// Saves passwords to encrypted file
        /// </summary>
        public void SavePasswords()
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Data manager not initialized");

            try
            {
                string json = JsonConvert.SerializeObject(Sections, Formatting.Indented);
                byte[] encryptedData = _cryptoManager.Encrypt(json);
                File.WriteAllBytes(_passwordsPath, encryptedData);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to save passwords: " + ex.Message);
            }
        }

        /// <summary>
        /// Loads settings from JSON file
        /// </summary>
        private void LoadSettings()
        {
            if (!File.Exists(_settingsPath))
            {
                Settings = new Settings();
                return;
            }

            try
            {
                string json = File.ReadAllText(_settingsPath, Encoding.UTF8);
                Settings = JsonConvert.DeserializeObject<Settings>(json) ?? new Settings();
            }
            catch
            {
                Settings = new Settings();
            }
        }

        /// <summary>
        /// Saves settings to JSON file
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                string json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
                File.WriteAllText(_settingsPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to save settings: " + ex.Message);
            }
        }

        /// <summary>
        /// Adds a new section
        /// </summary>
        public void AddSection(string sectionName)
        {
            if (Sections.Any(s => s.Name.Equals(sectionName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("Section already exists");
            }

            Sections.Add(new Section(sectionName));
            SavePasswords();
        }

        /// <summary>
        /// Removes a section
        /// </summary>
        public void RemoveSection(Section section)
        {
            Sections.Remove(section);
            SavePasswords();
        }

        /// <summary>
        /// Adds an account to a section
        /// </summary>
        public void AddAccount(Section section, string type, string email, string password)
        {
            section.AddAccount(new Account(type, email, password));
            SavePasswords();
        }

        /// <summary>
        /// Updates an account
        /// </summary>
        public void UpdateAccount(Account account, string type, string email, string password)
        {
            account.Type = type;
            account.Email = email;

            if (account.Password != password)
            {
                account.UpdatePassword(password);
            }

            SavePasswords();
        }

        /// <summary>
        /// Removes an account from a section
        /// </summary>
        public void RemoveAccount(Section section, Account account)
        {
            section.RemoveAccount(account);
            SavePasswords();
        }
    }
}
