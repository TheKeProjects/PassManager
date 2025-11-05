using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Win32;
using PassManager.Models;

namespace PassManager.UI
{
    public class CsvExporter
    {
        private readonly DataManager _dataManager;

        public CsvExporter(DataManager dataManager)
        {
            _dataManager = dataManager;
        }

        public void Export()
        {
            if (_dataManager.Sections.Count == 0)
            {
                MessageBox.Show("No data to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                Title = "Export Passwords to CSV",
                FileName = $"PassManager_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveFileDialog.ShowDialog() != true)
                return;

            try
            {
                var records = PrepareExportData();
                WriteCsvFile(saveFileDialog.FileName, records);

                MessageBox.Show($"Successfully exported {records.Count} accounts to CSV.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                throw new Exception($"Export failed: {ex.Message}");
            }
        }

        private List<ExportRecord> PrepareExportData()
        {
            var records = new List<ExportRecord>();

            foreach (var section in _dataManager.Sections)
            {
                foreach (var account in section.Accounts)
                {
                    records.Add(new ExportRecord
                    {
                        Section = section.Name,
                        Type = account.Type,
                        Email = account.Email,
                        Password = account.Password
                    });
                }
            }

            return records;
        }

        private void WriteCsvFile(string filePath, List<ExportRecord> records)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            };

            using var writer = new StreamWriter(filePath);
            using var csv = new CsvWriter(writer, config);

            csv.WriteRecords(records);
        }

        private class ExportRecord
        {
            public string Section { get; set; }
            public string Type { get; set; }
            public string Email { get; set; }
            public string Password { get; set; }
        }
    }
}
