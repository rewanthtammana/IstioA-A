using System.Linq;
using System.Threading.Tasks;
using Transfers.API.Model;
using Quantic.Core;
using System.Net.Http;
using System.Text.Json;

namespace Transfers.API.Query
{
    public class GetCustomerLimitHandler : IQueryHandler<GetCustomerLimit, Limit>
    {
        private readonly HttpClient httpClient;
        private readonly Config config;

        public GetCustomerLimitHandler(HttpClient httpClient, Config config)
        {
            this.httpClient = httpClient;
            this.config = config;
        }

        public async Task<QueryResult<Limit>> Handle(GetCustomerLimit query, RequestContext context)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{config.LimitsApiUrl}/limits?cif={query.Cif}");

            request.Headers.Add("Accept", "application / json");
            request.Headers.Add("quantic-trace-id", context.TraceId);

            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return QueryResult<Limit>.WithError(Msg.GetLimitError);

            var getLimit = await JsonSerializer.DeserializeAsync<QueryResponse<Limit>>(await response.Content.ReadAsStreamAsync(), JsonCfg.Options);
            return QueryResult<Limit>.WithResult(getLimit.Data);
        }
    }
    public class GetCustomerLimit: IQuery<Limit>
    {
        public GetCustomerLimit(string cif)
        {
            Cif = cif;
        }

        public string Cif { get; }
    }
}
