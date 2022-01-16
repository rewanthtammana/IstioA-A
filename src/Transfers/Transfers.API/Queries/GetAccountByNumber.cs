using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Quantic.Core;
using Transfers.API.Model;

namespace Transfers.API.Query
{
    public class GetAccountByNumberHandler : IQueryHandler<GetAccountByNumber, Account>
    {
        private readonly HttpClient httpClient;
        private readonly Config config;

        public GetAccountByNumberHandler(HttpClient httpClient, Config config)
        {
            this.httpClient = httpClient;
            this.config = config;
        }

        public async Task<QueryResult<Account>> Handle(GetAccountByNumber query, RequestContext context)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{config.AccountsApiUrl}/accounts?accountNumber={query.AccountNumber}");

            request.Headers.Add("Accept", "application / json");
            request.Headers.Add("quantic-trace-id", context.TraceId);

            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return QueryResult<Account>.WithError(Msg.GetAccountError, $"status code is {response.IsSuccessStatusCode}");

            var getAccount = await JsonSerializer.DeserializeAsync<QueryResponse<Account>>(await response.Content.ReadAsStreamAsync(), JsonCfg.Options);
            return QueryResult<Account>.WithResult(getAccount.Data);
        }
    }

    public class GetAccountByNumber : IQuery<Account>
    {
        public GetAccountByNumber(string accountNumber)
        {
            AccountNumber = accountNumber;
        }

        public string AccountNumber { get; }
    }
}
