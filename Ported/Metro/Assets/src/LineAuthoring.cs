﻿using System.Collections.Generic;
using src;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[DisallowMultipleComponent]
[RequiresEntityConversion]
[ConverterVersion("christianw", 4)]
public class LineAuthoring : MonoBehaviour, IConvertGameObjectToEntity
{
    // Add fields to your component here. Remember that:
    //
    // * The purpose of this class is to store data for authoring purposes - it is not for use while the game is
    //   running.
    // 
    // * Traditional Unity serialization rules apply: fields must be public or marked with [SerializeField], and
    //   must be one of the supported types.
    //
    // For example,
    //    public float scale;

    [SerializeField]
    int m_LineIndex = 0;

    public const float k_StepSize = 0.1f;

    Entity m_Entity;
    EntityManager m_EntityManager;
    
    public Mesh RailMarkerMesh = null;
    public Color RailMarkerColor = Color.white;
    public Vector3 RailMarkerScale = new Vector3(0.1f, 1f, 0.025f);


    struct GenerationStep
    {
        public GenerationStep(int nextWaypointIdx, Transform currentWaypoint)
        {
            NextWaypointIdx = nextWaypointIdx;
            CurrentWaypoint = currentWaypoint;
            NextWaypoint = currentWaypoint.parent.GetChild(nextWaypointIdx);
            FullDistanceBetweenWaypoints = NextWaypoint.position - currentWaypoint.position;
        }

        public readonly int NextWaypointIdx;
        public readonly Transform CurrentWaypoint;
        public readonly Transform NextWaypoint;
        public readonly float3 FullDistanceBetweenWaypoints;

        public bool HasNext
        {
            get
            {
                return CurrentWaypoint.parent.childCount > NextWaypointIdx + 1;
            }
        }

        public GenerationStep Next
        {
            get {
                return new GenerationStep(NextWaypointIdx + 1, NextWaypoint);
            }
        }
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        // Call methods on 'dstManager' to create runtime components on 'entity' here. Remember that:
        //
        // * You can add more than one component to the entity. It's also OK to not add any at all.
        //
        // * If you want to create more than one entity from the data in this class, use the 'conversionSystem'
        //   to do it, instead of adding entities through 'dstManager' directly.
        //
        // For example,
        //   dstManager.AddComponentData(entity, new Unity.Transforms.Scale { Value = scale });

        dstManager.AddComponentData(entity, new Line { ID = m_LineIndex });
        var buffer = new List<LinePositionBufferElement>();
        var generationStep = new GenerationStep(1, transform.GetChild(0));
        float3 currentPosition = generationStep.CurrentWaypoint.position;
        float3 currentHeading = math.normalize(generationStep.FullDistanceBetweenWaypoints);
        buffer.Add(currentPosition);

        bool done = false;
        while (!done)
        {
            var diff = (float3)generationStep.NextWaypoint.position - currentPosition;
            while(math.length(diff) < k_StepSize)
            {
                if (generationStep.HasNext)
                {
                    generationStep = generationStep.Next;
                    currentPosition = generationStep.CurrentWaypoint.position;
                    diff = (float3)generationStep.NextWaypoint.position - currentPosition;
                }
                else
                {

                    done = true;
                    break;
                }
            }
            if(!done)
            {
                var t = 1 - (math.length(diff) - k_StepSize) / math.length(generationStep.FullDistanceBetweenWaypoints);
                currentHeading = math.normalize(math.lerp(currentHeading, math.normalize(diff), t));
                var step = currentHeading * k_StepSize;
                currentPosition += step;
                buffer.Add(currentPosition);
            }
        }
        m_Entity = entity;
        m_EntityManager = dstManager;
        var b = dstManager.AddBuffer<LinePositionBufferElement>(entity);
        foreach (var bufferElement in buffer)
        {
            b.Add(bufferElement);
        }
        
        var material = new Material(Shader.Find("Standard"));
        material.color = RailMarkerColor;

        for (var index = 0; index < buffer.Count; index++)
        {
            var bufferElement = buffer[index];
            var markerEntity = conversionSystem.CreateAdditionalEntity(gameObject);

            var direction = index + 1 < buffer.Count ? buffer[index + 1].Value - bufferElement.Value : bufferElement.Value - buffer[index - 1].Value;
            
            dstManager.AddComponentData(markerEntity, new Unity.Transforms.Translation()
            {
                Value = bufferElement.Value
            });
            dstManager.AddComponentData(markerEntity, new Unity.Transforms.Rotation()
            {
                Value = Quaternion.LookRotation(direction)
            });
            dstManager.AddComponentData(markerEntity, new Unity.Transforms.LocalToWorld());
            dstManager.AddComponentData(markerEntity, new Unity.Transforms.NonUniformScale()
            {
                Value = RailMarkerScale
            });
            dstManager.AddComponentData(markerEntity, new SimpleMeshRenderer
            {
                Mesh = RailMarkerMesh,
                Material = material,
            });
        }
    }

    //void Update()
    //{
    //    m_EntityManager = World.Active.EntityManager;
    //    if (m_EntityManager != null)
    //    {
    //        m_Entity = m_EntityManager.CreateEntityQuery(typeof(Line)).ToEntityArray(Unity.Collections.Allocator.TempJob)[0];
    //        var buffer = m_EntityManager.GetBuffer<LinePositionBufferElement>(m_Entity);
    //        for (int i = 0; i <  buffer.Length-1; i++)
    //        {
    //            Debug.DrawLine(buffer[i].Value, buffer[i + 1].Value,Color.red);
    //        }
    //    }
    //}
}