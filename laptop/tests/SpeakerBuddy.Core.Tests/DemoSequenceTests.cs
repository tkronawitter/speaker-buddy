using SpeakerBuddy.Core;

namespace SpeakerBuddy.Core.Tests;

public class DemoSequenceTests
{
    [Fact]
    public void Starts_neutral_without_deviation()
    {
        Assert.Equal(new StateLine("#000000", null), DemoSequence.At(0, ColorScheme.Default));
    }

    [Fact]
    public void Changes_every_step_and_holds_within_a_step()
    {
        Assert.Equal(DemoSequence.At(0, ColorScheme.Default), DemoSequence.At(DemoSequence.StepSeconds - 1, ColorScheme.Default));
        Assert.NotEqual(DemoSequence.At(0, ColorScheme.Default), DemoSequence.At(DemoSequence.StepSeconds, ColorScheme.Default));
    }

    [Fact]
    public void Covers_every_colour_and_both_deviation_signs()
    {
        var lines = Enumerable.Range(0, DemoSequence.Length)
            .Select(step => DemoSequence.At(step * DemoSequence.StepSeconds, ColorScheme.Default))
            .ToList();

        foreach (var color in Enum.GetValues<EvaluationColor>())
        {
            Assert.Contains(lines, l => l.Color == ColorScheme.Default.For(color));
        }

        Assert.Contains(lines, l => l.Deviation > 0);
        Assert.Contains(lines, l => l.Deviation < 0);
        Assert.Contains(lines, l => l.Deviation is null);
    }

    [Fact]
    public void Repeats_after_the_last_step()
    {
        var cycle = DemoSequence.Length * DemoSequence.StepSeconds;

        Assert.Equal(DemoSequence.At(1, ColorScheme.Default), DemoSequence.At(cycle + 1, ColorScheme.Default));
    }

    [Fact]
    public void Shows_every_colour_before_overtime_and_overtime_within_two_minutes()
    {
        Assert.InRange(DemoSequence.LengthSeconds, DemoSequence.Length * DemoSequence.StepSeconds, 120);
        Assert.True(Protocol.TryParse(Protocol.Serialize(new LengthLine(DemoSequence.LengthSeconds)), out _));
    }

    [Fact]
    public void Every_step_serializes_as_a_valid_line()
    {
        for (var step = 0; step < DemoSequence.Length; step++)
        {
            var line = Protocol.Serialize(DemoSequence.At(step * DemoSequence.StepSeconds, ColorScheme.Default));
            Assert.True(Protocol.TryParse(line, out _));
        }
    }
}
