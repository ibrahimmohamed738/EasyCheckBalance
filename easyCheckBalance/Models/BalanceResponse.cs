namespace easyCheckBalance.Models
{
    public class BalanceResponse
    {
        public decimal Balance { get; set; }
        public decimal FrBalance { get; set; }
        public string TxnStatus { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
