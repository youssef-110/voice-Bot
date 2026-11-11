using VoiceBot;
using Xunit;

public class BrainTests
{
    private static Brain At(int hour) => new(() => new DateTime(2026, 11, 11, hour, 5, 0), seed: 1);

    [Theory]
    [InlineData(9, "Good morning")]
    [InlineData(14, "Good afternoon")]
    [InlineData(21, "Good evening")]
    public void Greets_by_time_of_day(int hour, string expected) =>
        Assert.StartsWith(expected, At(hour).Respond("Hello!").Text);

    [Fact]
    public void Tells_time_and_date()
    {
        var brain = At(15);
        Assert.Equal("It's 3:05 PM.", brain.Respond("What time is it?").Text);
        Assert.Equal("Today is Wednesday, November 11.", brain.Respond("what is the date").Text);
    }

    [Fact]
    public void Opens_known_programs_and_refuses_unknown_ones()
    {
        var brain = At(10);
        Assert.Equal(new OpenProgram("notepad.exe"), brain.Respond("open notepad").Action);
        Assert.Equal(new OpenProgram("calc.exe"), brain.Respond("Launch calculator").Action);
        Assert.IsType<OpenUrl>(brain.Respond("open browser").Action);
        var unknown = brain.Respond("open the pod bay doors");
        Assert.Null(unknown.Action);
        Assert.StartsWith("I don't know how to open", unknown.Text);
    }

    [Fact]
    public void Searches_with_an_escaped_query()
    {
        var reply = At(10).Respond("search for black holes & stars");
        Assert.Equal(new OpenUrl("https://www.google.com/search?q=black%20holes%20%20stars"), reply.Action);
    }

    [Theory]
    [InlineData("what is 12 times 7", "That's 84.")]
    [InlineData("calculate 10 divided by 4", "That's 2.5.")]
    [InlineData("what's (2 plus 3) * 4", "That's 20.")]
    public void Does_math(string question, string answer) =>
        Assert.Equal(answer, At(10).Respond(question).Text);

    [Fact]
    public void Math_rejects_anything_but_numbers()
    {
        Assert.Equal("I don't know that one yet. Say help to hear what I can do.", At(10).Respond("what is love").Text);
        Assert.Contains("divide by zero", At(10).Respond("what is 1 divided by 0").Text);
    }

    [Fact]
    public void Keeps_reads_and_clears_notes()
    {
        var brain = At(10);
        Assert.Equal("Noted. You have 1 note.", brain.Respond("note buy milk").Text);
        brain.Respond("remember that the meeting is at 3");
        Assert.Equal("Your notes: buy milk. the meeting is at 3.", brain.Respond("read my notes").Text);
        brain.Respond("clear my notes");
        Assert.Equal("You have no notes.", brain.Respond("read my notes").Text);
    }

    [Fact]
    public void Goodbye_quits() => Assert.IsType<Quit>(At(10).Respond("Goodbye").Action);

    [Fact]
    public void Every_listened_phrase_gets_a_real_answer()
    {
        foreach (var phrase in Brain.Phrases())
            Assert.DoesNotContain("I don't know that one", At(10).Respond(phrase).Text);
    }
}
