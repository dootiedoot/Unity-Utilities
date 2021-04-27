using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetMeshColor : MonoBehaviour
{
    public Color color = Color.white;
    public bool randomColor = false;

    // Start is called before the first frame update
    void Start()
    {
        SetColor();
    }

    private void SetColor()
    {
        if (randomColor)
        {
            color = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
        }

        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        properties.SetColor("_BaseColor", color);

        //  parent
        MeshRenderer thisRend = GetComponent<MeshRenderer>();
        if (thisRend)
        {
            thisRend.SetPropertyBlock(properties);
        }

        //  children
        MeshRenderer[] childRenderers = GetComponentsInChildren<MeshRenderer>();
        foreach (var childRend in childRenderers)
        {
            childRend.SetPropertyBlock(properties);
        }
    }
}
