using System.Threading.Tasks;
using Quantic.Core;
using Transfers.API.Model;
using Transfers.API.Query;

namespace Transfers.API.Commands
{
    public class DoTransferHandler : ICommandHandler<DoTransfer>
    {
        private readonly IQueryHandler<GetAccountByNumber, Account> getAccountByNumberHandler;
        private readonly IQueryHandler<GetCustomerByCif, Customer> getCustomerByCifHandler;
        private readonly IQueryHandler<GetCustomerLimit, Limit> getCustomerLimitHandler;

        public DoTransferHandler(IQueryHandler<GetAccountByNumber, Account> getAccountByNumberHandler,
            IQueryHandler<GetCustomerByCif, Customer> getCustomerByCifHandler,
            IQueryHandler<GetCustomerLimit, Limit> getCustomerLimitHandler)
        {
            this.getAccountByNumberHandler = getAccountByNumberHandler;
            this.getCustomerByCifHandler = getCustomerByCifHandler;
            this.getCustomerLimitHandler = getCustomerLimitHandler;
        }

        public async Task<CommandResult> Handle(DoTransfer command, RequestContext context)
        {
            var getAccount = await getAccountByNumberHandler.Handle(new GetAccountByNumber(command.From), context);
            if (getAccount.HasError)
                return CommandResult.WithError(Msg.GetAccountError, "get from account error");

            var fromAccount = getAccount.Result;

            getAccount = await getAccountByNumberHandler.Handle(new GetAccountByNumber(command.To), context);
            if (getAccount.HasError)
                return CommandResult.WithError(Msg.GetAccountError, "get to account error");

            var toAccount = getAccount.Result;

            var getCustomer = await getCustomerByCifHandler.Handle(new GetCustomerByCif(fromAccount.CIF), context);
            if (getCustomer.HasError)
                return CommandResult.WithError(getCustomer.Errors);

            var customer = getCustomer.Result;

            var getLimit = await getCustomerLimitHandler.Handle(new GetCustomerLimit(customer.CIF), context);
            if (getLimit.HasError)
                return CommandResult.WithError(getLimit.Errors);

            return CommandResult.Success;
        }
    }
    public class DoTransfer : ICommand
    {
        public DoTransfer(string from, string to, double amount)
        {
            From = from;
            To = to;
            Amount = amount;
        }

        public string From { get; }
        public string To { get; }
        public double Amount { get; }
    }
}
