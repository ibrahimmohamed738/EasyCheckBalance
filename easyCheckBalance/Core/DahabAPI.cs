using easyCheckBalance.Models;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace easyCheckBalance.Core
{
    public class DahabAPI
    {
        private string _token = "";
        private readonly Config _config;
        private readonly ILogger<DahabAPI> _logger;
        private readonly HttpClient _client;
        private readonly IConfiguration _configuration;

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
            var response = await _client.PostAsJsonAsync($"EDahab/GetBalance",request);
            if (!response.IsSuccessStatusCode)
            {
                return new DahabResponse<BalanceResponse> { Data = null, Message="Invalid Request", Success=false };
            }

            var result = await response.Content.ReadFromJsonAsync<DahabResponse<BalanceResponse>>();
            if (result is null)
            {
                return new DahabResponse<BalanceResponse> { Data = null, Message = "No data found", Success = false };
            }
            return result;
        }
    }
}
