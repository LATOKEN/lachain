using Org.BouncyCastle.Math;
using Phorkus.Hermes.Generator.State;

namespace Phorkus.Hermes.Generator.Messages
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
    }
}
