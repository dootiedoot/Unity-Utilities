using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public class PrefabEntityExample : MonoBehaviour, IConvertGameObjectToEntity 
{
    public GameObject prefabGameObject;

    private Entity prefabEntity;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) {
        using (BlobAssetStore blobAssetStore = new BlobAssetStore()) {
            prefabEntity = GameObjectConversionUtility.ConvertGameObjectHierarchy(prefabGameObject, GameObjectConversionSettings.FromWorld(dstManager.World, blobAssetStore));
        }
    }
}
