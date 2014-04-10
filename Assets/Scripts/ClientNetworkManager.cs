using UnityEngine;

namespace Assets.Scripts
{
    public class ClientNetworkManager : MonoBehaviour
    {
		private void OnConnectedToServer()
		{
		    Debug.Log("Disabling message queue");
			/* TODO: when a menu scene is added */
		    //Network.isMessageQueueRunning = false;
			networkView.RPC("requestSpawn", RPCMode.Server, Network.player);

		}

		/* TODO:
		private void OnLevelWasLoaded(int level)
		{
		    if (level != 0 && Network.isClient)
		    {
		        Network.isMessageQueueRunning = true;
				Debug.Log("Level loaded, requesting spawn");
				Debug.Log("Enabling Message Queue");
				networkView.RPC("requestSpawn", RPCMode.Server, Network.player);
		    }
		}
		*/
    }
}