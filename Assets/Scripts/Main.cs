using UnityEngine;

public static class Main
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AfterSceneLoad() => MySqlPad.Runtime.MySqlPadBehaviour.CreateDontDestroyOnLoad();
}
