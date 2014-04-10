using UnityEngine;

namespace Assets.Scripts
{
    public class EnableComponents : MonoBehaviour
    {
        private PlayerMovement movement;
        private void Start()
        {
            movement = GetComponent<PlayerMovement>();

            EnableControls(networkView.isMine);
        }

		private void EnableControls(bool enable)
		{
		    if (movement != null) movement.enabled = enable;
		}
    }
}