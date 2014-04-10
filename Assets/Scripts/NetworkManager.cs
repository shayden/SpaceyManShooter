using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace Assets.Scripts
{
	public enum NetworkingGroup
	{
	    Default,
		Player,
		Server
	}
	/*
     * Server-side implementation for the generic network manager. 
     * In this class are only functions that are called on or as the server 
     */
    public class NetworkManager : MonoBehaviour
    {
        private const string GameType = "ReallyEliteSpaceyManDestiny_Server";
        private const string RoomName = "TestLevel";
        private HostData[] hostList;

        public GameObject PlayerPrefab;
        private readonly List<ClientPlayerManager> playerTracker = new List<ClientPlayerManager>();
        private readonly List<NetworkPlayer> scheduledSpawns = new List<NetworkPlayer>();
        private bool processSpawnRequests = false;

		private void Start()
		{
            MasterServer.ipAddress = "kitbanger.com";
		}
        private static void StartServer()
        {
            Network.InitializeServer(32, 25000, !Network.HavePublicAddress());
			MasterServer.RegisterHost(GameType, RoomName);
        }
		private static void RefreshHostList()
		{
		    MasterServer.RequestHostList(GameType);
		}

		void OnMasterServerEvent(MasterServerEvent masterServerEvent)
		{
		    if (masterServerEvent == MasterServerEvent.HostListReceived)
		    {
		        hostList = MasterServer.PollHostList();
		    }
		}

        static void JoinServer(HostData hostData)
		{
		    Network.Connect(hostData);
		}
		void OnConnectedToServer()
		{
		    Debug.Log("Server Joined");
			//SpawnPlayer();
		}

		private void OnPlayerConnected(NetworkPlayer player)
		{
		    Debug.Log("Spawning prefab for new client");
			scheduledSpawns.Add(player);
		    processSpawnRequests = true;
		}

		[RPC]
		public void RequestSpawn(NetworkPlayer requester)
		{
		    if (Network.isClient)
		    {
		        Debug.LogError("Client tried to spawn itself!");
		        return;
		    }

		    if (!processSpawnRequests) return;

			foreach (var spawn in scheduledSpawns)
			{
			   Debug.Log("Checking player " + spawn.guid); 
				if (spawn == requester)
				{
				    var handle = Network.Instantiate(
                        PlayerPrefab, 
                        transform.position, 
                        Quaternion.identity, 
                        (int)NetworkingGroup.Player) as GameObject;

				    var sc = handle.GetComponent<ClientPlayerManager>();
					playerTracker.Add(sc);
				    var netView = handle.GetComponent<NetworkView>();
					netView.RPC("setOwner", RPCMode.AllBuffered, spawn);
				}
			}
		    scheduledSpawns.Remove(requester);
			if (scheduledSpawns.Count == 0)
			{
			    Debug.Log("Scheduled Spawns is done");
			    processSpawnRequests = false;
			}

		}
		
		void OnPlayerDisconnected(NetworkPlayer player)
		{
			Debug.Log("Cleaning up after player " + player);
		    ClientPlayerManager found = null;
			foreach (var man in playerTracker.Where(man => man.GetOwner() == player))
			{
			    Network.RemoveRPCs(man.gameObject.networkView.viewID);
			    Network.Destroy(man.gameObject);
                found = man;
			}

			playerTracker.Remove(found);
			
			//Network.RemoveRPCs(player);
			//Network.DestroyPlayerObjects(player);
		}

		void OnServerInitialized()
		{
		    Debug.Log("Server Up");
		    //SpawnPlayer();
		}

        private void SpawnPlayer()
        {
		    if (Network.isServer) return;
            var go = Network.Instantiate(
                PlayerPrefab, 
                new Vector3(Random.Range(1, 10), 1, Random.Range(1, 10)), 
                Quaternion.identity, 0) as GameObject;

        }

		void OnGUI()
		{
		    if (Network.isClient || Network.isServer) return;

			GUILayout.BeginArea(new Rect(100, 100, 250, 500));
		    if (GUILayout.Button("Start Server"))
		    {
		        StartServer();
		    }

			if (GUILayout.Button("Refresh Host List"))
			{
			    RefreshHostList();
			}

		    if (hostList == null)
		    {
				GUILayout.EndArea();
		        return;
		    }

		    foreach (var host in hostList.Where(host => GUILayout.Button(host.gameName)))
		    {
		        JoinServer(host);
		        //FindObjectOfType<Camera>().enabled = false;
		    }
			GUILayout.EndArea();
		}
    }
}