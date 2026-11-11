# Voice Bot

A voice assistant for Windows, written in C#. It listens on your microphone with the
built-in Windows speech recognizer and answers out loud. Everything runs offline: no
cloud speech service, no API key.

```
You: what is 12 times 7
Bot: That's 84.
You: open notepad
Bot: Opening notepad.
You: note buy milk
Bot: Noted. You have 1 note.
```

## What it can do

| Say | It does |
|---|---|
| hello | Greets you for the time of day |
| what time is it / what is the date | Tells the time or date |
| open notepad, calculator, paint, file explorer, task manager, command prompt, settings, browser | Opens the app |
| search for black holes | Opens a Google search |
| what is 12 times 7 / calculate 10 divided by 4 | Does the math |
| note buy milk / read my notes / clear my notes | Keeps notes for the session |
| tell me a joke / who are you / help | Small talk |
| goodbye | Quits |

## Run

Needs Windows and the [.NET 9 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project VoiceBot              # talk to it
dotnet run --project VoiceBot -- --type    # type commands instead (no microphone)
dotnet run --project VoiceBot -- --mute    # no spoken replies
```

Recognition uses the speech language installed in Windows (Settings, Time & language,
Speech). English works best.

## Test

```bash
dotnet test
```

## How it works

- `Brain.cs` turns text into a reply and an optional action (open a program, open a URL,
  quit). It has no audio code, so all commands are unit tested.
- `Program.cs` wires it to `System.Speech`: a fixed grammar of known commands for
  accuracy, plus dictation for free-form searches, notes and math. It stops listening
  while it speaks so it does not hear itself.
- Math goes through `DataTable.Compute`, but only after a check that the expression
  holds nothing except digits and operators.
