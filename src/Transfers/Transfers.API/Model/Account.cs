namespace Transfers.API.Model
{
    public class Account
    {
        public string AccountNumber { get; set; }
        public string AccountType { get; set; }
        public string CIF { get; set; }  
        public double Balance { get; set; } 
        public string BranchCode { get; set; }                        
    }
}
