using UnityEngine;
using UnityEngine.SceneManagement;

namespace DCG.Bootstrap
{
    public sealed class BootstrapEntry : MonoBehaviour
    {
        void Start() { SceneManager.LoadScene("ControlLab"); }
    }
}
