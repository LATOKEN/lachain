
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
        CollectingProof, //     | (repeat 10 times)
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