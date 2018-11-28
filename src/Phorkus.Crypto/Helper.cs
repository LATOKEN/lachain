using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace Phorkus.Crypto
{
    public static class Helper
    {
        /// <summary>
        /// Hash256 Password
        /// </summary>
        /// <param name="password">Password</param>
        /// <returns>byte array hash256</returns>
        public static byte[] ToAesKey(string password)
        {
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            /* TODO: "not secure password hashing" */
            var passwordHash = passwordBytes.Sha256().Sha256();
            Array.Clear(passwordBytes, 0, passwordBytes.Length);
            return passwordHash;
        }

        /// <summary>
        /// Hash256 Password
        /// </summary>
        /// <param name="password">Password</param>
        /// <returns>byte array hash256</returns>
        public static byte[] ToAesKey(SecureString password)
        {
            if (password == null)
                throw new ArgumentNullException(nameof(password));
            var unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(password);
                var str = Marshal.PtrToStringUni(unmanagedString);
                if (str == null)
                    throw new Exception("Unable to unmarshall secure string");
                return ToAesKey(str);
            }
            finally
            {
                if (unmanagedString != IntPtr.Zero)
                    Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }
    }
}