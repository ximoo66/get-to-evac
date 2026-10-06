using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.Xml;
using System;

public abstract class Collectable : NetworkBehaviour
{
    public abstract event Action<Collectable> OnCollected;

    protected int collectableValue = 1;
    // to avoid that two players get it at the same frame. Only the first one will get it.
    protected bool isCollected = false;

    protected void DoHide(bool hide)
    {
        (gameObject.GetComponent(typeof(Collider)) as Collider).enabled = !hide;



        // hide them all
        foreach (Renderer childRenderer in transform.GetComponentsInChildren<Renderer>())
        {
            childRenderer.enabled = !hide;
        }
    }

    public void SetValue(int newValue)
    {
        collectableValue = newValue;
    }

    // abstract: no implementation here
    public abstract int Collect();
}
