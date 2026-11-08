# MPRSGxZ.Library.IntegrationTest

Integration tests that exercise the communication and protocol layers against a **real** Monoprice amplifier reached
through a serial-to-Ethernet bridge.

Every test that touches hardware is skipped unless it is explicitly enabled, so a checkout with no rig attached still
produces a green build. Only the pure protocol/validation tests (`CommandGuardTests`, and the unreachable-endpoint case
in `ConnectionTests`) run unconditionally.

## Running

```pwsh
dotnet test
```

The suite uses xUnit v3, which runs on Microsoft.Testing.Platform. `global.json` at the repository root selects that
runner for `dotnet test`; because a solution cannot mix runners, the MSTest unit test project sets
`EnableMSTestRunner`. Note that VSTest-era flags such as `--nologo` and `--logger` are forwarded straight to the test
application and will fail the run — use the platform's own options instead.

To run a subset:

```pwsh
dotnet test --project test\MPRSGxZ.Library.IntegrationTest\MPRSGxZ.Library.IntegrationTest.csproj
```

## Pointing the suite at a rig

Copy the example file and fill in your own values. `appsettings.local.json` is git ignored.

```pwsh
Copy-Item test\MPRSGxZ.Library.IntegrationTest\appsettings.local.example.json `
          test\MPRSGxZ.Library.IntegrationTest\appsettings.local.json
```

| Setting | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `false` | Master switch. Everything hardware-facing is skipped while this is false. |
| `AllowMutation` | `false` | Allows tests that change amplifier state. Separate from `Enabled` because writes are audible. |
| `Host` | *(empty)* | Host name or IP of the serial-to-Ethernet bridge. |
| `Port` | `23` | TCP port the bridge listens on. |
| `ExpectedAmplifierCount` | `1` | Amplifiers physically on the RS-232 chain. Discovery is asserted against this. |
| `MutableZoneId` | `1` | Zone used by writing tests, in flat 1-18 numbering. Pick one you don't mind hearing. |
| `TimeoutSeconds` | `30` | Per-test timeout, so a wedged bridge can't hang the run. |
| `SettleMilliseconds` | `500` | Delay between a write and the read-back, since the amplifier applies sets asynchronously. |

Any setting can be overridden by an environment variable prefixed `MPRSGXZ_`, which is the more convenient route in CI:

```pwsh
$env:MPRSGXZ_HardwareTests__Enabled = 'true'
$env:MPRSGXZ_HardwareTests__Host = '172.16.20.5'
dotnet test
```

## How the tests treat the hardware

- **One client at a time.** The bridge accepts a single TCP client, so `HardwareFixture` hands out an exclusive session
  per test behind a semaphore. Tests never run against the amplifier in parallel.
- **State is restored.** Every mutating test captures the target zone with `ZoneStateSnapshot` before writing and
  restores it in a `finally`. Restore is best-effort and never throws, so a failing assertion isn't masked. Power is
  restored last, since the amplifier ignores audio changes while off.
- **Absent amplifiers are expected.** Zones belonging to amplifiers that aren't present never populate, so assertions
  are filtered against `ExpectedAmplifierCount`.

## Test classes

| Class | Hardware | Covers |
| --- | --- | --- |
| `CommandGuardTests` | no | Frame rendering, broadcast/PA rejection, range guards, `ExpectedLines`. |
| `ConnectionTests` | mostly | Open/close/reopen, baud-derived timeout, poll-interval floor, endpoint re-read, unreachable host. |
| `QueryProtocolTests` | yes | Discovery, framing, field ranges, `ToString` round-trip, desync stress, concurrency. |
| `SetCommandTests` | yes, mutating | Per-property write and read-back, range boundaries, zone isolation, settling. |
| `AmplifierTests` | yes, partly mutating | Facade behaviour: zone population, `ZoneChanged` from polling, write-through, socket release. |

## Logging

Library logs are bridged into xUnit output via `MartinCostello.Logging.XUnit`, so a failing test shows the exact frames
that went over the wire. Use the platform's `--output Detailed` to see output for passing tests too.
