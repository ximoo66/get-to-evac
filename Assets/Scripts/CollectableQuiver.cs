using System;
using UnityEngine;

public class CollectableQuiver : Collectable
{
    public override event Action<Collectable> OnCollected;

    public override int Collect()
    {
        // only the Server is allowed to collect the collectables
        // if client then hide it and return "0" as value
        if (!IsServer)
        {
            DoHide(true);
            return 0;
        }

        // if there was someone already faster within the frame
        if (isCollected) { return 0; }

        // if not yet then claim the item, hide it and return the value
        isCollected = true;
        DoHide(true);

        // trigger event to inform that this one was picked
        OnCollected?.Invoke(this);

        return collectableValue;
    }
}
