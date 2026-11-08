# Build and tests

Run these commands from the repository root.

```powershell
dotnet build tests/IsTaskbarVisible.Tests -c Release
dotnet run --project tests/IsTaskbarVisible.Tests -c Release --no-build
dotnet run --project tests/IsTaskbarVisible.Tests -c Release --no-build -- --native-read-only
dotnet publish tests/IsTaskbarVisible.Tests -c Release -r win-x64 -p:PublishAot=true
dotnet publish tests/IsTaskbarVisible.Tests -c Release -r win-x86 -p:PublishAot=true
```

Default tests use deterministic native-call substitutes. The optional native
checks only read existing taskbar/monitor state; they do not change settings,
create visible windows, or send input. Supports x64, x86 and NativeAOT.
