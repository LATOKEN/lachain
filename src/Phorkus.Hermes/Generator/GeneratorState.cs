namespace Phorkus.Hermes.Generator
{
    public enum GeneratorState
    {
        Initialization,
        GeneratingShare,
        CollectingShare,
        GeneratingPoint,
        CollectingPoint,
        GeneratingProof, // <---+
        CollectingProof, //     | (repeat several times)
        ValidatingProof, // ----+
        GeneratingDerivation,
        CollectingDerivation,
        GeneratingTheta,
        CollectingTheta,
        GeneratingVerification,
        CollectingVerification,
        Finalization
    }
}