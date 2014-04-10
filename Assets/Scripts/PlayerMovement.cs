using UnityEngine;

namespace Assets.Scripts
{
    public class PlayerMovement : MonoBehaviour
    {
		private void Start()
		{
		    rigidbody2D.gravityScale = 0;
		}
        public int PlayerSpeed = 5;
        private void Update()
        {
            var horizontal = Input.GetAxis("Horizontal");
            var vertical = Input.GetAxis("Vertical");
            var vector = new Vector2(horizontal, vertical);
			rigidbody2D.AddForce(vector);
        }
    }
}