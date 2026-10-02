using easyCheckBalance.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace easyCheckBalance.Core
{
    public class DahabAPI
    {
        private string _token = "";
        private readonly Config _config;
        private readonly ILogger<DahabAPI> _logger;
        private readonly HttpClient _client;
        private readonly IConfiguration _configuration;
        private DateTime _tokenExpiry;
        public DahabAPI(ILogger<DahabAPI> logger, IOptions<Config> options, HttpClient client, IConfiguration configuration)
        {
            _logger = logger;
            _client = client;
            _configuration = configuration;
            _config = options.Value;
        }

        public async Task<string> GetToken()
        {
            if (!string.IsNullOrEmpty(_token))
            {
                return _token;
            }

            var request = new
            {
                username = "plshortcodes",
                password = "$plshort$codes@&%"
            };
            using var _client = new HttpClient();
            _client.BaseAddress = new Uri(_config.EDahabApiUrl);
            var response = await _client.PostAsJsonAsync("Auth/login", request);
            if (!response.IsSuccessStatusCode)
            {
                return "";
            }
            var result = await response.Content.ReadFromJsonAsync<DahabResponse<string>>();
            if (result is null)
            {
                return "";
            }
            _token = result.Data;
            return _token;
        }

        public async Task<EDahabUserInfo> GetUserInfo(string msisdn)
        {
            using var _client = new HttpClient();
            _client.BaseAddress = new Uri(_config.EDahabApiUrl);
            await GetToken();
            _client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + _token);
            var response = await _client.GetAsync($"EDahab/GetUserInfo/{msisdn}");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<DahabResponse<EDahabUserInfo>>();
            if (result is null)
            {
                return null;
            }
            var user = new EDahabUserInfo
            {
                UserId = result.Data.UserId,
                Msisdn = result.Data.Msisdn,
                FullName = result.Data.FullName,
                CategoryCode = result.Data.CategoryCode,
                Gender = result.Data.Gender,
                Status = result.Data.Status,
                IsBarred = result.Data.IsBarred,
            };
            return user;
        }

        public async Task<DahabResponse<BalanceResponse>> GetBalance(string Msisdn, string Pin)
        {
            using var _client = new HttpClient();
            _client.BaseAddress = new Uri(_config.EDahabApiUrl);
            await GetToken();

            var request = new
            {
                msisdn = Msisdn,
                pin = Pin
            };
            _client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + _token);
            var response = await _client.PostAsJsonAsync($"EDahab/GetBalance", request);
            if (!response.IsSuccessStatusCode)
            {
                return new DahabResponse<BalanceResponse> { Data = null, Message = "Invalid Request", Success = false };
            }

            var result = await response.Content.ReadFromJsonAsync<DahabResponse<BalanceResponse>>();
            if (result is null)
            {
                return new DahabResponse<BalanceResponse> { Data = null, Message = "No data found", Success = false };
            }
            return result;
        }



        //Login to Balance API 
        private async Task<bool> EnsureTokenAsync(CancellationToken ct = default)
        {
            // Reuse token if valid
            if (!string.IsNullOrEmpty(_token) && _tokenExpiry > DateTime.UtcNow)
                return true;

            try
            {
                var loginRequest = new
                {
                    username = _config.ApiAuth.Username,
                    password = _config.ApiAuth.Password
                };

                var content = new StringContent(JsonSerializer.Serialize(loginRequest), Encoding.UTF8, "application/json");

                var response = await _client.PostAsync(_config.ApiAuth.LoginUrl, content, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to login. StatusCode: {StatusCode}", response.StatusCode);
                    return false;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                var token = doc.RootElement.GetProperty("data").GetProperty("token").GetString();

                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogError("Token not found in login response.");
                    return false;
                }

                _token = token;
                _tokenExpiry = DateTime.UtcNow.AddMinutes(30); // optional rough expiry assumption

                _logger.LogInformation("Successfully retrieved API token.");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during API login.");
                return false;
            }
        }


        public async Task<DahabResponse<CheckBalanceData>> GetBalanceNew(string msisdn, string pin, CancellationToken ct = default)
        {
            try
            {
                if (!await EnsureTokenAsync(ct))
                {
                    return new DahabResponse<CheckBalanceData>
                    {
                        Success = false,
                        Message = "Cosdigani waa mid khaldan, Fadlan mar kale isku day codsi sax ah",
                        Data = null
                    };
                }

                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

                var requestBody = new { msisdn, mPin = pin };
                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var response = await _client.PostAsync($"{_config.BalanceApiUrl}/api/Dahab/check-balance", content, ct);

                // Retry once if unauthorized
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Token expired or invalid, refreshing...");
                    _token = null; // reset
                    await EnsureTokenAsync(ct);
                    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
                    response = await _client.PostAsync($"{_config.BalanceApiUrl}/api/Dahab/check-balance", content, ct);
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to fetch balance. Status Code: {StatusCode}", response.StatusCode);
                    return new DahabResponse<CheckBalanceData>
                    {
                        Success = false,
                        Message = "Cosdigani waa mid khaldan, Fadlan mar kale isku day codsi sax ah",
                        Data = null
                    };
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var apiResponse = JsonSerializer.Deserialize<DahabResponse<CheckBalanceData>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Data == null)
                {
                    return new DahabResponse<CheckBalanceData>
                    {
                        Success = false,
                        Message = "Cosdigani waa mid khaldan, Fadlan mar kale isku day codsi sax ah",
                        Data = null
                    };
                }

                var balanceData = new CheckBalanceData
                {
                    Dollar = apiResponse.Data.Dollar,
                    Shl = apiResponse.Data.Shl
                };

                // ✅ Success only when both statuses == 200
                bool success = balanceData.Dollar?.TxnStatus == "200" && balanceData.Shl?.TxnStatus == "200";

                // ✅ Friendly error message selection
                string finalMessage;

                if (success)
                {
                    finalMessage = apiResponse.Message ?? "Balance fetched successfully.";
                }
                else if (balanceData != null && balanceData.Dollar.TxnStatus != "200" && !string.IsNullOrWhiteSpace(balanceData.Dollar.Message))
                {
                    finalMessage = balanceData.Dollar.Message;
                }
                else if (balanceData != null && balanceData.Shl.TxnStatus != "200" && !string.IsNullOrWhiteSpace(balanceData.Shl.Message))
                {
                    finalMessage = balanceData.Shl.Message;
                }
                else
                {
                    finalMessage = "Cosdigani waa mid khaldan, Fadlan mar kale isku day codsi sax ah";
                }

                return new DahabResponse<CheckBalanceData>
                {
                    Success = success,
                    Message = finalMessage,
                    Data = success ? balanceData : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Dahab API.");
                return new DahabResponse<CheckBalanceData>
                {
                    Success = false,
                    Message = "Cosdigani waa mid khaldan, Fadlan mar kale isku day codsi sax ah",
                    Data = null
                };
            }
        }


        public async Task<DahabResponse<CheckBalanceData>> GetChannelBalanceNew(string msisdn, string pin, CancellationToken ct = default)
        {
            try
            {
                if (!await EnsureTokenAsync(ct))
                {
                    return new DahabResponse<CheckBalanceData>
                    {
                        Success = false,
                        Message = "Cosdigani waa mid khaldan, Fadlan mar kale isku day codsi sax ah",
                        Data = null
                    };
                }

                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

                var requestBody = new { msisdn, mPin = pin };
                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var response = await _client.PostAsync($"{_config.BalanceApiUrl}/api/Dahab/check-balance-channel", content, ct);

                // Retry once if unauthorized
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Token expired or invalid, refreshing...");
                    _token = null; // reset
                    await EnsureTokenAsync(ct);
                    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
                    response = await _client.PostAsync($"{_config.BalanceApiUrl}/api/Dahab/check-balance-channel", content, ct);
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to fetch balance. Status Code: {StatusCode}", response.StatusCode);
                    return new DahabResponse<CheckBalanceData>
                    {
                        Success = false,
                        Message = "Cosdigani waa mid khaldan, Fadlan mar kale isku day codsi sax ah",
                        Data = null
                    };
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var apiResponse = JsonSerializer.Deserialize<DahabResponse<CheckBalanceData>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Data == null)
                {
                    return new DahabResponse<CheckBalanceData>
                    {
                        Success = false,
                        Message = "Cosdigani waa mid khaldan, Fadlan mar kale isku day codsi sax ah",
                        Data = null
                    };
                }

                var balanceData = new CheckBalanceData
                {
                    Dollar = apiResponse.Data.Dollar,
                    Shl = apiResponse.Data.Shl
                };

                // ✅ Success only when both statuses == 200
                bool success = balanceData.Dollar?.TxnStatus == "200" && balanceData.Shl?.TxnStatus == "200";

                // ✅ Friendly error message selection
                string finalMessage;

                if (success)
                {
                    finalMessage = apiResponse.Message ?? "Balance fetched successfully.";
                }
                else if (balanceData != null && balanceData.Dollar.TxnStatus != "200" && !string.IsNullOrWhiteSpace(balanceData.Dollar.Message))
                {
                    finalMessage = balanceData.Dollar.Message;
                }
                else if (balanceData != null && balanceData.Shl.TxnStatus != "200" && !string.IsNullOrWhiteSpace(balanceData.Shl.Message))
                {
                    finalMessage = balanceData.Shl.Message;
                }
                else
                {
                    finalMessage = "Cosdigani waa mid khaldan, Fadlan mar kale isku day codsi sax ah";
                }

                return new DahabResponse<CheckBalanceData>
                {
                    Success = success,
                    Message = finalMessage,
                    Data = success ? balanceData : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Dahab API.");
                return new DahabResponse<CheckBalanceData>
                {
                    Success = false,
                    Message = "Cosdigani waa mid khaldan, Fadlan mar kale isku day codsi sax ah",
                    Data = null
                };
            }
        }


        public async Task<DahabResponse<CheckBalanceData>> GetBalancePayxNew(string msisdn, string pin, CancellationToken ct = default)
        {
            try
            {
                var requestBody = new
                {
                    phone = msisdn,
                    pin = pin
                };

                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{_config.PayxApiUrl}/edahab/balance-with-pin");

                request.Headers.TryAddWithoutValidation(
                    "X-API-KEY",
                    _config.PayxApiKey);

                request.Headers.TryAddWithoutValidation(
                    "X-API-SECRET",
                    _config.PayxApiSecret);

                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                var response = await _client.SendAsync(request, ct);

                var json = await response.Content.ReadAsStringAsync(ct);

                // Failed response
                if (!response.IsSuccessStatusCode)
                {
                    string message =
                        "Cosdigani waa mid khaldan, Fadlan mar kale isku day codsi sax ah";

                    try
                    {
                        var errorResponse =
                            JsonSerializer.Deserialize<BalanceErrorResponse>(
                                json,
                                new JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true
                                });

                        if (!string.IsNullOrWhiteSpace(errorResponse?.Message))
                        {
                            message = errorResponse.Message;
                        }
                    }
                    catch
                    {
                        // Keep default error message
                    }

                    _logger.LogWarning(
                        "Balance request failed. MSISDN: {Msisdn}, StatusCode: {StatusCode}, Response: {Response}",
                        msisdn,
                        response.StatusCode,
                        json);

                    return new DahabResponse<CheckBalanceData>
                    {
                        Success = false,
                        Message = message,
                        Data = null
                    };
                }

                // Deserialize successful response
                var apiResponse =
                    JsonSerializer.Deserialize<PayxBalanceResponse>(
                        json,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                if (apiResponse?.Balances == null)
                {
                    return new DahabResponse<CheckBalanceData>
                    {
                        Success = false,
                        Message = "Balance information was not found.",
                        Data = null
                    };
                }

                // Find Dollar balance
                var dollar = apiResponse.Balances
                    .FirstOrDefault(x =>
                        string.Equals(
                            x.Currency,
                            "dollar",
                            StringComparison.OrdinalIgnoreCase));

                // Find Shilling balance
                var shilling = apiResponse.Balances
                    .FirstOrDefault(x =>
                        string.Equals(
                            x.Currency,
                            "shilling",
                            StringComparison.OrdinalIgnoreCase));

                var balanceData = new CheckBalanceData
                {
                    Dollar = new BalanceResponse
                    {
                        Balance = dollar?.Balance ?? 0,
                        FrBalance = dollar?.FrozenAmount ?? 0,
                        TxnStatus = "200",
                        Message = "Success"
                    },

                    Shl = new BalanceResponse
                    {
                        Balance = shilling?.Balance ?? 0,
                        FrBalance = shilling?.FrozenAmount ?? 0,
                        TxnStatus = "200",
                        Message = "Success"
                    }
                };

                return new DahabResponse<CheckBalanceData>
                {
                    Success = true,
                    Message = "Balance fetched successfully.",
                    Data = balanceData
                };
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Balance request cancelled for {Msisdn}.",
                    msisdn);

                return new DahabResponse<CheckBalanceData>
                {
                    Success = false,
                    Message = "Request cancelled.",
                    Data = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error calling PayX Balance API for {Msisdn}.",
                    msisdn);

                return new DahabResponse<CheckBalanceData>
                {
                    Success = false,
                    Message = "Cosdigani waa mid khaldan, Fadlan mar kale isku day codsi sax ah",
                    Data = null
                };
            }
        }
    }
}
