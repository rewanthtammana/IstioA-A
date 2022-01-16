namespace Customers.API.Controllers
{
    public class NewCustomerRequest
    {
        public string CIF { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Status { get; set; }
    }
}
