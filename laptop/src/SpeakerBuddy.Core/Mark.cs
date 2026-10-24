namespace SpeakerBuddy.Core;

/// <summary>
/// A time mark from the speaker notes: the slide carrying a <c>## m:ss</c> line and the target
/// time, in seconds from the start of the talk, by which that slide should be reached.
/// </summary>
/// <param name="SlideIndex">1-based slide index in the deck, as PowerPoint reports it.</param>
/// <param name="Seconds">Target time in seconds since the talk started.</param>
public sealed record Mark(int SlideIndex, int Seconds);
