using System.IO;
using System.Text.Json;

namespace Envelope_printing
{
    public class FilterPreset
    {
        public string Name { get; set; }
        public string City { get; set; }
        public string SearchText { get; set; }
        public string SortBy { get; set; }
        public bool SortDescending { get; set; }
    }

    public class FilterPresetsService
    {
        private readonly string _filePath;
        public FilterPresetsService()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EnvelopePrinter");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, "print_presets.json");
        }

        public List<FilterPreset> Load()
        {
            try
            {
                if (!File.Exists(_filePath)) return new List<FilterPreset>();
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<FilterPreset>>(json) ?? new List<FilterPreset>();
            }
            catch { return new List<FilterPreset>(); }
        }

        public void Save(List<FilterPreset> presets)
        {
            try
            {
                var json = JsonSerializer.Serialize(presets, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch { /* ignore */ }
        }
    }
}
