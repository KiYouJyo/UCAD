using UCAD.Core.Entities;
using UCAD.Core.Geometry;
using UCAD.Core.Modify;

namespace UCAD.Core.Blocks;

public static class CadBlockFactory
{
    private static readonly CadPoint Origin = new(0, 0);

    public static BlockReferenceEntity CreateReference(
        CadBlockDefinition definition,
        CadPoint insertionPoint,
        double scale = 1,
        double rotationRadians = 0)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!double.IsFinite(scale) || scale <= 1e-9) throw new ArgumentOutOfRangeException(nameof(scale));
        if (!double.IsFinite(rotationRadians)) throw new ArgumentOutOfRangeException(nameof(rotationRadians));

        var toOrigin = new CadVector(-definition.BasePoint.X, -definition.BasePoint.Y);
        var toInsertion = new CadVector(insertionPoint.X, insertionPoint.Y);
        var contents = definition.Entities.Select(entity =>
        {
            var local = CadEntityTransform.Translate(entity, toOrigin, preserveIdentity: false);
            var scaled = CadEntityTransform.Scale(local, Origin, scale);
            var rotated = CadEntityTransform.Rotate(scaled, Origin, rotationRadians);
            return CadEntityTransform.Translate(rotated, toInsertion);
        }).ToArray();

        return new BlockReferenceEntity(definition.Name, insertionPoint, contents, scale, rotationRadians);
    }

    public static IReadOnlyList<ICadEntity> Explode(BlockReferenceEntity reference) =>
        reference.Contents
            .Select(entity => CadEntityTransform.Translate(entity, new CadVector(0, 0), preserveIdentity: false))
            .ToArray();
}
