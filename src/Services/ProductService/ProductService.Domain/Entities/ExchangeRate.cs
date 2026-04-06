using Common.Domain.Entities;

namespace ProductService.Domain.Entities;

public class ExchangeRate : BaseEntity<Guid>
{
    public string FromCurrency { get; set; } = "USD";
    public string ToCurrency { get; set; } = "VND";
    public decimal Rate { get; set; }
    public DateTime FetchedAt { get; set; }
}
