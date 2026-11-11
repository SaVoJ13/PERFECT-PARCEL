namespace KebabStreet.Services
{
    /// <summary>Відомості про продукт зі стору. ID — з iap_catalog.json.</summary>
    public sealed class ProductInfo
    {
        public ProductInfo(string productId, string localizedPrice)
        {
            ProductId = productId;
            LocalizedPrice = localizedPrice;
        }

        public string ProductId { get; }

        /// <summary>Ціна для показу, вже локалізована стором (рядок готовий до відображення).</summary>
        public string LocalizedPrice { get; }
    }
}
