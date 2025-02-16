using Skylar.Services.SbServices.Calls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skylar.Services.SbServices.Accounts
{

    public class AccountStateEventArgs : EventArgs
    {
        public AccountStateEventArgs(Account account, sbsip_status_code state) : this(account, state, "")
        {
        }

        public AccountStateEventArgs(Account account, sbsip_status_code state, String reason)
        {
            Account = account;
            State = state;
            Reason = reason;
        }

        public Account Account { get; private set; }
        public sbsip_status_code State { get; private set; }
        public String Reason { get; private set; }
    }

    public class Accountconfig : AccountConfig
    {
        public String DisplayName { get; set; }
    }

    public class AccountManager : Account
    {
        public delegate void AccountStateHandler(object sender, AccountStateEventArgs e);

        public delegate void CallMediaStateHandler(object sender, CallMediaStateEventArgs e);

        public delegate void CallStateHandler(object sender, CallStateEventArgs e);

        public delegate void IncomingCallHandler(object sender, IncomingCallEventArgs e);

        private readonly Accountconfig acfg;
        private readonly Dictionary<int, CallManager> calls = new Dictionary<int, CallManager>();

        public AccountManager(Accountconfig acfg)
        {
            DND = false;
            this.acfg = acfg;
        }

        public AccountManager()
        {
        }

        public String DisplayName
        {
            get { return acfg.DisplayName; }
            private set { acfg.DisplayName = value; }
        }

        public String Username
        {
            get { return acfg.sipConfig.authCreds[0].username; }
        }

        public Boolean DND { get; set; }

        public String RegURI
        {
            get { return acfg.regConfig.registrarUri; }
        }

        public Dictionary<int, CallManager> Calls
        {
            get { return calls; }
        }

        public AccountManager Register()
        {
            create(acfg);
            return this;
        }

        public override void onRegState(OnRegStateParam prm)
        {
            base.onRegState(prm);
            //Console.WriteLine("Account registration state: " + prm.code + " : " + prm.reason);
            NotifyAccountState(prm.code, prm.reason);
        }

        public event AccountStateHandler AccountStateChange;

        private void NotifyAccountState(sbsip_status_code state, string reason)
        {
            // Make sure someone is listening to event
            if (AccountStateChange == null) return;
            AccountStateChange(null, new AccountStateEventArgs(this, state, reason));
        }

        public event IncomingCallHandler IncomingCall;

        private void NotifyIncomingCall(CallManager call)
        {
            if (!calls.ContainsKey(call.ID))
                calls.Add(call.ID, call);

            // Make sure someone is listening to event
            if (IncomingCall == null) return;
            IncomingCall(null, new IncomingCallEventArgs(call));
        }

        public event CallStateHandler CallStateChange;

        private void NotifyCallState(CallManager call, sbsip_inv_state state)
        {
            if (!calls.ContainsKey(call.ID))
                calls.Add(call.ID, call);
            if (state == sbsip_inv_state.SBSIP_INV_STATE_DISCONNECTED)
                calls.Remove(call.ID);

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

        public override void onIncomingCall(OnIncomingCallParam iprm)
        {
            base.onIncomingCall(iprm);

            var call = new CallManager(this, iprm.callId, CallManager.Type.INBOUND);

            // If Do not disturb
            if (DND)
            {
                // hangup;
                var op = new CallOpParam(true);
                op.statusCode = sbsip_status_code.SBSIP_SC_DECLINE;
                call.decline();

                // And delete the call
                //call.Dispose();
                return;
            }

            // Hook into call state
            hookCall(call);

            // Notify stack of the call
            NotifyIncomingCall(call);
        }

        private void hookCall(CallManager call)
        {
            call.CallStateChange += (sender, e) => { NotifyCallState(e.Call, e.State); };
            call.CallMediaStateChange += (sender, e) => { NotifyCallMediaState(e.Call, e.State); };
        }

        public CallManager makeCall(string number)
        {
            var call = new CallManager(this);

            // Hook into call state
            hookCall(call);

            return call.makeCall(number);
        }
    }
}
