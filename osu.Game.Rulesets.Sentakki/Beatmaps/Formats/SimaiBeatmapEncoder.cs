using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Game.Rulesets.Sentakki.Objects;
using osu.Game.Rulesets.Sentakki.UI;
using osuTK;
using SimaiSharp.Internal.SyntacticAnalysis;
using SimaiSharp.Structures;

namespace osu.Game.Rulesets.Sentakki.Beatmaps.Formats;

internal class MathUtils
{
    public static long gcd(long a, long b)
    {
        long Remainder;

        while (b != 0)
        {
            Remainder = a % b;
            a = b;
            b = Remainder;
        }

        return a;
    }

    public static long lcm(long a, long b)
    {
        return (long)((double)a * b / gcd(a, b));
    }
}

internal static class SimaiBeatmapEncoderExtensions
{
    // https://www.geeksforgeeks.org/convert-given-decimal-number-into-an-irreducible-fraction/
    public static (long numerator, long denominator) AsIntegerRatio(
        this double number,
        long precision = 6983776800
    )
    {
        double integral = Math.Floor(number);
        double fractional = number - integral;
        long gcdVal = MathUtils.gcd((long)Math.Round(fractional * precision), precision);
        long numerator = (long)Math.Round(fractional * precision) / gcdVal;
        long denominator = precision / gcdVal;
        return (numerator: (long)(integral * denominator) + numerator, denominator);
    }
}

public class SimaiBeatmapEncoder
{
    internal static Note EncodeTap(Tap hitObject, NoteCollection parent)
    {
        return new(parent)
        {
            location = new(hitObject.Lane, NoteGroup.Tap),
            styles = hitObject.Ex ? NoteStyles.Ex : 0,
            type = hitObject.Break ? NoteType.Break : NoteType.Tap,
        };
    }

    internal static Note EncodeHold(Hold hitObject, NoteCollection parent)
    {
        return new(parent)
        {
            location = new(hitObject.Lane, NoteGroup.Tap),
            styles = hitObject.Ex ? NoteStyles.Ex : 0,
            type = hitObject.Break ? NoteType.Break : NoteType.Hold,
            length = (float)hitObject.Duration / 1000,
        };
    }

    internal static SlideType SlideTypeOfPart(SlideBodyPart part)
    {
        switch (part.Shape)
        {
            case SlidePaths.PathShapes.Straight:
                return SlideType.StraightLine;
            case SlidePaths.PathShapes.Circle:
                return part.Mirrored ? SlideType.RingCcw : SlideType.RingCw;
            case SlidePaths.PathShapes.V:
                return SlideType.Fold;
            case SlidePaths.PathShapes.U:
                return part.Mirrored ? SlideType.CurveCw : SlideType.CurveCcw;
            case SlidePaths.PathShapes.Cup:
                return part.Mirrored ? SlideType.EdgeCurveCw : SlideType.EdgeCurveCcw;
            case SlidePaths.PathShapes.Thunder:
                return part.Mirrored ? SlideType.ZigZagZ : SlideType.ZigZagS;
            case SlidePaths.PathShapes.Fan:
                return SlideType.Fan;
            default:
                throw new UnreachableException();
        }
    }

    internal static Note EncodeSlide(Slide hitObject, NoteCollection parent)
    {
        Note note = new(parent)
        {
            location = new(hitObject.Lane, NoteGroup.Tap),
            styles = hitObject.Ex ? NoteStyles.Ex : 0,
            length = (float)hitObject.Duration / 1000,
        };
        switch (hitObject.TapType)
        {
            case Slide.TapTypeEnum.Star:
                note.appearance = NoteAppearance.Default;
                note.type = NoteType.Slide;
                break;
            case Slide.TapTypeEnum.Tap:
                note.appearance = NoteAppearance.ForceNormal;
                note.type = NoteType.Slide;
                break;
            case Slide.TapTypeEnum.None:
                note.appearance = NoteAppearance.Default;
                note.type = NoteType.ForceInvalidate;
                break;
        }
        note.slidePaths = hitObject
            .SlideBodies.Select(body =>
            {
                int partStartLane = hitObject.Lane;
                List<SlideSegment> segments = body
                    .SlideBodyInfo.SlidePathParts.Select(part =>
                    {
                        int partEndLane = partStartLane + part.EndOffset;
                        return new SlideSegment([new(partEndLane, NoteGroup.Tap)])
                        {
                            slideType = SlideTypeOfPart(part),
                        };
                    })
                    .ToList();
                SlidePath path = new(segments);
                return path;
            })
            .ToList();
        return note;
    }

