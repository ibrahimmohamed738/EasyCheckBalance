namespace easyCheckBalance.Models;

public class PayxBalanceResponse
{
    public string Id { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    public List<PayxBalance> Balances { get; set; } = new();
}

public class PayxBalance
{
    public decimal Balance { get; set; }
    public decimal FrozenAmount { get; set; }
    public decimal FicAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
}

public class BalanceErrorResponse
{
    public string Message { get; set; } = string.Empty;
}
