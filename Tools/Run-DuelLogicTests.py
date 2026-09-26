"""Run only engine-independent duel tests with the bundled Mono runtime, after Compile-Duel.py."""
from pathlib import Path
import os
import subprocess
import sys

ROOT = Path(__file__).resolve().parent.parent
UNITY = Path(r"C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Data")
OUT = ROOT / "Logs/MultiplayerCompile"
names = [
    "BothCharactersMustBeConfirmedAndSameClassIsAllowed",
    "MatchingMapsDoNotDrawAndConflictingMapsDrawExactlyOnce",
    "ReadyBarrierCountdownResultAndRematchRejectOldMessages",
    "LoadingTimesOutAndDisconnectHasPhaseSpecificOutcome",
]
source = """using System;
class DuelLogicRunner {
 static int Main() {
  var suite = new DCG.Tests.DuelRoomTests(); int failed = 0;
"""
for name in names:
    source += f'  try {{ suite.{name}(); Console.WriteLine("PASS {name}"); }} catch(Exception e) {{ failed++; Console.WriteLine("FAIL {name}: " + e); }}\n'
source += '  var ngo = new DCG.Tests.DuelNgoLogicTests();\n'
for name in ["QuickStartWaitsForBothPlayersThenUsesDefaultsWithoutDrawing", "QuickStartCanMixWithManualCharacterAndMapSelection", "LateReliableActionSurvivesNewerUnreliableMovement", "StopRejectsOlderMovementAndBoundedEventsDoNotGrowForever", "MovementCoalescesButReliableButtonEdgesRemain"]:
    source += f'  try {{ ngo.{name}(); Console.WriteLine("PASS {name}"); }} catch(Exception e) {{ failed++; Console.WriteLine("FAIL {name}: " + e); }}\n'
source += '  return failed == 0 ? 0 : 1;\n }\n}\n'
(OUT / "DuelLogicRunner.cs").write_text(source, encoding="utf-8")
lines = []
for line in (OUT / "DCG.Tests.EditMode.rsp").read_text(encoding="utf-8").splitlines():
    if line.startswith(("-target:", "-out:")) or line.strip('"').endswith(".cs"):
        continue
    lines.append(line)
lines += ['-target:exe', f'-out:"{OUT / "DuelLogicRunner.exe"}"', f'-r:"{OUT / "DCG.Tests.EditMode.dll"}"', f'"{OUT / "DuelLogicRunner.cs"}"']
(OUT / "DuelLogicRunner.rsp").write_text("\n".join(lines), encoding="utf-8")
compile_result = subprocess.run([str(UNITY / "NetCoreRuntime/dotnet.exe"), str(UNITY / "DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll"), "@" + str(OUT / "DuelLogicRunner.rsp")], cwd=ROOT)
if compile_result.returncode:
    sys.exit(compile_result.returncode)
env = dict(os.environ)
paths = [OUT, ROOT / "Library/ScriptAssemblies", UNITY / "Managed", UNITY / "Managed/UnityEngine"]
paths += list((ROOT / "Library/PackageCache").glob("com.unity.ext.nunit*/net472/unity-custom"))
env["MONO_PATH"] = ";".join(map(str, paths))
result = subprocess.run([str(UNITY / "MonoBleedingEdge/bin/mono.exe"), str(OUT / "DuelLogicRunner.exe")], cwd=ROOT, env=env, capture_output=True, text=True, encoding="utf-8", errors="replace")
(OUT / "logic-tests.log").write_text(result.stdout + result.stderr, encoding="utf-8")
print((result.stdout + result.stderr).replace("\ufeff", ""))
sys.exit(result.returncode)
