using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace EnvelopePrinter.Core
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<Recipient> Recipients { get; set; }
        public DbSet<Template> Templates { get; set; }
        public DbSet<TemplateItem> TemplateItems { get; set; }

        private readonly string _databasePath;

        public ApplicationDbContext()
        {
            // Store DB in per-user LocalAppData to avoid writing beside the exe (Program Files)
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appData, "EnvelopePrinter");
            if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);
            _databasePath = Path.Combine(appFolder, "envelopes.db");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={_databasePath}");
        }
    }
}