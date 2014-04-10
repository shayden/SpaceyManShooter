using System.Collections;
using System.Reflection;
using UnityEngine;

// For the client which owns the player, the local transform contains
// the current client _predicted_ position/rotation. The buffered state
// contains the _server_ position/rotation, which is the correct state. The
// predicted and server states are interpolated together so the predicted
// state will always converge with the server state.
//
// For the server the transform contains the current 100% legal position and
// rotation of the player in question. He sends this over the network
// to all clients.
//
// For other clients, which don't own this player, the buffered state contains
// the correct values. This is played back with a 100 ms delay to elminate
// choppyness. The delay needs to be higher is the ping time between the server
// and client is larger than 100 ms.

namespace Assets.Scripts
{
    public class GraduallyUpdateState : MonoBehaviour
    {
        // We store twenty states with "playback" information
        private readonly State[] m_BufferedState = new State[20];
        private readonly State[] m_LocalBufState = new State[20];
        private Rect connInfo = new Rect(Screen.width - 280, 40, 320, 160);
        private FieldInfo isMovingFieldInfo;
        private bool m_FixError;
        public double m_InterpolationBackTime = 0.1;
        // Keep track of what slots are used

        private bool m_IsMine;
        private int m_LocalStateCount;

        // Stat variables for latency, msg rate
        private int m_MsgCounter;
        private double m_MsgLatency;
        private double m_MsgLatencyTotal;
        private int m_MsgRate;

        // Stat variabels for prediction stuff
        private Vector3 m_NewPosition;
        private float m_PredictionAccuracy;

        // The position vector distance to start error correction. The higher the latency the higher this
        // value should be or it constantly tries to correct errors in prediction, of course this depends
        // on the game too.
        public float m_PredictionThreshold = 0.25F;
        private float m_TimeAccuracy;
        // Time difference in milliseconds where we check for error in position. If the server time value
        // of a state is too different from the local state time then the error correction comparison is
        // highly unreliable and you might try to correct more errors than there really are.
        public float m_TimeThreshold = 0.05F;
        private float m_Timer = 0;
        private int m_TimestampCount;

        private Rect playerInfo = new Rect(0, 0, 160, 80);
        private GameObject spawnTracker;
        private Component targetController;

        // We need to grab a reference to the isMoving variable in the javascript ThirdPersonController script

        // Convert field info from character controller script to a local bool variable
        //private bool targetIsMoving
        //{
        //    get { return (bool) isMovingFieldInfo.GetValue(targetController); }
        //}

        private void Start()
        {
            //targetController = GetComponent("ThirdPersonController");
            //isMovingFieldInfo = targetController.GetType().GetField("isMoving");
            //spawnTracker = GameObject.Find("SpawnPoint");
            m_Timer = Time.time + 1;
			if (networkView.isMine)
			{
			    SetOwnership();
			}
			else
			{
			    enabled = false;
			}
        }

        private IEnumerator MonitorLocalMovement()
        {
            while (true)
            {
                yield return new WaitForSeconds(1 / 15);

                // Shift buffer contents, oldest data erased, 18 becomes 19, ... , 0 becomes 1
                for (int i = m_LocalBufState.Length - 1; i >= 1; i--)
                {
                    m_LocalBufState[i] = m_LocalBufState[i - 1];
                }

                // Save currect received state as 0 in the buffer, safe to overwrite after shifting
                State state;
                state.timestamp = Network.time;
                state.pos = transform.position;
                state.rot = transform.rotation;
                m_LocalBufState[0] = state;

                // Increment state count but never exceed buffer size
                m_LocalStateCount = Mathf.Min(m_LocalStateCount + 1, m_LocalBufState.Length);

                //
                // Check if the client side prediction has an error
                //

                // Find the local buffered state which is closest to network state in time
                int j = 0;
                bool match = false;
                for (j = 0; j < m_LocalStateCount - 1; j++)
                {
                    if (m_BufferedState[0].timestamp <= m_LocalBufState[j].timestamp &&
                        m_LocalBufState[j].timestamp - m_BufferedState[0].timestamp <= m_TimeThreshold)
                    {
                        Debug.Log("Comparing state " + j + "localtime: " + m_LocalBufState[j].timestamp +
                                  " networktime: " +
                                  m_BufferedState[0].timestamp);
                        Debug.Log("Local: " + m_LocalBufState[j].pos + " Network: " + m_BufferedState[0].pos);
                        m_TimeAccuracy =
                            Mathf.Abs((float) m_LocalBufState[j].timestamp - (float) m_BufferedState[0].timestamp);
                        m_PredictionAccuracy = (Vector3.Distance(m_LocalBufState[j].pos, m_BufferedState[0].pos));
                        match = true;
                        break;
                    }
                }
                if (!match)
                {
                    //Debug.Log("No match!");
                }
                    // If prediction is off, diverge current location by the amount of the offset
                else if (m_PredictionAccuracy > m_PredictionThreshold)
                {
                    //Debug.Log("Error in prediction("+m_PredictionAccuracy+"), local is " + m_LocalBufState[j].pos + " network is " + m_BufferedState[0].pos);
                    //Debug.Log("Local time: " + m_LocalBufState[j].timestamp + " Network time: " + m_BufferedState[0].timestamp);

                    // Find how far we travelled since the prediction failed
                    Vector3 localMovement = m_LocalBufState[j].pos - m_LocalBufState[0].pos;

                    // "Erase" old values in the local buffer
                    m_LocalStateCount = 1;

                    // New position which we need to converge to in the update loop                        
                    m_NewPosition = m_BufferedState[0].pos + localMovement;

                    // Trigger the new position convergence routine                        
                    m_FixError = true;
                }
                else
                {
                    m_FixError = false;
                }
            }
        }

