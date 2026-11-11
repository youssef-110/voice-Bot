using System.Diagnostics;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using VoiceBot;

// Usage: VoiceBot            listen on the microphone and speak replies
//        VoiceBot --type     type commands instead (no microphone needed)
//        VoiceBot --mute     do not speak replies aloud

bool typeMode = args.Contains("--type");
bool mute = args.Contains("--mute");

var brain = new Brain();
using var voice = new SpeechSynthesizer();
voice.SetOutputToDefaultAudioDevice();

void Say(string text)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("Bot: " + text);
    Console.ResetColor();
    if (!mute) voice.Speak(text);
}

void Run(BotAction? action)
{
    try
    {
        switch (action)
        {
            case OpenProgram p:
                Process.Start(new ProcessStartInfo(p.FileName) { UseShellExecute = true });
                break;
            case OpenUrl u:
                Process.Start(new ProcessStartInfo(u.Url) { UseShellExecute = true });
                break;
        }
    }
    catch (Exception e)
    {
        Say("I couldn't do that: " + e.Message);
    }
}

// Returns false when the user said goodbye.
bool Handle(string heard)
{
    Console.WriteLine("You: " + heard);
    var reply = brain.Respond(heard);
    Say(reply.Text);
    Run(reply.Action);
    return reply.Action is not Quit;
}

Console.WriteLine("Voice Bot. Say \"help\" to hear what I can do, \"goodbye\" to quit.\n");

if (typeMode)
{
    Say("Hi! Type a command.");
    while (Console.ReadLine() is { } line && Handle(line)) { }
    return;
}

SpeechRecognitionEngine recognizer;
try
{
    recognizer = new SpeechRecognitionEngine(System.Globalization.CultureInfo.CurrentUICulture);
}
catch (ArgumentException)
{
    // No recognizer for the UI language; fall back to the installed default.
    recognizer = new SpeechRecognitionEngine();
}

using (recognizer)
{
    try
    {
        recognizer.SetInputToDefaultAudioDevice();
    }
    catch (InvalidOperationException)
    {
        Console.WriteLine("No microphone found. Run with --type to type commands instead.");
        return;
    }

    // A fixed command grammar plus open dictation for searches, notes and math.
    recognizer.LoadGrammar(new Grammar(new GrammarBuilder(new Choices(Brain.Phrases().ToArray()))) { Name = "commands", Priority = 1 });
    recognizer.LoadGrammar(new DictationGrammar { Name = "dictation" });

    var done = new ManualResetEventSlim();
    recognizer.SpeechRecognized += (_, e) =>
    {
        if (e.Result.Confidence < 0.55) return;  // ignore background noise
        recognizer.RecognizeAsyncCancel();       // don't listen to our own voice
        if (!Handle(e.Result.Text)) { done.Set(); return; }
        recognizer.RecognizeAsync(RecognizeMode.Multiple);
    };

    Say("Hi! I'm listening.");
    recognizer.RecognizeAsync(RecognizeMode.Multiple);
    done.Wait();
}
