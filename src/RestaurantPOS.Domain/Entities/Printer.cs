using RestaurantPOS.Domain.Enums;

namespace RestaurantPOS.Domain.Entities
{
    public class Printer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public PrinterType PrinterType { get; set; }
        public StationType Station { get; set; }
        public string Target { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public int SortOrder { get; set; }
    }
}