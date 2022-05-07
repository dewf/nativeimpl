using System;
using System.Collections.Generic;

namespace TestLibraryClient.Support
{
    internal delegate void FuncValWrapper();
    
    internal class ClientFuncVal : ClientResource
    {
        private static readonly Dictionary<IntPtr, int> ReverseFuncValDict = new ();
        public readonly FuncValWrapper Wrapper;
        private readonly IntPtr _key;
        
        private ClientFuncVal(FuncValWrapper wrapper, IntPtr key)
        {
            Wrapper = wrapper;
            _key = key;
        }
        
        protected override void NativePush()
        {
            NativeMethods.pushClientFunc(Id);
        }
        
        protected override void ReleaseExtra()
        {
            ReverseFuncValDict.Remove(_key);
        }
        
        public static ClientFuncVal AddFuncVal(FuncValWrapper wrapper, IntPtr uniqueKey)
        {
            Console.WriteLine($"Func Val Unique Key: {uniqueKey}");
            if (ReverseFuncValDict.ContainsKey(uniqueKey))
            {
                // return existing
                var id = ReverseFuncValDict[uniqueKey];
                return (ClientFuncVal)GetResourceById(id);
            }
            // else create anew
            var clientFunc = new ClientFuncVal(wrapper, uniqueKey);
            ReverseFuncValDict[uniqueKey] = clientFunc.Id;
            return clientFunc;
        }
    }
}
