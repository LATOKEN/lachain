using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Lachain.Crypto.VRF
{
    internal static class VrfImports
    {
        private const string Libvrf = "vrf.so";

        [DllImport(Libvrf)]
        internal static extern string evaluate(string privateKey, string msg);

        [DllImport(Libvrf)]
        internal static extern bool verify(string publicKey, string proof, string msg);

        [DllImport(Libvrf)]
        internal static extern string proof_to_hash(string proof);
    }
}