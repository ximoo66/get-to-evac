using UnityEngine;

public class CharacterVisualApplier : MonoBehaviour
{
    [Header("Drag Character Mesh Parts Here")]
    [SerializeField] private SkinnedMeshRenderer bodyRenderer;
    [SerializeField] private SkinnedMeshRenderer handRenderer;
    [SerializeField] private SkinnedMeshRenderer noseRenderer;
    [SerializeField] private SkinnedMeshRenderer earsRenderer;
    [SerializeField] private SkinnedMeshRenderer glassesRenderer;
    [SerializeField] private SkinnedMeshRenderer faceRenderer;
    [SerializeField] private SkinnedMeshRenderer hairRenderer;
    [SerializeField] private SkinnedMeshRenderer shoesRenderer;
    [SerializeField] private SkinnedMeshRenderer beltRenderer;

    public void Apply(CharacterOutfitDatabase.OutfitSet outfit)
    {
        if (outfit == null) return;

        // Apply materials
        if (bodyRenderer)  bodyRenderer.material = outfit.body;
        if (faceRenderer)  faceRenderer.material = outfit.face;
        if (earsRenderer)  earsRenderer.material = outfit.skin;   // ears = skin
        if (noseRenderer)  noseRenderer.material = outfit.skin;   // nose = skin
        if (handRenderer)  handRenderer.material = outfit.skin;   // hand = skin
        if (hairRenderer)  hairRenderer.material = outfit.hair;
        if (shoesRenderer) shoesRenderer.material = outfit.shoes;
        if (beltRenderer)  beltRenderer.material = outfit.belt;

        // If body clothing is a separate material, assign here instead:
        // bodyRenderer.material = outfit.body;
    }
}
