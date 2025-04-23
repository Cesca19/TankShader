using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

namespace Tanks.Complete
{
    [DefaultExecutionOrder(-10)]
    public class TankMovement : MonoBehaviour
    {
        [Tooltip("The player number. Without a tank selection menu, Player 1 is left keyboard control, Player 2 is right keyboard")]
        public int m_PlayerNumber = 1;
        [Tooltip("The speed in unity unit/second the tank move at")]
        public float m_Speed = 12f;
        [Tooltip("The speed in deg/s that tank will rotate at")]
        public float m_TurnSpeed = 180f;
        [Tooltip("If set to true, the tank auto orient and move toward the pressed direction instead of rotating on left/right and move forward on up")]
        public bool m_IsDirectControl;
        public AudioSource m_MovementAudio;
        public AudioClip m_EngineIdling;
        public AudioClip m_EngineDriving;
        public float m_PitchRange = 0.2f;
        [Tooltip("Is set to true this will be controlled by the computer and not a player")]
        public bool m_IsComputerControlled = false;
        [HideInInspector]
        public TankInputUser m_InputUser;

        // New field for dust particle prefab
        [Tooltip("The dust particle prefab to play when the tank moves")]
        public GameObject m_DustParticlePrefab;

        public Rigidbody Rigidbody => m_Rigidbody;
        public int ControlIndex { get; set; } = -1;

        private string m_MovementAxisName;
        private string m_TurnAxisName;
        private Rigidbody m_Rigidbody;
        private float m_MovementInputValue;
        private float m_TurnInputValue;
        private float m_OriginalPitch;
        private InputAction m_MoveAction;
        private InputAction m_TurnAction;
        private Vector3 m_RequestedDirection;

        // Particle system instance
        private GameObject m_DustParticleInstance;
        private ParticleSystem m_DustParticleSystem;

        private void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            m_InputUser = GetComponent<TankInputUser>();
            if (m_InputUser == null)
                m_InputUser = gameObject.AddComponent<TankInputUser>();

            // Instantiate the dust particle GameObject if a prefab is assigned
            if (m_DustParticlePrefab != null)
            {
                m_DustParticleInstance = Instantiate(m_DustParticlePrefab, transform);
                // Get the ParticleSystem component from the instantiated GameObject's children
                m_DustParticleSystem = m_DustParticleInstance.GetComponentInChildren<ParticleSystem>();
                if (m_DustParticleSystem == null)
                {
                    Debug.LogError("Dust Particle Prefab does not contain a ParticleSystem component in its children!", m_DustParticlePrefab);
                }
                else
                {
                    m_DustParticleSystem.Stop(); // Ensure it's stopped initially
                }
            }
        }

        private void OnEnable()
        {
            m_Rigidbody.isKinematic = false;
            m_MovementInputValue = 0f;
            m_TurnInputValue = 0f;
        }

        private void OnDisable()
        {
            m_Rigidbody.isKinematic = true;
        }

        private void Start()
        {
            // Existing Start code...
            if (m_IsComputerControlled)
            {
                var ai = GetComponent<TankAI>();
                if (ai == null)
                {
                    gameObject.AddComponent<TankAI>();
                }
            }

            if (ControlIndex == -1 && !m_IsComputerControlled)
            {
                ControlIndex = m_PlayerNumber;
            }

            var mobileControl = FindAnyObjectByType<MobileUIControl>();
            if (mobileControl != null && ControlIndex == 1)
            {
                m_InputUser.SetNewInputUser(InputUser.PerformPairingWithDevice(mobileControl.Device));
                m_InputUser.ActivateScheme("Gamepad");
            }
            else
            {
                m_InputUser.ActivateScheme(ControlIndex == 1 ? "KeyboardLeft" : "KeyboardRight");
            }

            m_MovementAxisName = "Vertical";
            m_TurnAxisName = "Horizontal";
            m_MoveAction = m_InputUser.ActionAsset.FindAction(m_MovementAxisName);
            m_TurnAction = m_InputUser.ActionAsset.FindAction(m_TurnAxisName);
            m_MoveAction.Enable();
            m_TurnAction.Enable();
            m_OriginalPitch = m_MovementAudio.pitch;
        }

