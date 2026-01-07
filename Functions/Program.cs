using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Functions.Worker;
using System;
using Stripe;

// Configure Stripe API Key from environment
StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("StripeSecretKey");

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .Build();

host.Run();
