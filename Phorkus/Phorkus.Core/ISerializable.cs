using System.IO;

namespace Phorkus.Core
{
    public interface ISerializable
    {
        int Serialize(BinaryWriter binaryWriter);

        int Deserialize(BinaryReader binaryReader);
    }
}