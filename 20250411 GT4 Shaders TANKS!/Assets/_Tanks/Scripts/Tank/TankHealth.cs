﻿using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Tanks.Complete
{
    public class TankHealth : MonoBehaviour
    {
        public float m_StartingHealth = 100f;               // The amount of health each tank starts with.
        public Slider m_Slider;                             // The slider to represent how much health the tank currently has.
        public Image m_FillImage;                           // The image component of the slider.
        public Color m_FullHealthColor = Color.green;    // The color the health bar will be when on full health.
        public Color m_ZeroHealthColor = Color.red;      // The color the health bar will be when on no health.
        public GameObject m_ExplosionPrefab;                // A prefab that will be instantiated in Awake, then used whenever the tank dies.
        public GameObject m_SmokeParticlePrefab;            // Prefab for the smoke particle system when health is low.
        public float m_LowHealthPercentThreshold = 0.6f;    // Health percentage (0 to 1) below which smoke appears (e.g., 30%).
        public float m_MinSmokeEmissionRate = 10f;          // Minimum emission rate for smoke (when health is at m_LowHealthPercentThreshold).
        public float m_MaxSmokeEmissionRate = 50f;          // Maximum emission rate for smoke (when health is at 0).
        [HideInInspector] public bool m_HasShield;          // Has the tank picked up a shield power up?

        private AudioSource m_ExplosionAudio;               // The audio source to play when the tank explodes.
        private float m_CurrentHealth;                      // How much health the tank currently has.
        private bool m_Dead;                                // Has the tank been reduced beyond zero health yet?
        private float m_ShieldValue;                        // Percentage of reduced damage when the tank has a shield.
        private bool m_IsInvincible;                        // Is the tank invincible in this moment?
        private GameObject m_SmokeInstance;                 // Reference to the instantiated smoke particle system.
        
        [Header("Hit Effect")]
        public float m_MaxVignetteIntensity = 0.5f;   // Maximum vignette intensity when hit
        public float m_VignetteFadeSpeed = 2.0f;      // How quickly the vignette fades away
        
        public Volume m_PostProcessingVolume;        // Reference to your volume component in the scene
        private Vignette m_Vignette;                  // Reference to the vignette effect
        private float m_CurrentVignetteIntensity;     // Current intensity of the vignette effect

        private float dissolveDuration = 2.0f;
        private float dissolveValue = 0f;
        private bool isDissolving = false;
        MeshRenderer[] renderers;

        private void Awake()
        {
            // Set the slider max value to the max health the tank can have
            m_Slider.maxValue = m_StartingHealth;
            
            // Find the post-processing volume in the scene
            m_PostProcessingVolume = FindObjectOfType<Volume>();
            
            // Find and set up the vignette effect
            if (m_PostProcessingVolume != null && m_PostProcessingVolume.profile != null)
            {
                if (!m_PostProcessingVolume.profile.TryGet(out m_Vignette))
                {
                    Debug.LogWarning("Vignette effect not found in Volume profile!");
                }
            }
            else
            {
                Debug.LogWarning("No Post Processing Volume found in the scene!");
            }
            renderers = transform.gameObject.GetComponentsInChildren<MeshRenderer>();
        }
        
        private void Update()
        {
            // Gradually fade out the vignette effect using exponential decay
            if (m_Vignette != null && m_Vignette.intensity.value > 0.01f)
            {
                // Using exponential decay: intensity = intensity * e^(-fadeSpeed * deltaTime)
                // This makes higher fade speed values result in a slower fade
                float newIntensity = m_Vignette.intensity.value * Mathf.Exp(-m_VignetteFadeSpeed * 0.1f * Time.deltaTime);
        
                // Ensure it doesn't get too small but never quite reaches zero
                if (newIntensity < 0.01f)
                    newIntensity = 0f;
            
                m_Vignette.intensity.Override(newIntensity);
            }
            else if (m_Vignette != null && m_Vignette.intensity.value > 0f && m_Vignette.intensity.value <= 0.01f)
            {
                // When it gets very small, set it to zero
                m_Vignette.intensity.Override(0f);
            }

            if (isDissolving)
            {
                dissolveValue += Time.deltaTime / dissolveDuration;
                for (int i = 0; i < renderers.Length; i++) {
                    var renderer = renderers[i];
                    for (int j = 0; j < renderer.materials.Length; ++j)
                        renderer.materials[j].SetFloat("_Dissolve", dissolveValue);
                } 
                if (dissolveValue >= 1f)
                {
                    isDissolving = false;
                    gameObject.SetActive(false);
                }
            }
        }

        private void OnDestroy()
        {
            // Clean up smoke instance if it exists
            if (m_SmokeInstance != null)
            {
                Destroy(m_SmokeInstance);
            }
        }

        private void OnEnable()
        {
            // When the tank is enabled, reset the tank's health and whether or not it's dead.
            m_CurrentHealth = m_StartingHealth;
            m_Dead = false;
            m_HasShield = false;
            m_ShieldValue = 0;
            m_IsInvincible = false;

            // Ensure smoke is not active initially
            if (m_SmokeInstance != null)
            {
                Destroy(m_SmokeInstance);
                m_SmokeInstance = null;
            }

            // Update the health slider's value and color.
            SetHealthUI();
        }

        public void TakeDamage(float amount)
        {
            // Check if the tank is not invincible
            if (!m_IsInvincible)
            {
                // Activate vignette effect when taking damage
                ApplyVignetteEffect();
                
                // Reduce current health by the amount of damage done.
                m_CurrentHealth -= amount * (1 - m_ShieldValue);

                // Change the UI elements appropriately.
                SetHealthUI();

                // If the current health is at or below zero and it has not yet been registered, call OnDeath.
                if (m_CurrentHealth <= 0f && !m_Dead)
                {
                    OnDeath();
                }
            }
        }

        public void IncreaseHealth(float amount)
        {
            // Check if adding the amount would keep the health within the maximum limit
            if (m_CurrentHealth + amount <= m_StartingHealth)
            {
                // If the new health value is within the limit, add the amount
                m_CurrentHealth += amount;
            }
            else
            {
                // If the new health exceeds the starting health, set it at the maximum
                m_CurrentHealth = m_StartingHealth;
            }

            // Change the UI elements appropriately.
            SetHealthUI();
        }

        public void ToggleShield(float shieldAmount)
        {
            // Inverts the value of has shield.
            m_HasShield = !m_HasShield;

            // Establish the amount of damage that will be reduced by the shield
            if (m_HasShield)
            {
                m_ShieldValue = shieldAmount;
            }
            else
            {
                m_ShieldValue = 0;
            }
        }

        public void ToggleInvincibility()
        {
            m_IsInvincible = !m_IsInvincible;
        }

        private void SetHealthUI()
        {
            // Set the slider's value appropriately.
            m_Slider.value = m_CurrentHealth;

            // Interpolate the color of the bar between the chosen colors based on the current percentage of the starting health.
            m_FillImage.color = Color.Lerp(m_ZeroHealthColor, m_FullHealthColor, m_CurrentHealth / m_StartingHealth);

            // Calculate health percentage
            float healthPercent = m_CurrentHealth / m_StartingHealth;

            // Manage smoke particle system based on health percentage
            if (healthPercent <= m_LowHealthPercentThreshold && m_SmokeInstance == null && !m_Dead)
            {
                // Instantiate smoke particle system if health is below threshold and no smoke exists
                m_SmokeInstance = Instantiate(m_SmokeParticlePrefab, transform);
                m_SmokeInstance.transform.localPosition = Vector3.zero; // Adjust position relative to tank if needed
            }
            else if (healthPercent > m_LowHealthPercentThreshold && m_SmokeInstance != null)
            {
                // Destroy smoke particle system if health is above threshold
                Destroy(m_SmokeInstance);
                m_SmokeInstance = null;
            }

            // Adjust smoke intensity if smoke is active
            if (m_SmokeInstance != null)
            {
                // Use GetComponentInChildren to find the ParticleSystem in case it's on a child object
                var particleSystem = m_SmokeInstance.GetComponentInChildren<ParticleSystem>();
                if (particleSystem != null)
                {
                    var emission = particleSystem.emission;
                    // Interpolate emission rate: max at 0% health, min at m_LowHealthPercentThreshold
                    float emissionRatio = Mathf.Clamp01(healthPercent / m_LowHealthPercentThreshold);
                    float emissionRate = Mathf.Lerp(m_MaxSmokeEmissionRate, m_MinSmokeEmissionRate, emissionRatio);

                    // Set the emission rate and reassign the modified emission module
                    emission.rateOverTime = new ParticleSystem.MinMaxCurve(emissionRate);

                    // Debug log to verify the rate is being set
                    Debug.Log($"Smoke emission rate set to: {emissionRate} for health percent: {healthPercent}");
                }
            }   
        }

        private void OnDeath()
        {
            // Set the flag so that this function is only called once.
            m_Dead = true;

            // Clean up smoke instance if it exists
            if (m_SmokeInstance != null)
            {
                Destroy(m_SmokeInstance);
                m_SmokeInstance = null;
            }

            // Turn the tank off.
            // gameObject.SetActive(false);
            isDissolving = true;
        }
        
        private void ApplyVignetteEffect()
        {
            if (m_Vignette != null)
            {
                // Calculate vignette intensity based on health percentage
                float healthPercent = m_CurrentHealth / m_StartingHealth;
                float damagePercent = 1f - healthPercent;
                
                // Set the vignette intensity proportional to health loss
                // More intense as health decreases
                float intensity = Mathf.Lerp(0, m_MaxVignetteIntensity, damagePercent);
                
                // Apply vignette effect
                m_Vignette.intensity.Override(intensity);
            }
        }
    }
}