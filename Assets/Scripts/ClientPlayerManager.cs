using System;
using UnityEngine;

namespace Assets.Scripts
{
	/*
     * Client-side PlayerMovement implementation, only send
     * motion data to the server.
     */
    public class ClientPlayerManager : MonoBehaviour
    {
        // That's actually not the owner but the player,
		// the server instantiated the prefab for, where this script is attached
        private NetworkPlayer owner;

		// These are stored to only send RPCs to the server when
		// the data actually changed.
        private float lastMotionH;
        private float lastMotionV;

		[RPC]
		public void SetOwner(NetworkPlayer player)
		{
		    Debug.Log("Setting owner");
		    owner = player;
			if (player == Network.player)
			{
			    // we are the player
			    enabled = true;
			}
			else
			{
			    if (GetComponent<Camera>()) { GetComponent<Camera>().enabled = false; }
			    if (GetComponent<AudioListener>()) { GetComponent<AudioListener>().enabled = false; }
			    if (GetComponent<GUILayer>()) { GetComponent<GUILayer>().enabled = false; }
			}
		}

        [RPC]
        public NetworkPlayer GetOwner()
        {
            return owner;
        }

		private void Awake()
		{
		    // Disable this by default for now
 			// just to make sure no one can use this until we didn't [sic]
			// find the right player. (see SetOwner())
		    if (Network.isClient) enabled = false;
		}
		private void Update()
		{
		    if (Network.isServer) return; // client side
			if (owner != null && Network.player == owner)
			{
			    var motionH = Input.GetAxis("Horizontal");
			    var motionV = Input.GetAxis("Vertical");
				if (Math.Abs(motionH - lastMotionH) > float.Epsilon || Math.Abs(motionV - lastMotionV) > float.Epsilon)
				{
					networkView.RPC("updateClientMotion", RPCMode.Server, motionH, motionV);
				    lastMotionH = motionH;
				    lastMotionV = motionV;

				}
			}
		}

    }
}