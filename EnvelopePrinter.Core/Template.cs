namespace EnvelopePrinter.Core
{
    /// <summary>
    /// Описывает шаблон конверта, включая его размеры и набор элементов.
    /// </summary>
    public class Template
    {
        public int Id { get; set; }
        public string Name { get; set; }

        // Размеры конверта в миллиметрах
        public double EnvelopeWidth { get; set; } = 220; // Стандартный DL/E65
        public double EnvelopeHeight { get; set; } = 110;

        // Коллекция всех элементов, которые принадлежат этому шаблону
        public virtual List<TemplateItem> Items { get; set; } = new List<TemplateItem>();

        // Фон холста: путь и режим растяжения (сохраняем в БД)
        public string BackgroundImagePath { get; set; } = string.Empty;
        public string BackgroundStretch { get; set; } = "Uniform"; // None/Uniform/Fill/UniformToFill
    }
}