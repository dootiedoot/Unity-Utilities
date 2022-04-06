using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

public class SceneLoader : MonoBehaviour
{
    [Header("User Assigned")]
    public List<AssetReference> scenesToLoad = new List<AssetReference>();

    // Start is called before the first frame update
    void Start()
    {
        LoadSceneList();
    }

    //  Load each scene additivly in order
	private void LoadSceneList()
	{
        foreach (AssetReference scene in scenesToLoad)
        {
            //  load scene, skip if scene is already loaded
            Addressables.LoadResourceLocationsAsync(scene).Completed += (loc) =>
            {
                bool isSceneLoaded = SceneManager.GetSceneByPath(loc.Result[0].InternalId).isLoaded;
                if (isSceneLoaded == false)
                {
                    scene.LoadSceneAsync(LoadSceneMode.Additive).Completed += OnSceneLoaded;
                }
            };
        }
    }

    private void OnSceneLoaded(AsyncOperationHandle<SceneInstance> obj)
    {
        if (obj.Status == AsyncOperationStatus.Succeeded)
        {
            Debug.Log(obj.Result.Scene.name + " scene loaded.");
        }
    }
}
