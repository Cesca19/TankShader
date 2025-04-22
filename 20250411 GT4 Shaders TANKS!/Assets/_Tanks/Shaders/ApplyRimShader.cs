using UnityEngine;

public class ApplyRimShader : MonoBehaviour
{
    [SerializeField] private Material rimMaterial;

    [ContextMenu("Apply Rim Material To All")]
    public void ApplyToAll()
    {
        if (rimMaterial == null)
            {
            Debug.LogError("Please assign a rim material first!");
            return;
        }
        
        int count = 0;
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        foreach (Renderer renderer in renderers)
        {
            // Skip UI elements and particles
            if (renderer.gameObject.layer == LayerMask.NameToLayer("UI") || 
                renderer is ParticleSystemRenderer)
                continue;
                
            renderer.material = rimMaterial;
            count++;
        }
        
        Debug.Log($"Applied rim material to {count} objects");
    }
}