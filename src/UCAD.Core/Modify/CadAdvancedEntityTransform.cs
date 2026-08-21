using UCAD.Core.Entities;
using UCAD.Core.Geometry;

namespace UCAD.Core.Modify;

/// <summary>
/// Metadata-preserving v0.11 transform facade. It delegates ordinary entities to the
/// established transform engine while explicitly carrying Hatch islands/association and
/// BlockReference attribute dictionaries that did not exist in the v0.7 constructors.
/// </summary>
public static class CadAdvancedEntityTransform
{
    public static ICadEntity Translate(ICadEntity entity, CadVector displacement, bool preserveIdentity = true) => entity switch
    {
        HatchEntity hatch => TransformHatch(
            hatch,
            point => new CadPoint(point.X + displacement.X, point.Y + displacement.Y),
            patternScale: hatch.PatternScale,
            patternAngleRadians: hatch.PatternAngleRadians,
            preserveIdentity),
        BlockReferenceEntity block => TransformBlock(
            block,
            child => Translate(child, displacement, preserveIdentity),
            new CadPoint(block.InsertionPoint.X + displacement.X, block.InsertionPoint.Y + displacement.Y),
            block.Scale,
            block.RotationRadians,
            preserveIdentity),
        _ => CadEntityTransform.Translate(entity, displacement, preserveIdentity)
    };

    public static ICadEntity Rotate(ICadEntity entity, CadPoint basePoint, double angleRadians, bool preserveIdentity = true) => entity switch
    {
        HatchEntity hatch => TransformHatch(
            hatch,
            point => CadEntityTransform.RotatePoint(point, basePoint, angleRadians),
            hatch.PatternScale,
            hatch.PatternAngleRadians + angleRadians,
            preserveIdentity),
        BlockReferenceEntity block => TransformBlock(
            block,
            child => Rotate(child, basePoint, angleRadians, preserveIdentity),
            CadEntityTransform.RotatePoint(block.InsertionPoint, basePoint, angleRadians),
            block.Scale,
            block.RotationRadians + angleRadians,
            preserveIdentity),
        _ => CadEntityTransform.Rotate(entity, basePoint, angleRadians, preserveIdentity)
    };

    public static ICadEntity Scale(ICadEntity entity, CadPoint basePoint, double factor, bool preserveIdentity = true) => entity switch
    {
        HatchEntity hatch => TransformHatch(
            hatch,
            point => CadEntityTransform.ScalePoint(point, basePoint, factor),
            hatch.PatternScale * factor,
            hatch.PatternAngleRadians,
            preserveIdentity),
        BlockReferenceEntity block => TransformBlock(
            block,
            child => Scale(child, basePoint, factor, preserveIdentity),
            CadEntityTransform.ScalePoint(block.InsertionPoint, basePoint, factor),
            block.Scale * factor,
            block.RotationRadians,
            preserveIdentity),
        _ => CadEntityTransform.Scale(entity, basePoint, factor, preserveIdentity)
    };

    public static ICadEntity Mirror(ICadEntity entity, CadPoint firstAxisPoint, CadPoint secondAxisPoint, bool preserveIdentity = true) => entity switch
    {
        HatchEntity hatch => TransformHatch(
            hatch,
            point => CadEntityTransform.MirrorPoint(point, firstAxisPoint, secondAxisPoint),
            hatch.PatternScale,
            MirrorAngle(hatch.PatternAngleRadians, firstAxisPoint, secondAxisPoint),
            preserveIdentity),
        BlockReferenceEntity block => TransformBlock(
            block,
            child => Mirror(child, firstAxisPoint, secondAxisPoint, preserveIdentity),
            CadEntityTransform.MirrorPoint(block.InsertionPoint, firstAxisPoint, secondAxisPoint),
            block.Scale,
            MirrorAngle(block.RotationRadians, firstAxisPoint, secondAxisPoint),
            preserveIdentity),
        _ => CadEntityTransform.Mirror(entity, firstAxisPoint, secondAxisPoint, preserveIdentity)
    };

    private static HatchEntity TransformHatch(
        HatchEntity hatch,
        Func<CadPoint, CadPoint> pointTransform,
        double patternScale,
        double patternAngleRadians,
        bool preserveIdentity)
    {
        var boundary = hatch.Boundary.Select(pointTransform).ToArray();
        var islands = hatch.Islands.Select(loop => (IEnumerable<CadPoint>)loop.Select(pointTransform).ToArray()).ToArray();
        return new HatchEntity(
            boundary,
            hatch.Pattern,
            patternScale,
            patternAngleRadians,
            islands,
            hatch.Associative,
            hatch.SourceEntityIds,
            hatch.IslandDetection,
            preserveIdentity ? hatch.Id : Guid.NewGuid());
    }

    private static BlockReferenceEntity TransformBlock(
        BlockReferenceEntity block,
        Func<ICadEntity, ICadEntity> childTransform,
        CadPoint insertionPoint,
        double scale,
        double rotationRadians,
        bool preserveIdentity)
    {
        var children = block.Contents.Select(childTransform).ToArray();
        return new BlockReferenceEntity(
            block.DefinitionName,
            insertionPoint,
            children,
            scale,
            rotationRadians,
            block.AttributeValues,
            preserveIdentity ? block.Id : Guid.NewGuid());
    }

    private static double MirrorAngle(double angleRadians, CadPoint firstAxisPoint, CadPoint secondAxisPoint)
    {
        var origin = new CadPoint(0, 0);
        var direction = new CadPoint(Math.Cos(angleRadians), Math.Sin(angleRadians));
        var mirroredOrigin = CadEntityTransform.MirrorPoint(origin, firstAxisPoint, secondAxisPoint);
        var mirroredDirection = CadEntityTransform.MirrorPoint(direction, firstAxisPoint, secondAxisPoint);
        var vector = mirroredDirection - mirroredOrigin;
        return Math.Atan2(vector.Y, vector.X);
    }
}