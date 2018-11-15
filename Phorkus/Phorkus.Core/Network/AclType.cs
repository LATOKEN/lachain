namespace Phorkus.Core.Network
{
    public enum AclType
    {
        /// <summary>
        /// None Acl
        /// </summary>
        None,
        /// <summary>
        /// If match deny
        /// </summary>
        Whitelist,
        /// <summary>
        /// If match allow
        /// </summary>
        Blacklist
    }
}