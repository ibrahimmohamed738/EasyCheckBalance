//  ---------------------------------------------------------------------------------
//  Copyright © eDahab. All rights reserved.
//  ---------------------------------------------------------------------------------

using easyCheckBalance.Models;
using Microsoft.Extensions.Options;

namespace easyCheckBalance.Core
{

    public class Responder
    {
        private readonly Config _config;
        private Session _session;
        private readonly Repository _repository;
        private readonly DahabAPI _dahab;
        private const string WrongInput = "[-eDahab Service-]\nCosdigani waa mid khaldan, Fadlan mar kale isku day codsi sax ah.\n*340*1#";
        private const string WrongPin = "[-eDahab Service-]\nLambarka sirta ahi waa khalad.";
        private const string EnterPin = "Geli lambarka sirta ah";
        private const string BalanceMsg = "[-eDahab Service-]\nHaadhaagagu Waa\n";
        private const string AdsMessage = "Wac *101# oo kushubo internet iyo ku hadal";
        private const string AdsMessagePL = "Wac *102# oo kushubo internet iyo ku hadal";

        public Responder(IOptions<Config> options, Repository repository, DahabAPI dahab)
        {
            _config = options.Value;
            _repository = repository;
            _dahab = dahab;
        }

        public async Task<Response> ProcessAsync(string msisdn, string text)
        {
            await Task.CompletedTask;

            string[] stringSeparators = new string[] { "*", "#" };
            string[] result = text.Split(stringSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (result.Length == 2 && result[1] == "1")
            {
                _session = new Session
                {
                    MSISDN = NormalizePhone(msisdn),
                    CurrentMenu = "0",
                    EdahabUser = await _dahab.GetUserInfo(msisdn)
                };
                if (_session.EdahabUser is null)
                {
                    return await Task.Run(() => GetResponse(_session.MSISDN, ResponseType.Notification, "This number is not register in eDahab."));
                }

                if (!_session.EdahabUser.IsBarred.Equals("No", StringComparison.OrdinalIgnoreCase) || _session.EdahabUser.Status.Equals("S", StringComparison.OrdinalIgnoreCase))
                {
                    return await Task.Run(() => GetResponse(_session.MSISDN, ResponseType.Notification, "Your mobile number is barred or suspended.\nPlease contact customer care 333 or 111"));
                }

                _session.CurrentMenu = "100";
                return GetResponse(msisdn, ResponseType.Prompt, EnterPin);
            }
            _session = _repository.GetSession(msisdn);

            if (_session is null)
            {
                return GetResponse(msisdn, ResponseType.Notification, WrongInput);
            }
            if (_session.CurrentMenu == "100")
            {
                var balanceResponse = new DahabResponse<CheckBalanceData>();
                if (_session.EdahabUser.CategoryCode.Equals("SUBS", StringComparison.OrdinalIgnoreCase))
                    balanceResponse = await _dahab.GetBalanceNew(_session.MSISDN, text);
                else
                    balanceResponse = await _dahab.GetChannelBalanceNew(_session.MSISDN, text);

                if (balanceResponse.Success)
                {
                    if (_session.MSISDN.StartsWith("65"))
                        return GetResponse(msisdn, ResponseType.Notification,
                            BalanceMsg + $"Dollar: ${balanceResponse.Data.Dollar.Balance:N2}\nShillings: Shl {balanceResponse.Data.Shl.Balance:N0}\n{AdsMessage}");
                    else
                        return GetResponse(msisdn, ResponseType.Notification,
                            BalanceMsg + $"Dollar: ${balanceResponse.Data.Dollar.Balance:N2}\n{AdsMessagePL}");
                }
                else
                {
                    return GetResponse(msisdn, ResponseType.Notification, balanceResponse.Message);
                }
            }
            else
            {
                return GetResponse(msisdn, ResponseType.Notification, WrongInput);
            }

        }

        private Response GetResponse(string msisdn, ResponseType type, string message)
        {
            if (type == ResponseType.Prompt)
            {
                _repository.SaveSession(msisdn, _session);
            }

            return new()
            {
                ResponseType = type,
                Text = message
            };
        }

        private string NormalizePhone(string input)
        {
            if (input.StartsWith("65") || input.StartsWith("66"))
            {
                return input;
            }

            if (input.StartsWith("065") || input.StartsWith("66"))
            {
                return input[1..];
            }

            return "65" + input;
        }
    }
}
