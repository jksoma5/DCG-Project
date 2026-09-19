using DCG.Core;
using UnityEngine;
namespace DCG.Gameplay
{
    [CreateAssetMenu(menuName = "DCG/Class Definition")]
    public sealed class ClassDefinition : ScriptableObject
    {
        public ClassId classId;
        public string referenceGame;
        public ReferenceStatus referenceStatus = ReferenceStatus.TuningPending;
        public bool playableInLab;
        public GameObject actorPrefab;
        public PrototypeTuning prototypeTuning;
        [TextArea] public string pendingNotes;
    }
}