    internal static Dictionary<Vector2, Location> PositionsToLocations = SentakkiPlayfield
        .LANEANGLES.SelectMany<float, KeyValuePair<Vector2, Location>>(
            (angle, index) =>

                [
                    new(
                        SentakkiExtensions.GetCircularPosition(130, angle),
                        new(index, NoteGroup.BSensor)
                    ),
                    new(
                        SentakkiExtensions.GetCircularPosition(190, angle - 22.5f),
                        new(index, NoteGroup.ESensor)
                    ),
                    new(
                        SentakkiExtensions.GetCircularPosition(270, angle),
                        new(index, NoteGroup.ASensor)
                    ),
                    new(
                        SentakkiExtensions.GetCircularPosition(270, angle - 22.5f),
                        new(index, NoteGroup.DSensor)
                    ),
                ]
        )
        .Append(new(new(0, 0), new(0, NoteGroup.CSensor)))
        .ToDictionary();

    internal static Note EncodeTouch(Touch hitObject, NoteCollection parent)
    {
        return new(parent)
        {
            location = PositionsToLocations
                .MinBy(kv =>
                {
                    double xDelta = Math.Abs(kv.Key.X - hitObject.Position.X);
                    double yDelta = Math.Abs(kv.Key.Y - hitObject.Position.Y);
                    return Math.Sqrt(xDelta * xDelta + yDelta * yDelta);
                })
                .Value,
            styles = hitObject.Ex ? NoteStyles.Ex : 0,
            type = NoteType.Touch,
        };
    }

    internal static Note EncodeTouchHold(TouchHold hitObject, NoteCollection parent)
    {
        return new(parent)
        {
            location = new(0, NoteGroup.CSensor),
            styles = hitObject.Ex ? NoteStyles.Ex : 0,
            type = NoteType.Touch,
            length = (float)hitObject.Duration / 1000,
        };
    }

    internal static Note EncodeHitObject(SentakkiHitObject hitObject, NoteCollection parent)
    {
        switch (hitObject)
        {
            case Tap tap:
                return EncodeTap(tap, parent);
            case Hold hold:
                return EncodeHold(hold, parent);
            case Slide slide:
                return EncodeSlide(slide, parent);
            case Touch touch:
                return EncodeTouch(touch, parent);
            case TouchHold touchhold:
                return EncodeTouchHold(touchhold, parent);
            default:
                throw new NotImplementedException("Hit object type not supported");
        }
    }

    public static MaiChart EncodeBeatmap(SentakkiBeatmap beatmap)
    {
        float end = (float)beatmap.BeatmapInfo.Length / 1000;
        var collections = beatmap
            .HitObjects.GroupBy(hitObject => hitObject.StartTime)
            .Select(group =>
                group.Aggregate(
                    new NoteCollection((float)group.Key / 1000),
                    (collection, hitObject) =>
                    {
                        var note = EncodeHitObject(hitObject, collection);
                        collection.AddNote(ref note);
                        return collection;
                    }
                )
            )
            .ToArray();
        var timingChanges = collections
            .GroupBy(collection => beatmap.ControlPointInfo.TimingPointAt(collection.time * 1000))
            .Select(group => new TimingChange()
            {
                time = (float)(group.Key.Time / 1000),
                tempo = (float)group.Key.BPM,
                subdivisions = group
                    .Zip(group.Skip(1))
                    .Select(pair =>
                        ((pair.Second.time - pair.First.time) / (group.Key.BeatLength / 1000) / 4)
                            .AsIntegerRatio(5040)
                            .denominator
                    )
                    .Aggregate(1L, MathUtils.lcm),
            })
            .ToArray();
        return new()
        {
            FinishTiming = end,
            NoteCollections = collections,
            TimingChanges = timingChanges,
        };
    }
}
