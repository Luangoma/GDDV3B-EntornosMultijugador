using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Add this component to the same GameObject as
/// the NetworkManager component.
/// </summary>
public class NM_script : MonoBehaviour
{
    private NetworkManager m_NetworkManager;

    private void Awake()
    {
        m_NetworkManager = GetComponent<NetworkManager>();
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 300));
        if (!m_NetworkManager.IsClient && !m_NetworkManager.IsServer)
        {
            StartButtons();
        }
        GUILayout.EndArea();
    }

    private void StartButtons()
    {
        if (GUILayout.Button("Host")) m_NetworkManager.StartHost();
        if (GUILayout.Button("Client")) m_NetworkManager.StartClient();
        if (GUILayout.Button("Server")) m_NetworkManager.StartServer();
    }

    //void StartHost() {
    //    NetworkManager.Singleton.StartHost();
    //}
    //void StartServer() {
    //    NetworkManager.Singleton.StartServer();
    //}
    //void StartClient() { 
    //    NetworkManager.Singleton.StartClient();
    //}
}