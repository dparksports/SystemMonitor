using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Stripe;

namespace DeviceMonitorFunctions
{
    public class StripeWebhook
    {
        private readonly ILogger _logger;
        // Note: For production with multiple instances, use a database or distributed cache (Redis).
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _processedEvents = new();

        public StripeWebhook(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<StripeWebhook>();
        }

        [Function("StripeWebhook")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            try
            {
                string json = await new StreamReader(req.Body).ReadToEndAsync();
                
                // Get the signature header
                string? signature = null;
                if (req.Headers.TryGetValues("Stripe-Signature", out var values))
                {
                    signature = values.FirstOrDefault();
                }

                if (string.IsNullOrEmpty(signature))
                {
                    _logger.LogError("Missing Stripe-Signature header");
                    var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badReq.WriteStringAsync("Missing Stripe-Signature header");
                    return badReq;
                }

                // Verify the event
                var secret = Environment.GetEnvironmentVariable("StripeWebhookSecret");
                if (string.IsNullOrEmpty(secret))
                {
                     _logger.LogError("StripeWebhookSecret environment variable is null or empty");
                     return req.CreateResponse(HttpStatusCode.InternalServerError);
                }

                var stripeEvent = EventUtility.ConstructEvent(json, signature, secret, throwOnApiVersionMismatch: false);

                // Idempotency Check
                if (!_processedEvents.TryAdd(stripeEvent.Id, true))
                {
                    _logger.LogInformation($"Event {stripeEvent.Id} already processed. Skipping.");
                    return req.CreateResponse(HttpStatusCode.OK);
                }

                // Handle the event
                if (stripeEvent.Type == Events.CustomerSubscriptionCreated)
                {
                    var subscription = stripeEvent.Data.Object as Subscription;
                    if (subscription != null)
                    {
                        var licenseKey = Guid.NewGuid().ToString().ToUpper();
                        _logger.LogInformation($"Generating license key {licenseKey} for subscription {subscription.Id}");

                        var options = new SubscriptionUpdateOptions
                        {
                            Metadata = new Dictionary<string, string>
                            {
                                { "license_key", licenseKey }
                            }
                        };

                        var service = new SubscriptionService();
                        await service.UpdateAsync(subscription.Id, options);
                    }
                }

                return req.CreateResponse(HttpStatusCode.OK);
            }
            catch (StripeException e)
            {
                _logger.LogWarning($"Stripe signature verification failed. Message: {e.Message}");
                var res = req.CreateResponse(HttpStatusCode.BadRequest);
                await res.WriteStringAsync($"Webhook signature verification failed: {e.Message}");
                return res;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error processing Stripe webhook");
                // Return 500 as requested for update failures
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
