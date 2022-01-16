using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Transfers.API.Model;
using Quantic.Core;
using System.Net.Http;
using System.Text.Json;

namespace Transfers.API.Query
{
    public class GetCustomerByCifHandler : IQueryHandler<GetCustomerByCif, Customer>
    {
        private readonly HttpClient httpClient;
        private readonly Config config;

        public GetCustomerByCifHandler(HttpClient httpClient, Config config)
        {
            this.httpClient = httpClient;
            this.config = config;
        }

        public async Task<QueryResult<Customer>> Handle(GetCustomerByCif query, RequestContext context)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{config.CustomersApiUrl}/customers?cif={query.Cif}");

            request.Headers.Add("Accept", "application / json");
            request.Headers.Add("quantic-trace-id", context.TraceId);

            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return QueryResult<Customer>.WithError(Msg.GetCustomerError);

            var getCustomer = await JsonSerializer.DeserializeAsync<QueryResponse<Customer>>(await response.Content.ReadAsStreamAsync(), JsonCfg.Options);
            return QueryResult<Customer>.WithResult(getCustomer.Data);
        }
    }
    public class GetCustomerByCif : IQuery<Customer>
    {
        public GetCustomerByCif(string cif)
        {
            Cif = cif;
        }

        public string Cif { get; }
    }
}
