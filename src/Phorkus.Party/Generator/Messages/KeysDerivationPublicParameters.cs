using System.IO;
using Org.BouncyCastle.Math;
using Phorkus.Party.Generator.State;

namespace Phorkus.Party.Generator.Messages
{
    /**
     * The structure used by the parties to exchange their shares in the key derivation
     * @author Christian Mouchet
     */
    public class KeysDerivationPublicParameters{
        private int i;
        private int j;
        public BigInteger betaij;
        public BigInteger DRij;
        public BigInteger Phiij;
        public BigInteger hij;
			
        internal KeysDerivationPublicParameters(int i, int j, BigInteger betaij, BigInteger Rij, BigInteger Phiij, BigInteger hij)
        {
            this.i = i;
            this.j = j;
            this.betaij = betaij;
            this.DRij = Rij;
            this.Phiij = Phiij;
            this.hij = hij;
        }
			
        /** Generates the structure containing the shares for party j
         * @param j the id of the party for which we want to generate the shares
         * @param keysDerivationPrivateParameters the private parameters to use
         * @return the structure containing the shares for party j
         */
        public static KeysDerivationPublicParameters genFor(int j, KeysDerivationPrivateParameters keysDerivationPrivateParameters) {
            BigInteger Betaij = keysDerivationPrivateParameters.betaiSharing.eval(j);
            BigInteger DRij = keysDerivationPrivateParameters.DRiSharing.eval(j);
            BigInteger Phiij = keysDerivationPrivateParameters.PhiSharing.eval(j);
            BigInteger hij = keysDerivationPrivateParameters.zeroSharing.eval(j);
            return new KeysDerivationPublicParameters(keysDerivationPrivateParameters.i, j, Betaij, DRij, Phiij, hij);
        }
        
        public KeysDerivationPublicParameters(byte[] buffer)
        {
            using (var stream = new MemoryStream(buffer))
            using (var reader = new BinaryReader(stream))
            {
                
                var betaijLength = reader.ReadInt32();
                var DRijLength = reader.ReadInt32();
                var PhiijLength  = reader.ReadInt32();
                var hijLength =reader.ReadInt32(); 
                
                betaij = new BigInteger(reader.ReadBytes(betaijLength));
                DRij = new BigInteger(reader.ReadBytes(DRijLength));
                Phiij = new BigInteger(reader.ReadBytes(PhiijLength));
                hij = new BigInteger(reader.ReadBytes(hijLength));
                
                i = reader.ReadInt32();
                j = reader.ReadInt32();
                
                
            }
        }
        
        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                var betaijArray = betaij.ToByteArray();
                writer.Write(betaijArray.Length);
                writer.Write(betaijArray);
                
                var DRijArray = DRij.ToByteArray();
                writer.Write(DRijArray.Length);
                writer.Write(DRijArray);
                
                var PhiijArray = Phiij.ToByteArray();
                writer.Write(PhiijArray.Length);
                writer.Write(PhiijArray);
                
                var hijArray = hij.ToByteArray();
                writer.Write(hijArray.Length);
                writer.Write(hijArray);

                writer.Write(i);
                writer.Write(j);
                
                return stream.ToArray();
            }
        }
    }
}
