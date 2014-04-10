using UnityEngine;

namespace Assets.Scripts
{
	[RequireComponent(typeof(NetworkView))]
    public class PlayerManager : MonoBehaviour
	{
	    public float Speed = 10;
	    private CharacterController controller;

	    private float horizontalMotion;
	    private float verticalMotion;

		private void Start()
		{
		    if (Network.isServer)
		    {
		        controller = GetComponent<CharacterController>();
		    }
		}

		private void Update()
		{
		    if (Network.isClient) return;  // this is server side code
			/*
		    controller.Move(new Vector3(
		                        horizontalMotion * Speed * Time.deltaTime,
		                        0,
		                        verticalMotion * Speed * Time.deltaTime));
			*/
			rigidbody2D.AddForce(new Vector2(horizontalMotion, verticalMotion));
		}
		[RPC]
		public void UpdateClientMotion(float hor, float ver)
		{
		    horizontalMotion = hor;
		    verticalMotion = ver;
		}
	}
}