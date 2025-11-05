using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Win32;
using PassManager.Models;

namespace PassManager.UI
{
    public class CsvImporter
    {
        private readonly DataManager _dataManager;

        public CsvImporter(DataManager dataManager)
        {
            _dataManager = dataManager;
        }

        public void Import()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                Title = "Select CSV File to Import"
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            try
            {
                var records = ReadCsvFile(openFileDialog.FileName);

                if (records.Count == 0)
                {
                    MessageBox.Show("No records found in CSV file.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                ImportRecords(records);

                MessageBox.Show($"Successfully imported {records.Count} records.", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                throw new Exception($"Import failed: {ex.Message}");
            }
        }

        private List<CsvRecord> ReadCsvFile(string filePath)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null
            };

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);

            var records = new List<CsvRecord>();

            csv.Read();
            csv.ReadHeader();

            while (csv.Read())
            {
                try
                {
                    // Try to read common CSV formats (Brave, Chrome, etc.)
                    var record = new CsvRecord
                    {
                        Name = csv.GetField<string>("name") ?? csv.GetField<string>("Name") ?? "",
                        Url = csv.GetField<string>("url") ?? csv.GetField<string>("URL") ?? "",
                        Username = csv.GetField<string>("username") ?? csv.GetField<string>("Username") ?? "",
                        Password = csv.GetField<string>("password") ?? csv.GetField<string>("Password") ?? ""
                    };

                    if (!string.IsNullOrWhiteSpace(record.Password))
                    {
                        records.Add(record);
                    }
                }
                catch
                {
                    // Skip malformed records
                    continue;
                }
            }

            return records;
        }

        private void ImportRecords(List<CsvRecord> records)
        {
            var importSection = _dataManager.Sections.FirstOrDefault(s => s.Name == "Imported");
            if (importSection == null)
            {
                _dataManager.AddSection("Imported");
                importSection = _dataManager.Sections.First(s => s.Name == "Imported");
            }

            foreach (var record in records)
            {
                string accountType = string.IsNullOrWhiteSpace(record.Name) ? record.Url : record.Name;
                if (string.IsNullOrWhiteSpace(accountType))
                    accountType = "Unknown";

                string email = string.IsNullOrWhiteSpace(record.Username) ? "No email" : record.Username;

                _dataManager.AddAccount(importSection, accountType, email, record.Password);
            }
        }

        private class CsvRecord
        {
            public string Name { get; set; }
            public string Url { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
        }
    }
}
