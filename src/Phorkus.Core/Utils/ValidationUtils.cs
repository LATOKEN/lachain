using Phorkus.Proto;

namespace Phorkus.Core.Utils
{
    public static class ValidationUtils
    {
        public static bool IsValid(this Node node)
        {
            if (string.IsNullOrEmpty(node.Address))
                return false;
            if (node.Port == 0)
                return false;
            if (string.IsNullOrEmpty(node.Agent))
                return false;
            return node.Nonce != 0;
        }
    }
}