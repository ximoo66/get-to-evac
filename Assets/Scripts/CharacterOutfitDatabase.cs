using UnityEngine;

public class CharacterOutfitDatabase : MonoBehaviour
{
    [System.Serializable]
    public class OutfitSet
    {
        [Header("Materials for this Outfit")]
        public Material skin;
        public Material face;
        public Material hair;
        public Material body;
        public Material glasses;
        public Material shoes;
        public Material belt;
    }

    [Header("All Available Outfit Sets")]
    public OutfitSet[] outfits;

    public int Count => outfits.Length;

    public OutfitSet Get(int index)
    {
        if (index < 0 || index >= outfits.Length)
        {
            Debug.LogError("Outfit index out of range.");
            return null;
        }

        return outfits[index];
    }
}
