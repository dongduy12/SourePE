namespace API_WEB.Models.Repositories
{
    public class ProductWithPositionModel
    {
        public string? SerialNumber { get; set; } // Số Serial của sản phẩm
        public int PositionInTray { get; set; }  // Vị trí của sản phẩm trong khay
    }
}
