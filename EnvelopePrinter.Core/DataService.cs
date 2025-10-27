using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;

namespace EnvelopePrinter.Core
{
    public class DataService
    {
        private readonly string _databasePath;

        public DataService()
        {
            var folder = AppDomain.CurrentDomain.BaseDirectory;
            _databasePath = Path.Combine(folder, "envelopes.db");

            TryApplyMigrations();
            TryEnsureTemplateItemColumns();
            TryEnsureRecipientColumns();
        }

        private void TryApplyMigrations()
        {
            try
            {
                var dir = Path.GetDirectoryName(_databasePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                optionsBuilder.UseSqlite($"Data Source={_databasePath}");

                using var context = new ApplicationDbContext();
                context.Database.Migrate();
            }
            catch { }
        }

        private void TryEnsureTemplateItemColumns()
        {
            try
            {
                if (!File.Exists(_databasePath)) return;
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                connection.Open();
                using var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "PRAGMA table_info('TemplateItems');";
                using var reader = checkCmd.ExecuteReader();
                var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (reader.Read())
                {
                    if (!reader.IsDBNull(1))
                    {
                        var colName = reader.GetString(1);
                        if (!string.IsNullOrEmpty(colName)) existing.Add(colName);
                    }
                }
                var toAdd = new List<string>();
                if (!existing.Contains("Name")) toAdd.Add("ALTER TABLE TemplateItems ADD COLUMN Name TEXT;");
                if (!existing.Contains("ImagePath")) toAdd.Add("ALTER TABLE TemplateItems ADD COLUMN ImagePath TEXT;");
                if (!existing.Contains("IsImage")) toAdd.Add("ALTER TABLE TemplateItems ADD COLUMN IsImage INTEGER NOT NULL DEFAULT0;");
                foreach (var sql in toAdd)
                {
                    using var alterCmd = connection.CreateCommand();
                    alterCmd.CommandText = sql; try { alterCmd.ExecuteNonQuery(); } catch { }
                }
            }
            catch { }
        }

        private void TryEnsureRecipientColumns()
        {
            try
            {
                if (!File.Exists(_databasePath)) return;
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                connection.Open();
                using var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "PRAGMA table_info('Recipients');";
                using var reader = checkCmd.ExecuteReader();
                var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (reader.Read())
                {
                    if (!reader.IsDBNull(1))
                    {
                        var colName = reader.GetString(1);
                        if (!string.IsNullOrEmpty(colName)) existing.Add(colName);
                    }
                }
                var toAdd = new List<string>();
                if (!existing.Contains("Region")) toAdd.Add("ALTER TABLE Recipients ADD COLUMN Region TEXT;");
                if (!existing.Contains("Country")) toAdd.Add("ALTER TABLE Recipients ADD COLUMN Country TEXT;");
                foreach (var sql in toAdd)
                {
                    using var alterCmd = connection.CreateCommand();
                    alterCmd.CommandText = sql; try { alterCmd.ExecuteNonQuery(); } catch { }
                }
            }
            catch { }
        }

        #region Recipients
        public List<Recipient> GetAllRecipients()
        {
            using (var context = new ApplicationDbContext())
            {
                return context.Recipients.ToList();
            }
        }
        public void AddRecipient(Recipient recipient)
        {
            using (var context = new ApplicationDbContext())
            {
                context.Recipients.Add(recipient);
                context.SaveChanges();
            }
        }
        public void UpdateRecipient(Recipient recipient)
        {
            using (var context = new ApplicationDbContext())
            {
                context.Recipients.Update(recipient);
                context.SaveChanges();
            }
        }
        public void DeleteRecipient(Recipient recipient)
        {
            using (var context = new ApplicationDbContext())
            {
                context.Recipients.Remove(recipient);
                context.SaveChanges();
            }
        }
        #endregion

        #region Templates
        public List<Template> GetAllTemplates()
        {
            using (var context = new ApplicationDbContext())
            {
                return context.Templates.Include(t => t.Items).ToList();
            }
        }
        public void AddTemplate(Template template)
        {
            using (var context = new ApplicationDbContext())
            {
                context.Templates.Add(template);
                context.SaveChanges();
            }
        }
        public void UpdateTemplate(Template template)
        {
            using (var context = new ApplicationDbContext())
            {
                context.Templates.Update(template);
                context.SaveChanges();
            }
        }
        public void DeleteTemplate(Template template)
        {
            using (var context = new ApplicationDbContext())
            {
                var tracked = context.Templates.Include(t => t.Items).FirstOrDefault(t => t.Id == template.Id);
                if (tracked != null)
                {
                    if (tracked.Items.Any()) context.TemplateItems.RemoveRange(tracked.Items);
                    context.Templates.Remove(tracked);
                    context.SaveChanges();
                }
            }
        }
        #endregion

        #region Excel import/export
        public void ExportToExcel(string filePath)
        {
            var recipients = GetAllRecipients();
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Получатели");
                // Заголовки
                ws.Cell(1,1).Value = "Учреждение";
                ws.Cell(1,2).Value = "Улица";
                ws.Cell(1,3).Value = "Индекс";
                ws.Cell(1,4).Value = "Город";
                ws.Cell(1,5).Value = "Область";
                ws.Cell(1,6).Value = "Страна";

                // Стиль шапки: синий фон, белый жирный текст
                var header = ws.Range(1,1,1,6);
                header.Style.Fill.BackgroundColor = XLColor.FromHtml("#2F5597");
                header.Style.Font.Bold = true;
                header.Style.Font.FontColor = XLColor.White;
                header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Данные
                int r =2;
                foreach (var x in recipients)
                {
                    ws.Cell(r,1).Value = x.OrganizationName;
                    ws.Cell(r,2).Value = x.AddressLine1;
                    ws.Cell(r,3).Value = x.PostalCode;
                    ws.Cell(r,4).Value = x.City;
                    ws.Cell(r,5).Value = x.Region;
                    ws.Cell(r,6).Value = x.Country;
                    r++;
                }
                var used = ws.Range(1,1,Math.Max(r-1,1),6);
                used.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                used.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                ws.Columns(1,6).AdjustToContents();
                workbook.SaveAs(filePath);
            }
        }

