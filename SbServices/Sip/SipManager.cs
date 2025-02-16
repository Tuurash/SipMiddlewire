using Skylar.Services.SbServices.Accounts;
using Skylar.Services.SbServices.Calls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Skylar.Services.SbServices.Sip
{
    public class SipManager : IDisposable
    {
        private int transportid;

        public delegate void AccountStateHandler(object sender, AccountStateEventArgs e);

        public delegate void CallMediaStateHandler(object sender, CallMediaStateEventArgs e);

        public delegate void CallStateHandler(object sender, CallStateEventArgs e);

        public delegate void IncomingCallHandler(object sender, IncomingCallEventArgs e);

        private readonly List<AccountManager> accounts = new List<AccountManager>();
        public static Endpoint ep = new Endpoint();

        public SipManager(Thread thread, EpConfig config = null, sbsip_transport_type_e tType = sbsip_transport_type_e.SBSIP_TRANSPORT_UDP)
        {
            if (thread == null)
                throw new ArgumentNullException("Thread cannot be null!");
            try
            {
                // Create Library
                ep.libCreate();

                // Initialize endpoint
                ep.libInit(config == null ? initEndpoint() : config);
                ep.audDevManager().setCaptureDev(0);
                ep.audDevManager().setPlaybackDev(0);

                // Create SIP transport.
                //initTransportMode(tType);

                TransportConfig tcfg = new TransportConfig();
                tcfg.port = 5060;
                ep.transportCreate(sbsip_transport_type_e.SBSIP_TRANSPORT_UDP,
                           tcfg);
                ep.transportCreate(sbsip_transport_type_e.SBSIP_TRANSPORT_TCP,
                           tcfg);

                ep.libStart();
            }
            catch (Exception exc)
            { throw exc; }

            Console.WriteLine("*** SBSUA2 STARTED ***");
            // Register Thread
            Endpoint.instance()
                .libRegisterThread(thread.Name ?? Assembly.GetEntryAssembly().FullName);
        }

        public Endpoint Endpoint
        {
            get { return ep; }
        }

        public List<AccountManager> Accounts
        {
            get { return accounts; }
        }

        public AccountManager DefaultAccount
        {
            get { return Accounts.FirstOrDefault(account => account.isDefault()); }
        }

        public Dictionary<int, CallManager> Calls
        {
            get
            {
                var calls = new Dictionary<int, CallManager>();
                return accounts.ToList()
                    .Where(acc => acc.Calls.Count > 0)
                    .Aggregate(calls,
                        (current, acc) =>
                            current.Concat(acc.Calls.ToList())
                                .GroupBy(d => d.Key)
                                .ToDictionary(d => d.Key, d => d.First().Value));
            }
        }

        //public String SipLogPath { get; private set; }
        public String CallLogPath { get; private set; }
        public CallLogManager CallLogManager { get; private set; }

        public void Dispose()
        {
            ep.hangupAllCalls();

            /* Explicitly delete the account.
               * This is to avoid GC to delete the endpoint first before deleting
               * the account.
               */
            foreach (var acc in accounts)
            {
                acc.Dispose();
            }

            // Explicitly destroy and delete endpoint
            ep.libDestroy();
            ep.Dispose();
        }

        public event AccountStateHandler AccountStateChange;

        private void NotifyAccountState(Account account, sbsip_status_code state, string reason)
        {
            // Make sure someone is listening to event
            if (AccountStateChange == null) return;
            AccountStateChange(null, new AccountStateEventArgs(account, state, reason));
        }

        public event IncomingCallHandler IncomingCall;

        private void NotifyIncomingCall(Call call)
        {
            // Make sure someone is listening to event
            if (IncomingCall == null) return;
            IncomingCall(null, new IncomingCallEventArgs(call));
        }

        public event CallStateHandler CallStateChange;

        private void NotifyCallState(CallManager call, sbsip_inv_state state)
        {
            if (call.State == sbsip_inv_state.SBSIP_INV_STATE_DISCONNECTED)
            {
                // Update Call Log

            }

            /*if(
                (call.State.In(sbsip_inv_state.SBSIP_INV_STATE_CONNECTING,sbsip_inv_state.SBSIP_INV_STATE_CONFIRMED) && call.LastState == sbsip_inv_state.SBSIP_INV_STATE_INCOMING)
                || 
                call.State == sbsip_inv_state.SBSIP_INV_STATE_CALLING)
            {
                // Hold all active (non 'spying' calls)
                holdAllCalls(false, new Dictionary<int,Call>() { {call.ID, call} } );
            }*/

            // Make sure someone is listening to event
            if (CallStateChange == null) return;
            CallStateChange(null, new CallStateEventArgs(call, state));
        }

        public event CallMediaStateHandler CallMediaStateChange;

        private void NotifyCallMediaState(CallManager call, sbsua_call_media_status state)
        {
            // Make sure someone is listening to event
            if (CallMediaStateChange == null) return;
            CallMediaStateChange(null, new CallMediaStateEventArgs(call, state));
        }

        public AccountManager addAccount(string username, string password, string host, int port = 5060)
        {
            if(accounts.Where(x=>x.Username==username).Any())
                return accounts.Where(x => x.Username == username).FirstOrDefault();
            else 
            {
                accounts.Add(initAccount(getBasicAccountconfig(username, password, host, port)));
                return accounts.LastOrDefault();
            }
        }

        public List<AccountManager> addAccount(AccountManager account)
        {
            accounts.Add(account);
            return accounts;
        }

        public List<AccountManager> removeAccount(AccountManager account)
        {
            if (accounts.Contains(account))
                accounts.Remove(account);
            return accounts;
        }

        public static EpConfig initEndpoint()
        {
            var epConfig = new EpConfig();
            //epConfig.logConfig.filename = Utils.UserAppDataPath + "sbsip.log";
            //epConfig.logConfig.consoleLevel = 6;
            //epConfig.logConfig.msgLogging = 6;
            //epConfig.logConfig.level = 6;
            epConfig.uaConfig.maxCalls = 6;
            epConfig.medConfig.sndClockRate = 16000;
            epConfig.medConfig.noVad = true;
            epConfig.medConfig.ecTailLen = 0;
            epConfig.medConfig.hasIoqueue = true;
            return epConfig;
        }

        public void initTransportMode(sbsip_transport_type_e tType)
        {
            //if (transportid >= 0)
            //    ep.transportClose(transportid);

            var sipTpConfig = new TransportConfig();
            var random = new Random();
            var randomPort = random.Next(1025, 65534);

            sipTpConfig.port = (uint)randomPort; //5060;
            transportid = ep.transportCreate(tType, sipTpConfig);
        }

        public static Accountconfig getBasicAccountconfig(string username, string password, string host, int port = 5060)
        {
            var acfg = new Accountconfig();
            acfg.idUri = "sip:" + username + "@" + host;
            acfg.regConfig.registrarUri = "sip:" + host + ":" + port;
            var cred = new AuthCredInfo("digest", "*", username, 0, password);
            acfg.sipConfig.authCreds.Add(cred);

            return acfg;
        }

        public AccountManager initAccount(Accountconfig acfg)
        {
            // Create the account
            var account = new AccountManager(acfg);

            account.IncomingCall += (sender, e) => { NotifyIncomingCall(e.Call); };
            account.AccountStateChange += (sender, e) => { NotifyAccountState(e.Account, e.State, e.Reason); };
            account.CallStateChange += (sender, e) => { NotifyCallState(e.Call, e.State); };
            account.CallMediaStateChange += (sender, e) => { NotifyCallMediaState(e.Call, e.State); };

            return account.Register();
        }

        public Call answerCallExclusive(CallManager call)
        {
            foreach (var entry in Calls)
            {
                if (entry.Key != call.ID && entry.Value.HasAudio)
                    entry.Value.hold();
            }

            return call.answer();
        }

        public Dictionary<int, CallManager> holdAllCalls(bool force = false, Dictionary<int, CallManager> excludingCalls = null)
        {
            foreach (var entry in Calls)
            {
                if (excludingCalls != null)
                    if (excludingCalls.ContainsKey(entry.Key))
                        continue;
                if (entry.Value.HasAudioOut || force)
                    entry.Value.hold();
            }

            return Calls;
        }
    }
}
