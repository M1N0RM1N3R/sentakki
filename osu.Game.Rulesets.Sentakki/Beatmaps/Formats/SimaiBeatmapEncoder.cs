using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Game.Rulesets.Sentakki.Objects;
using osu.Game.Rulesets.Sentakki.UI;
using osuTK;
using SimaiSharp.Internal.SyntacticAnalysis;
using SimaiSharp.Structures;

namespace osu.Game.Rulesets.Sentakki.Beatmaps.Formats;

public class SimaiBeatmapEncoder
{
    internal static Note EncodeTap(Tap hitObject, NoteCollection parent)
    {
        Note note = new(parent);
        note.location = new(hitObject.Lane, NoteGroup.Tap);
        note.styles = (hitObject.Ex ? NoteStyles.Ex : 0);
        note.type = hitObject.Break ? NoteType.Break : NoteType.Tap;
        return note;
    }

    internal static Note EncodeHold(Hold hitObject, NoteCollection parent)
    {
        Note note = new(parent);
        note.location = new(hitObject.Lane, NoteGroup.Tap);
        note.styles = (hitObject.Ex ? NoteStyles.Ex : 0);
        note.type = hitObject.Break ? NoteType.Break : NoteType.Hold;
        note.length = ((float)hitObject.Duration);
        return note;
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
        Note note = new(parent);
        note.location = new(hitObject.Lane, NoteGroup.Tap);
        note.styles = (hitObject.Ex ? NoteStyles.Ex : 0);
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
                        SlideSegment segment = new([new(partEndLane, NoteGroup.Tap)]);
                        segment.slideType = SlideTypeOfPart(part);
                        return segment;
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
        Note note = new(parent);
        note.location = PositionsToLocations
            .MinBy(kv =>
            {
                var xDelta = Math.Abs(kv.Key.X - hitObject.Position.X);
                var yDelta = Math.Abs(kv.Key.Y - hitObject.Position.Y);
                return Math.Sqrt(xDelta * xDelta + yDelta * yDelta);
            })
            .Value;
        note.styles = (hitObject.Ex ? NoteStyles.Ex : 0);
        note.type = NoteType.Touch;
        return note;
    }

    internal static Note EncodeTouchHold(TouchHold hitObject, NoteCollection parent)
    {
        Note note = new(parent);
        note.location = new(0, NoteGroup.CSensor);
        note.styles = (hitObject.Ex ? NoteStyles.Ex : 0);
        note.type = NoteType.Touch;
        note.length = ((float)hitObject.Duration);
        return note;
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
        MaiChart chart = new();

        beatmap
            .HitObjects.GroupBy(o => o.StartTime)
            .Select(group =>
                group.Aggregate(
                    new NoteCollection(((float)group.Key)),
                    (collection, hitObject) =>
                    {
                        var note = EncodeHitObject(hitObject, collection);
                        collection.AddNote(ref note);
                        return collection;
                    }
                )
            )
            .Select((collection, index) => new { collection, index })
            .ForEach(e => chart.NoteCollections[e.index] = e.collection);

        var timingPoints = beatmap.ControlPointInfo.TimingPoints;
        timingPoints
            .Where((p, i) => i == 0 ? true : !p.IsRedundant(timingPoints[i - 1]))
            .Select(
                (point, index) =>
                {
                    TimingChange timingChange = new();
                    timingChange.time = ((float)point.Time);
                    timingChange.tempo = ((float)point.BPM);
                    timingChange.subdivisions = 4 * 12 * 16; // Lazy, I know.
                    return new { timingChange, index };
                }
            )
            .ForEach(e => chart.TimingChanges[e.index] = e.timingChange);

        return chart;
    }
}