        public void ImportFromExcel(string filePath)
        {
            using (var workbook = new XLWorkbook(filePath))
            {
                var worksheet = workbook.Worksheet(1);
                var used = worksheet.RangeUsed();
                if (used == null) return;
                var rows = used.RowsUsed();
                if (!rows.Any()) return;

                // Map headers to indices, support old and new headers
                var headerRow = rows.First();
                var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                int colCount = headerRow.CellCount();
                for (int c =1; c <= colCount; c++)
                {
                    var header = headerRow.Cell(c).GetValue<string>().Trim();
                    if (!string.IsNullOrEmpty(header) && !map.ContainsKey(header)) map[header] = c;
                }

                int Col(string key, string alt1 = null, string alt2 = null)
                {
                    if (map.TryGetValue(key, out var idx)) return idx;
                    if (alt1 != null && map.TryGetValue(alt1, out idx)) return idx;
                    if (alt2 != null && map.TryGetValue(alt2, out idx)) return idx;
                    return -1;
                }

                int orgCol = Col("Учреждение", "Организация");
                int streetCol = Col("Улица", "Адрес");
                int indexCol = Col("Индекс");
                int cityCol = Col("Город");
                int regionCol = Col("Область");
                int countryCol = Col("Страна");

                var recipientsToImport = new List<Recipient>();
                foreach (var row in rows.Skip(1))
                {
                    if (row.IsEmpty()) continue;
                    var r = new Recipient
                    {
                        OrganizationName = orgCol >0 ? row.Cell(orgCol).GetValue<string>() : string.Empty,
                        AddressLine1 = streetCol >0 ? row.Cell(streetCol).GetValue<string>() : string.Empty,
                        PostalCode = indexCol >0 ? row.Cell(indexCol).GetValue<string>() : string.Empty,
                        City = cityCol >0 ? row.Cell(cityCol).GetValue<string>() : string.Empty,
                        Region = regionCol >0 ? row.Cell(regionCol).GetValue<string>() : string.Empty,
                        Country = countryCol >0 ? row.Cell(countryCol).GetValue<string>() : string.Empty
                    };
                    // если строка пустая по основным полям — пропускаем
                    if (string.IsNullOrWhiteSpace(r.OrganizationName) && string.IsNullOrWhiteSpace(r.AddressLine1) && string.IsNullOrWhiteSpace(r.City) && string.IsNullOrWhiteSpace(r.PostalCode))
                        continue;
                    recipientsToImport.Add(r);
                }

                if (recipientsToImport.Count ==0) return;
                using (var context = new ApplicationDbContext())
                {
                    context.Recipients.AddRange(recipientsToImport);
                    context.SaveChanges();
                }
            }
        }
        #endregion

        #region Backup/restore
        private void CheckpointWal()
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "PRAGMA wal_checkpoint(FULL);"; cmd.ExecuteNonQuery();
            }
            catch { }
        }
        private static void ClearPools() { try { SqliteConnection.ClearAllPools(); } catch { } }

        public void BackupDatabase(string destinationPath)
        {
            CheckpointWal();
            ClearPools();
            File.Copy(_databasePath, destinationPath, true);

            TryCopySideFile(_databasePath + "-wal", destinationPath + "-wal");
            TryCopySideFile(_databasePath + "-shm", destinationPath + "-shm");
        }

        public void RestoreDatabase(string sourcePath)
        {
            ClearPools();
            File.Copy(sourcePath, _databasePath, true);
            TryDeleteSideFile(_databasePath + "-wal");
            TryDeleteSideFile(_databasePath + "-shm");
        }

        private static void TryCopySideFile(string from, string to)
        {
            try { if (File.Exists(from)) File.Copy(from, to, true); } catch { }
        }
        private static void TryDeleteSideFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
        #endregion
    }
}