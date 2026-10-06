using UnityEngine;

public class Arrow : MonoBehaviour
{
    [SerializeField] private float lifeTime = 10f;
    [SerializeField] private int projectileDamage = 10;
    private ulong ownerID;
    private bool isServerOwner = false;

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    public void SetOwner(ulong _ownerID, bool _isServerOwner)
    {
        ownerID = _ownerID;
        isServerOwner = _isServerOwner;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Ignore collisions without a valid root object
        Transform targetRoot = collision.transform.root;
        if (targetRoot == null) return;

        GameObject target = targetRoot.gameObject;

        // Only react to Player or Zombie tags
        if (target.CompareTag("Zombie") || target.CompareTag("Player"))
        {
            // Apply damage if a Health component exists on the collided object (or its root)
            Health health = target.GetComponent<Health>() ?? collision.gameObject.GetComponent<Health>();
            if (health != null)
            {
                health.TakeDamage(projectileDamage);
            }

            // If there's an animator on the target (root or the collider), set IsStunned = true
            Animator animator = target.GetComponent<Animator>() ?? collision.gameObject.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetBool("IsStunned", true);
            }

            Debug.Log($"Hit: {target.name} Damage: {projectileDamage}");

            // Optionally play hit VFX/sound here

            // Destroy the arrow GameObject after a short delay so any audio/VFX can play (0.01f to destroy immediately)
            Destroy(gameObject);
        }
        else
        {
            // Non-character hits: destroy arrow immediately
            Destroy(gameObject);
        }
    }
}
