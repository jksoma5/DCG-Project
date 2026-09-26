"""Compile current assemblies using Unity's cached reference lists without starting the Editor.

This is a syntax/type check, not a substitute for Unity import, PlayMode tests or a player build.
"""
from pathlib import Path
import json
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parent.parent
UNITY = Path(r"C:\Program Files\Unity\Hub\Editor\6000.5.5f1\Editor\Data")
OUT = ROOT / "Logs/MultiplayerCompile"
OUT.mkdir(parents=True, exist_ok=True)
caches = list((ROOT / "Library/Bee/artifacts").glob("*.dag/DCG.Gameplay.rsp"))
caches = [p for p in caches if (p.parent / "DCG.Editor.rsp").exists()]
if not caches:
    sys.exit("Open this project in Unity once to populate compiler references.")
cache = max(caches, key=lambda p: p.stat().st_mtime).parent
order = ["Core", "Gameplay", "Classes", "Presentation", "Networking", "Bootstrap", "Editor", "Tests.EditMode", "Tests.PlayMode"]
compiled = {}
for suffix in order:
    name = "DCG." + suffix
    folder = ROOT / ("Assets/_DCG/Tests/" + suffix[6:] if suffix.startswith("Tests.") else "Assets/_DCG/Scripts/" + suffix)
    definition = json.loads((folder / (name + ".asmdef")).read_text(encoding="utf-8-sig"))
    template = cache / (name + ".rsp")
    if not template.exists():
        template = cache / "DCG.Gameplay.rsp"
    lines = []
    for line in template.read_text(encoding="utf-8-sig").splitlines():
        if line.startswith(("-out:", "-refout:", "/additionalfile:", "-analyzer:", "/analyzer:")):
            continue
        if line.strip('"').endswith(".cs"):
            continue
        if line.startswith("-r:") and re.search(r"DCG\.[^/\\]+\.dll", line):
            continue
        lines.append(line)
    lines.append('-out:"' + str(OUT / (name + ".dll")) + '"')
    for reference in definition["references"]:
        if reference.startswith("DCG."):
            lines.append('-r:"' + str(compiled[reference]) + '"')
        elif (ROOT / "Library/ScriptAssemblies" / (reference + ".dll")).exists():
            path = ROOT / "Library/ScriptAssemblies" / (reference + ".dll")
            if not any(reference + ".dll" in line for line in lines):
                lines.append('-r:"' + str(path) + '"')
    for source in sorted(folder.rglob("*.cs")):
        lines.append('"' + str(source) + '"')
    response = OUT / (name + ".rsp")
    response.write_text("\n".join(lines), encoding="utf-8")
    result = subprocess.run([str(UNITY / "NetCoreRuntime/dotnet.exe"), str(UNITY / "DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll"), "@" + str(response)], cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    (OUT / (name + ".log")).write_text(result.stdout + result.stderr, encoding="utf-8")
    print(name, "PASS" if result.returncode == 0 else "FAIL", flush=True)
    if result.stdout or result.stderr:
        print(result.stdout + result.stderr)
    if result.returncode:
        sys.exit(result.returncode)
    compiled[name] = OUT / (name + ".dll")
print("All current runtime, editor and test assemblies compiled.")
