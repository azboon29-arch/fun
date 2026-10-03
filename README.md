# Merge Command (working title)

A portrait mobile merge-strategy game built in Unity 6 LTS (C#, 2D, URP). Merge units and buildings on a base grid,
earn gold, and win campaign battles through formation, counters and well-timed commander abilities rather than raw power.

- **Progress, how to run and test:** [PROGRESS.md](PROGRESS.md)
- **Design decisions and deviations from the spec:** [DESIGN_NOTES.md](DESIGN_NOTES.md)

Quick test of the plain C# logic without Unity (needs the .NET 8 SDK):

```
dotnet test tools/LogicTests/LogicTests.csproj
```
