using UnityEngine;
using Nep;
using Newtonsoft.Json;
using Cysharp.Threading.Tasks;

namespace ROSNEPConnector
{  
    public class NepClient<REQ, RES> where RES : class
    {
        private NepPublisher _reqPub;
        private NepCallback _resCallback;
        private RES _nep_res;

        public NepClient(string req_name, string res_name)
        {
            _reqPub = NepGlobal.Node.NewPub(req_name, "json", NepGlobal.Conf);
            _resCallback = NepGlobal.Node.NewCallback(res_name, ResCallback, NepGlobal.IP);
            _nep_res = null;

            _resCallback.Start();
            Debug.Log("*** Service client[" + req_name + " => " + res_name + "] started");
        }

        public async UniTask<RES> CallAsync(REQ nep_req)
        {
            _nep_res = null;
            _reqPub.Publish(nep_req);

            await UniTask.WaitUntil(() => _nep_res != null);

            return _nep_res;
        }

        private void ResCallback(string message)
        {
            _nep_res = JsonConvert.DeserializeObject<RES>(message);
        }
    }
}
