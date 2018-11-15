namespace Phorkus.Core.Network
{
    public class AclConfig
    {
        /// <summary>
        /// Acl behaviour
        /// </summary>
        public AclType Type { get; set; } = AclType.None;

        /// <summary>
        /// Path of rules file
        /// </summary>
        public string Path { get; set; }
    }
}