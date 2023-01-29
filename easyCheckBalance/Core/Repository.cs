//  ---------------------------------------------------------------------------------
//  Copyright © eDahab. All rights reserved.
//  ---------------------------------------------------------------------------------
using System.Collections.Concurrent;
namespace easyCheckBalance.Core
{
    public class Repository
    {
        private static readonly ConcurrentDictionary<string, Session> Sessions = new();

        // Holds connection strings and other settings.
        private readonly IConfiguration _config;

        public Repository(IConfiguration config)
        {
            _config= config;
        }

        public void SaveSession(string msisdn, Session session)
        {
            Sessions.AddOrUpdate(msisdn, session, (oldKey, oldValue) => session);
        }

        public Session GetSession(string msisdn)
        {
            Sessions.TryRemove(msisdn, out var session);
            return session;
        }
    }
}