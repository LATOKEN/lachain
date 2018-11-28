namespace Phorkus.VM
{
    public class GasCost
    {
        /* backwards compatibility, remove eventually */
        public const int Step = 1;

        public const int Sstore = 300;
        /* backwards compatibility, remove eventually */

        public const int Zerostep = 0;
        public const int Quickstep = 2;
        public const int Fasteststep = 3;
        public const int Faststep = 5;
        public const int Midstep = 8;
        public const int Slowstep = 10;
        public const int Extstep = 20;

        public const int Genesisgaslimit = 1000000;
        public const int Mingaslimit = 125000;

        public const int Balance = 20;
        public const int Sha3 = 30;
        public const int Sha3Word = 6;
        public const int Sload = 50;
        public const int Stop = 0;
        public const int Suicide = 0;
        public const int ClearSstore = 5000;
        public const int SetSstore = 20000;
        public const int ResetSstore = 5000;
        public const int RefundSstore = 15000;
        public const int Create = 32000;

        public const int Jumpdest = 1;
        public const int CreateDataByte = 5;
        public const int Call = 40;
        public const int StipendCall = 2300;
        public const int VtCall = 9000; //value transfer call
        public const int NewAcctCall = 25000; //new account call
        public const int Memory = 3;
        public const int SuicideRefund = 24000;
        public const int QuadCoeffDiv = 512;
        public const int CreateData = 200;
        public const int TxNoZeroData = 68;
        public const int TxZeroData = 4;
        public const int Transaction = 21000;
        public const int TransactionCreateContract = 53000;
        public const int LogGas = 375;
        public const int LogDataGas = 8;
        public const int LogTopicGas = 375;
        public const int CopyGas = 3;
        public const int ExpGas = 10;
        public const int ExpByteGas = 10;
        public const int Identity = 15;
        public const int IdentityWord = 3;
        public const int Ripemd160 = 600;
        public const int Ripemd160Word = 120;
        public const int Sha256 = 60;
        public const int Sha256Word = 12;
        public const int EcRecover = 3000;
    }
}