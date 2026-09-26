"""Prepare the serialized Multiplayer scene when Editor licensing blocks the generator.

Copies the existing ControlHub scene, preserving all asset references. The Unity menu
DCG/Generate Multiplayer remains the normal way to regenerate and import the scene.
"""
from pathlib import Path
import re
import uuid

ROOT = Path(__file__).resolve().parent.parent
new_sources = [
    "Scripts/Networking/DuelRoom.cs", "Scripts/Networking/DuelMessage.cs",
    "Scripts/Bootstrap/Hub/ControlHubNetwork.cs", "Scripts/Bootstrap/Hub/DuelMap.cs",
    "Scripts/Bootstrap/Hub/MultiplayerController.cs", "Scripts/Bootstrap/Hub/MultiplayerSmokeProbe.cs",
    "Scripts/Editor/MultiplayerSetup.cs", "Tests/EditMode/DuelRoomTests.cs", "Tests/PlayMode/MultiplayerTests.cs",
]

def meta(path):
    metadata = Path(str(path) + ".meta")
    if not metadata.exists():
        guid = uuid.uuid5(uuid.NAMESPACE_URL, "dcg-duel/" + path.relative_to(ROOT).as_posix()).hex
        metadata.write_text("fileFormatVersion: 2\nguid: " + guid + "\n", encoding="utf-8")
    return re.search(r"guid: (\w+)", metadata.read_text(encoding="utf-8-sig")).group(1)

for source in new_sources:
    meta(ROOT / "Assets/_DCG" / source)

source = (ROOT / "Assets/_DCG/Scenes/ControlHub.unity").read_text(encoding="utf-8-sig")
blocks = re.split(r"(?=^--- !u!)", source, flags=re.MULTILINE)
hub_guid = meta(ROOT / "Assets/_DCG/Scripts/Bootstrap/Hub/ControlHubController.cs")
hub_block = next(block for block in blocks if "guid: " + hub_guid in block)
hub_id = re.search(r"^--- !u!114 &(\d+)", hub_block).group(1)
game_object = re.search(r"m_GameObject: \{fileID: (\d+)\}", hub_block).group(1)
ids = {int(value) for value in re.findall(r"^--- !u!\d+ &(\d+)", source, re.MULTILINE)}
next_id = max(value for value in ids if value < 2**31) + 1
new_ids = [next_id, next_id + 1]
assert not ids.intersection(new_ids)
for i, block in enumerate(blocks):
    if block == hub_block:
        blocks[i] = block + "  NetworkMode: 1\n  ArenaMode: 0\n  EnemyAI: 0\n"
    elif block.startswith("--- !u!1 &" + game_object + "\n"):
        blocks[i] = block.replace("  m_Component:\n", "  m_Component:\n" + "".join("  - component: {fileID: " + str(value) + "}\n" for value in new_ids))
    elif re.match(r"--- !u!(23|33|64|65|135|136|137|143|154) &", block):
        # Renderer / Collider classes. MeshFilter has no enabled field and stays unchanged.
        blocks[i] = block.replace("  m_Enabled: 1\n", "  m_Enabled: 0\n")
for value, name in zip(new_ids, ["MultiplayerController", "MultiplayerSmokeProbe"]):
    script_guid = meta(ROOT / ("Assets/_DCG/Scripts/Bootstrap/Hub/" + name + ".cs"))
    blocks.append(f"""--- !u!114 &{value}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {game_object}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}
  m_Name:
  m_EditorClassIdentifier: DCG.Bootstrap::DCG.Bootstrap.Hub.{name}
""" + (f"  hub: {{fileID: {hub_id}}}\n" if name == "MultiplayerController" else ""))
target = ROOT / "Assets/_DCG/Scenes/Multiplayer.unity"
target.write_text("".join(blocks), encoding="utf-8")
guid = meta(target)
settings = ROOT / "ProjectSettings/EditorBuildSettings.asset"
content = settings.read_text(encoding="utf-8-sig")
if "Assets/_DCG/Scenes/Multiplayer.unity" not in content:
    content = content.replace("  m_configObjects:", "  - enabled: 1\n    path: Assets/_DCG/Scenes/Multiplayer.unity\n    guid: " + guid + "\n  m_configObjects:")
    settings.write_text(content, encoding="utf-8")
serialized = target.read_text(encoding="utf-8")
object_ids = re.findall(r"^--- !u!\d+ &(-?\d+)", serialized, re.MULTILINE)
assert len(object_ids) == len(set(object_ids)), "Duplicate serialized object IDs"
assert all(-(2**63) <= int(value) < 2**63 for value in object_ids), "Object ID out of range"
known = set(object_ids) | {"0"}
for reference in re.findall(r"\{fileID: (-?\d+)\}", serialized):
    assert reference in known, "Missing local scene object: " + reference
assert serialized.count("  NetworkMode: 1\n") == 1
print("Prepared Multiplayer scene; all local object references verified. Unity import/play validation remains required.")
