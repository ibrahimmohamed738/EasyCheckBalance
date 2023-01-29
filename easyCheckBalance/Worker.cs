using System;
using System.Threading;
using System.Threading.Tasks;
using easyCheckBalance;
using easyCheckBalance.Core;
using JamaaTech.Smpp.Net.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace easyCheckBalance
{

    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IServiceProvider _provider;
        private readonly Config _config;
        private readonly SmppClient _client = new();

        [Obsolete]
        public Worker(ILogger<Worker> logger, IOptions<Config> options, IServiceProvider provider)
        {
            _logger = logger;
            _provider = provider;
            _config = options.Value;

            _client.Properties.Host = _config.IPAddress;
            _client.Properties.DefaultEncoding = JamaaTech.Smpp.Net.Lib.DataCoding.SMSCDefault;
            _client.Properties.DefaultServiceType = "USSD";
            _client.Properties.Port = _config.Port;
            _client.Properties.SystemID = _config.Username;
            _client.Properties.Password = _config.Password;
            _client.Properties.InterfaceVersion = JamaaTech.Smpp.Net.Lib.InterfaceVersion.v34;
            _client.AutoReconnectDelay = 1000;
            _client.ConnectionStateChanged += Client_ConnectionStateChanged;
            _client.MessageSent += Client_MessageSent;
            _client.MessageReceived += Client_MessageReceivedAsync;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            await Task.CompletedTask;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {

            _client.Start();

            await Task.Delay(500, cancellationToken);
        }

        private void Client_MessageSent(object sender, MessageEventArgs e)
        {
            _logger.LogInformation($"Sent a message: {e.ShortMessage?.DestinationAddress} at: {DateTime.Now}");
            _logger.LogInformation($"Message: {System.Text.Json.JsonSerializer.Serialize(e.ShortMessage)}");
        }

        private void Client_ConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e)
        {
            _logger.LogInformation($"Connection state changed: {e.PreviousState} {e.CurrentState}");
        }

        [Obsolete]
        private async void Client_MessageReceivedAsync(object sender, MessageEventArgs e)
        {
            var client = sender as SmppClient ?? _client;

            var message = e.ShortMessage as TextMessage;
            await ProcessAsync(client, message);
        }

        [Obsolete]
        public async Task ProcessAsync(SmppClient client, TextMessage message)
        {
            if (message.Text.StartsWith("Unknown Error"))
            {
                return;
            }

            var reply = new TextMessage
            {
                DestinationAddress = message.SourceAddress,
                RegisterDeliveryNotification = true,
                Tlv = message.Tlv,
                SessionId = message.SessionId,
            };

            if (_config.IsLimited && !_config.AllowList.Contains(message.SourceAddress))
            {
                reply.Text = "Macmiil, adeeggan hadda shaqo ayaa ka socota, fadlan maalin kale isku day. Mahadsanid! :)";
                _client.SendMessage(reply, 200);
                return;
            }

            try
            {
                var responseder = _provider.GetRequiredService<Responder>();
                var response = await responseder.ProcessAsync(message.SourceAddress[3..], message.Text);
                if (response is null || string.IsNullOrWhiteSpace(response.Text))
                {
                    response.Text = "Macasalaama!";
                }

                reply.Text = response.Text;
                if (reply.Text.Length > 160)
                {
                    _logger.LogWarning("Truncating message from {0} to {1} characters.", reply.Text.Length, 160);

                    reply.Text = reply.Text.Substring(0, 160);
                }

                reply.HasPrompt = response.ResponseType == ResponseType.Prompt;
            }
            catch (Exception ex) when (ex is TypeInitializationException exception)
            {
                _logger.LogError($"Inner exception: {exception.InnerException.Message} -- {exception.InnerException.StackTrace}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                _logger.LogError("Exception when contacting Soap Service: {0} -- {1}", ex.Message, ex.StackTrace);

                reply.Text = "Waanu ka xunnahay, qalad ayaa dhacay, fadlan mar kale isku day. :(";
            }

            try
            {
                _logger.LogInformation($"Replying with: {reply.Text} to {reply.DestinationAddress}");
                client.SendMessage(reply, 500);
                return;
            }
            catch (Exception ex) when (ex is JamaaTech.Smpp.Net.Lib.SmppException smppException)
            {
                _logger.LogError($"SMPP exception occurred: {smppException.Message} - {smppException.InnerException?.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception occurred: {ex.Message} -- {ex.InnerException?.Message}");
            }

            // If we run this far, something is wrong.
            reply.Text = "Waanu ka xunnahay, qalad ayaa dhacay, fadlan mar kale isku day. :(";
            client.SendMessage(reply, 5);
        }
    }

}
