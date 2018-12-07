using System;
using Org.BouncyCastle.Math;

namespace Phorkus.Hermes.Generator.State
{
    /**
     * The structure used by the parties to exchange their shares.
     * @author Christian Mouchet
     */
    public class BgwPublicParams
    {
        /** The id of the party for which these shares were generated*/
        public int j;

        /** The share pij = f(j) of party i's pi generated for party j*/
        public BigInteger pij;

        /** The share qij = g(j) of party i's qi generated for party j*/
        public BigInteger qij;

        /** The share hij = h(j) of party i's zero generated for party j*/
        public BigInteger hij;

        /** The id of the party. i &in; [1;N]*/
        public int ii;

        /** the number of parties*/
        public int nn;

        internal BgwPublicParams(int i, int j, int n, BigInteger pj, BigInteger qj, BigInteger hj)
        {
            ii = i;
            nn = n;

            pij = pj;
            qij = qj;
            hij = hj;
            this.j = j;
        }

        /** Checks the shares for correctness. Not implemented yet.
         * @param protocolParameters the security parameters of the protocol
         * @return true if the share could be verified, false otherwise
         */
        public bool isCorrect(ProtocolParameters protocolParameters)
        {
            // Not implemented yet
            return true;
        }

        /** Generates the shares for a given party j.
         * @param j the id of the party for which we want to generate the share
         * @param bgwPrivParam the private parameters to use
         * @return a structure containing the shares generated to party j
         */
        public static BgwPublicParams genFor(int j, BgwPrivateParams bgwPrivParam)
        {
            if (j < 1 || j > bgwPrivParam.n)
                throw new ArgumentException("j must be between 1 and the number of parties");

            var i = bgwPrivParam.i;
            var pj = bgwPrivParam.fi.eval(j);
            var qj = bgwPrivParam.gi.eval(j);
            var hj = bgwPrivParam.hi.eval(j);
            return new BgwPublicParams(i, j, bgwPrivParam.n, pj, qj, hj);
        }

        public string ToString()
        {
            return $"BGWPublicParameters[{ii}][{j}]";
        }
    }
}