        private void OnGUI()
        {
            if (m_IsMine)
            {
                connInfo = GUILayout.Window(0, connInfo, MakeConnInfoWindow, "Local Player");
            }
            else
            {
                playerInfo = GUILayout.Window(1, playerInfo, MakeNetPlayerInfoWindow, "Net Player");
            }
        }

        private void MakeNetPlayerInfoWindow(int windowID)
        {
            GUILayout.Label(string.Format("Latest Pos: {0},{1},{2}", m_BufferedState[0].pos.x, m_BufferedState[0].pos.y,
                                          m_BufferedState[0].pos.z));
        }

        private void MakeConnInfoWindow(int windowID)
        {
            //GUILayout.BeginVertical();
            GUILayout.Label(string.Format("{0} msg/s {1,4:f3} ms", m_MsgRate, m_MsgLatency));
            GUILayout.Label(string.Format("Time Difference : {0,3:f3}", m_TimeAccuracy));
            GUILayout.Label(string.Format("Prediction Difference : {0,3:f3}", m_PredictionAccuracy));
            //GUILayout.EndVertical();
            if (Time.time - m_Timer > 0)
            {
                m_MsgRate = m_MsgCounter;
                m_Timer = Time.time + 1;
                m_MsgCounter = 0;
                if (m_MsgRate != 0)
                {
                    m_MsgLatency = (m_MsgLatencyTotal / m_MsgRate) * 1000F;
                }
                else
                {
                    m_MsgLatency = 0;
                }
                m_MsgLatencyTotal = 0;
            }
            GUILayout.Label(string.Format("Fix Error : {0}", m_FixError));
            GUILayout.Label(string.Format("Latest Pos: {0},{1},{2}", m_BufferedState[0].pos.x, m_BufferedState[0].pos.y,
                                          m_BufferedState[0].pos.z));
        }

        // The network sync routine makes sure m_BufferedState always contains the last 20 updates
        // The latest update is in slot 0, oldest in slot 19
        private void OnSerializeNetworkView(BitStream stream, NetworkMessageInfo info)
        {
			Debug.Log("Serializing");
            // Always send transform (depending on reliability of the network view)
            if (stream.isWriting)
            {
                Vector3 pos = transform.position;
                Quaternion rot = transform.rotation;
                stream.Serialize(ref pos);
                stream.Serialize(ref rot);
            }
                // When receiving, buffer the information
            else
            {
                m_MsgCounter++;
                m_MsgLatencyTotal += (Network.time - info.timestamp);

                // Receive latest state information
                Vector3 pos = Vector3.zero;
                Quaternion rot = transform.rotation; //Quaternion.identity;
                stream.Serialize(ref pos);
                stream.Serialize(ref rot);

                // Shift buffer contents, oldest data erased, 18 becomes 19, ... , 0 becomes 1
                for (int i = m_BufferedState.Length - 1; i >= 1; i--)
                {
                    m_BufferedState[i] = m_BufferedState[i - 1];
                }

                // Save currect received state as 0 in the buffer, safe to overwrite after shifting
                State state;
                state.timestamp = info.timestamp;
                state.pos = pos;
                state.rot = rot;
                m_BufferedState[0] = state;

                // Increment state count but never exceed buffer size
                m_TimestampCount = Mathf.Min(m_TimestampCount + 1, m_BufferedState.Length);

                // Check integrity, lowest numbered state in the buffer is newest and so on
                for (int i = 0; i < m_TimestampCount - 1; i++)
                {
                    if (m_BufferedState[i].timestamp < m_BufferedState[i + 1].timestamp)
                        Debug.Log("State inconsistent");
                }

                //Debug.Log("stamp: " + info.timestamp + "my time: " + Network.time + "delta: " + (Network.time - info.timestamp));
            }
        }

