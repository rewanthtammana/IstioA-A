using System;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;
using Quantic.Cache.InMemory;
using Quantic.Core;
using Quantic.Log;
using Transfers.API.Model;
using Transfers.API.Query;

namespace Transfers.API
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            var assemblies = new System.Reflection.Assembly[] { typeof(Startup).Assembly };

            services.AddQuantic(cfg =>
            {
                cfg.Assemblies = assemblies;
            })
            .AddMemoryCacheDecorator()
            .AddLogDecorator();

            services.AddControllers();
            
            services.AddApiVersioning(o =>
            {
                o.ReportApiVersions = true;
                o.AssumeDefaultVersionWhenUnspecified = true;
                o.DefaultApiVersion = new ApiVersion(1, 0);
            });             

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Limits.API", Version = "v1" });
            });      

            services.AddHttpClient<IQueryHandler<GetAccountByNumber, Account>, GetAccountByNumberHandler>()
                .SetHandlerLifetime(TimeSpan.FromMinutes(30))
                .AddPolicyHandler(GetRetryPolicy());

            services.AddHttpClient<IQueryHandler<GetCustomerLimit, Limit>, GetCustomerLimitHandler>()
                .SetHandlerLifetime(TimeSpan.FromMinutes(30))
                .AddPolicyHandler(GetRetryPolicy());

            services.AddHttpClient<IQueryHandler<GetCustomerByCif, Customer>, GetCustomerByCifHandler>()
                .SetHandlerLifetime(TimeSpan.FromMinutes(30))
                .AddPolicyHandler(GetRetryPolicy());   

            var config = new Config
            {
                AccountsApiUrl = Configuration.GetValue<string>("ACCOUNTS_URL"),
                CustomersApiUrl = Configuration.GetValue<string>("CUSTOMERS_URL"),
                LimitsApiUrl =   Configuration.GetValue<string>("LIMITS_URL")            
            };  

            services.AddSingleton(config);                          
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Limits.API v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }  

        static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            Random jitterer = new Random();
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                //  msg.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
                // || msg.StatusCode == System.Net.HttpStatusCode.GatewayTimeout
                // || msg.StatusCode == System.Net.HttpStatusCode.RequestTimeout
                .WaitAndRetryAsync(6,    // exponential back-off plus some jitter
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                                + TimeSpan.FromMilliseconds(jitterer.Next(0, 100))
                );
        }              
    }
}