        private void Update()
        {
            if (!m_IsComputerControlled)
            {
                m_MovementInputValue = m_MoveAction.ReadValue<float>();
                m_TurnInputValue = m_TurnAction.ReadValue<float>();
            }

            EngineAudio();
            UpdateDustParticles(); // Update dust particles based on movement
        }

        private void EngineAudio()
        {
            // Existing EngineAudio code...
            if (Mathf.Abs(m_MovementInputValue) < 0.1f && Mathf.Abs(m_TurnInputValue) < 0.1f)
            {
                if (m_MovementAudio.clip == m_EngineDriving)
                {
                    m_MovementAudio.clip = m_EngineIdling;
                    m_MovementAudio.pitch = Random.Range(m_OriginalPitch - m_PitchRange, m_OriginalPitch + m_PitchRange);
                    m_MovementAudio.Play();
                }
            }
            else
            {
                if (m_MovementAudio.clip == m_EngineIdling)
                {
                    m_MovementAudio.clip = m_EngineDriving;
                    m_MovementAudio.pitch = Random.Range(m_OriginalPitch - m_PitchRange, m_OriginalPitch + m_PitchRange);
                    m_MovementAudio.Play();
                }
            }
        }

        private void UpdateDustParticles()
        {
            if (m_DustParticleSystem == null) return;

            // Play dust particles when the tank is moving
            if (Mathf.Abs(m_MovementInputValue) > 0.1f || Mathf.Abs(m_TurnInputValue) > 0.1f)
            {
                if (!m_DustParticleSystem.isPlaying)
                {
                    m_DustParticleSystem.Play();
                }

                // Optional: Adjust emission rate based on speed
                var emission = m_DustParticleSystem.emission;
                float speedFactor = Mathf.Clamp01((Mathf.Abs(m_MovementInputValue) + Mathf.Abs(m_TurnInputValue)) / 2f);
                emission.rateOverTime = 20f * speedFactor; // Adjust 20f to match your particle system
            }
            else
            {
                if (m_DustParticleSystem.isPlaying)
                {
                    m_DustParticleSystem.Stop();
                }
            }
        }

        private void FixedUpdate()
        {
            // Existing FixedUpdate code...
            if (m_InputUser.InputUser.controlScheme.Value.name == "Gamepad" || m_IsDirectControl)
            {
                var camForward = Camera.main.transform.forward;
                camForward.y = 0;
                camForward.Normalize();
                var camRight = Vector3.Cross(Vector3.up, camForward);
                m_RequestedDirection = (camForward * m_MovementInputValue + camRight * m_TurnInputValue);
            }

            Move();
            Turn();
        }

        private void Move()
        {
            float speedInput = 0.0f;
            if (m_InputUser.InputUser.controlScheme.Value.name == "Gamepad" || m_IsDirectControl)
            {
                speedInput = m_RequestedDirection.magnitude;
                speedInput *= 1.0f - Mathf.Clamp01((Vector3.Angle(m_RequestedDirection, transform.forward) - 90) / 90.0f);
            }
            else
            {
                speedInput = m_MovementInputValue;
            }

            Vector3 movement = transform.forward * speedInput * m_Speed * Time.deltaTime;
            m_Rigidbody.MovePosition(m_Rigidbody.position + movement);
        }

        private void Turn()
        {
            Quaternion turnRotation;
            if (m_InputUser.InputUser.controlScheme.Value.name == "Gamepad" || m_IsDirectControl)
            {
                float angleTowardTarget = Vector3.SignedAngle(m_RequestedDirection, transform.forward, transform.up);
                var rotatingAngle = Mathf.Sign(angleTowardTarget) * Mathf.Min(Mathf.Abs(angleTowardTarget), m_TurnSpeed * Time.deltaTime);
                turnRotation = Quaternion.AngleAxis(-rotatingAngle, Vector3.up);
            }
            else
            {
                float turn = m_TurnInputValue * m_TurnSpeed * Time.deltaTime;
                turnRotation = Quaternion.Euler(0f, turn, 0f);
            }

            m_Rigidbody.MoveRotation(m_Rigidbody.rotation * turnRotation);
        }

        // Clean up the particle system when the tank is destroyed
        private void OnDestroy()
        {
            if (m_DustParticleInstance != null)
            {
                Destroy(m_DustParticleInstance.gameObject);
            }
        }
    }
}