        private void SetOwnership()
        {
            Debug.Log("Setting ownership for local player");
            m_IsMine = true;
            StartCoroutine(MonitorLocalMovement());
        }

        // This only runs where the component is enabled, which is only on remote peers (server/clients)
        private void Update()
        {
            double currentTime = Network.time;
            double interpolationTime = currentTime - m_InterpolationBackTime;
            // We have a window of interpolationBackTime where we basically play
            // By having interpolationBackTime the average ping, you will usually use interpolation.
            // And only if no more data arrives we will use extrapolation

            // If this is my player interpolate server position with the position set by me
            if (m_IsMine && m_FixError)
            {
                Vector3 difference = m_NewPosition - transform.position;
                // This is a cheap method for interpolating server and client positions. The higher
                // the difference the closer to the client state we will go. This is to minimize big jumps
                // in the movment. This can be done differenctly, like for example just doing 50/50 server
                // and client position.
                transform.position = Vector3.Lerp(m_NewPosition, transform.position, difference.magnitude);
                // If we are not moving converge to the 100% accurate server position
            }
            //else if (m_IsMine && !targetIsMoving)
            //{
            //    transform.position = Vector3.Lerp(m_BufferedState[0].pos, transform.position, 0.95F);
            //    // Use interpolation for other remote clients
            //}
            else if (Network.isClient && !m_IsMine)
            {
                // Use interpolation
                // Check if latest state exceeds interpolation time, if this is the case then
                // it is too old and extrapolation should be used
                if (m_BufferedState[0].timestamp > interpolationTime)
                {
                    for (int i = 0; i < m_TimestampCount; i++)
                    {
                        // Find the state which matches the interpolation time (time+0.1) or use last state
                        if (m_BufferedState[i].timestamp <= interpolationTime || i == m_TimestampCount - 1)
                        {
                            // The state one slot newer (<100ms) than the best playback state
                            State rhs = m_BufferedState[Mathf.Max(i - 1, 0)];
                            // The best playback state (closest to 100 ms old (default time))
                            State lhs = m_BufferedState[i];

                            // Use the time between the two slots to determine if interpolation is necessary
                            double length = rhs.timestamp - lhs.timestamp;
                            float t = 0.0F;
                            // As the time difference gets closer to 100 ms t gets closer to 1 in
                            // which case rhs is only used
                            if (length > 0.0001)
                                t = (float) ((interpolationTime - lhs.timestamp) / length);

                            // if t=0 => lhs is used directly
                            transform.position = Vector3.Lerp(lhs.pos, rhs.pos, t);
                            transform.rotation = Quaternion.Slerp(lhs.rot, rhs.rot, t);
                            //m_InterpolationTime = (Network.time - m_BufferedState[i].timestamp)*1000;
                            return;
                        }
                    }
                }
                    // Use extrapolation. Here we do something really simple and just repeat the last
                    // received state. You can do clever stuff with predicting what should happen.
                else
                {
                    State latest = m_BufferedState[0];

                    transform.localPosition = latest.pos;
                    transform.localRotation = latest.rot;
                    //Debug.Log("Extrapolating " + latest.pos);
                }
            }
        }

        private void OnDisconnectedFromServer(NetworkDisconnection info)
        {
            if (Network.isServer)
            {
                Debug.Log("Local server connection disconnected");
            }
            else
            {
                if (info == NetworkDisconnection.LostConnection)
                    Debug.Log("Lost connection to the server");
                else
                {
                    Debug.Log("Successfully diconnected from the server.  PeerType now " + Network.peerType);
                }
                if (spawnTracker == null)
                {
                    Debug.Log("SpawnTracker is null");
                }
                else
                {
                    //FIX ME this locks unity client on disconnection from server
                    //Right now there is only one network view for this object, the transform network view.  
                    //In the future we could have many more so we would need to index by the proper one.
                    NetworkView[] netViews = gameObject.GetComponents<NetworkView>();
                    if (netViews.Length == 1)
                        spawnTracker.SendMessage("CleanUpPlayer", netViews[0].viewID);
                    else
                        Debug.Log("Could not find the network views.");
                }
            }
            //Destroy(gameObject);
        }

        internal struct State
        {
            internal Vector3 pos;
            internal Quaternion rot;
            internal double timestamp;
        }
    }
}