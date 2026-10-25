using System.Runtime.InteropServices;

namespace SpeakerBuddy.App.Tests;

public class ComScopeTests
{
    [Fact]
    public void Values_that_are_not_com_objects_pass_through_untracked()
    {
        using var com = new ComScope();

        Assert.Equal("deck.pptx", com.Track("deck.pptx"));
        Assert.Equal(3, com.Track(3));
        Assert.Null(com.Track<object?>(null));
        Assert.Equal(0, com.Count);
    }

    [Fact]
    public void A_com_object_is_released_when_the_scope_ends()
    {
        // Scripting.Dictionary (scrrun.dll) ships with every Windows, so the test needs no Office
        var type = Type.GetTypeFromProgID("Scripting.Dictionary", throwOnError: true)!;
        dynamic dictionary;
        using (var com = new ComScope())
        {
            dictionary = com.Track(Activator.CreateInstance(type)!);
            dictionary.Add("slide", 1);
            Assert.Equal(1, com.Count);
        }

        Assert.Throws<InvalidComObjectException>(() => (int)dictionary.Count);
    }
}
