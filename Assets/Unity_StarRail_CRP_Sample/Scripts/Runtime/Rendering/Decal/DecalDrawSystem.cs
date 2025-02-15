﻿using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity_StarRail_CRP_Sample
{
    public abstract class DecalDrawSystem
    {
        protected DecalEntityManager m_EntityManager;
        private Matrix4x4[] m_WorldToDecals;
        private Matrix4x4[] m_NormalToDecals;
        private Vector4[] m_LightColors;
        private float[] m_DecalLayerMasks;
        private ProfilingSampler m_Sampler;

        public Material overrideMaterial { get; set; }

        public DecalDrawSystem(string sampler, DecalEntityManager entityManager)
        {
            m_EntityManager = entityManager;

            m_WorldToDecals = new Matrix4x4[250];
            m_NormalToDecals = new Matrix4x4[250];
            m_LightColors = new Vector4[250];
            m_DecalLayerMasks = new float[250];

            m_Sampler = new ProfilingSampler(sampler);
        }

        public void Execute(CommandBuffer cmd)
        {
            using (new ProfilingScope(cmd, m_Sampler))
            {
                for (int i = 0; i < m_EntityManager.chunkCount; ++i)
                {
                    Execute(
                        cmd,
                        m_EntityManager.entityChunks[i],
                        m_EntityManager.cachedChunks[i],
                        m_EntityManager.objectDrawCallChunks[i],
                        m_EntityManager.entityChunks[i].count);
                }
            }
        }

        protected virtual Material GetMaterial(DecalEntityChunk decalEntityChunk) => decalEntityChunk.material;

        protected abstract int GetPassIndex(DecalCachedChunk decalCachedChunk);

        private void Execute(CommandBuffer cmd, DecalEntityChunk decalEntityChunk, DecalCachedChunk decalCachedChunk, DecalDrawCallChunk decalDrawCallChunk, int count)
        {
            decalCachedChunk.currentJobHandle.Complete();
            decalDrawCallChunk.currentJobHandle.Complete();

            Material material = GetMaterial(decalEntityChunk);
            int passIndex = GetPassIndex(decalCachedChunk);

            if (count == 0 || material == null)
                return;

            if (SystemInfo.supportsInstancing && material.enableInstancing)
            {
                DrawInstanced(cmd, decalEntityChunk, decalCachedChunk, decalDrawCallChunk, passIndex);
            }
            else
            {
                Draw(cmd, decalEntityChunk, decalCachedChunk, decalDrawCallChunk, passIndex);
            }
        }

        private void Draw(CommandBuffer cmd, DecalEntityChunk decalEntityChunk, DecalCachedChunk decalCachedChunk, DecalDrawCallChunk decalDrawCallChunk, int passIndex)
        {
            var mesh = m_EntityManager.decalProjectorMesh;
            var material = GetMaterial(decalEntityChunk);
            decalCachedChunk.propertyBlock.SetVector("unity_LightData", new Vector4(1, 1, 1, 0)); // GetMainLight requires z component to be set

            int subCallCount = decalDrawCallChunk.subCallCount;
            for (int i = 0; i < subCallCount; ++i)
            {
                var subCall = decalDrawCallChunk.subCalls[i];

                for (int j = subCall.start; j < subCall.end; ++j)
                {
                    decalCachedChunk.propertyBlock.SetMatrix("_NormalToWorld", decalDrawCallChunk.normalToDecals[j]);
                    decalCachedChunk.propertyBlock.SetFloat("_DecalLayerMaskFromDecal", decalDrawCallChunk.renderingLayerMasks[j]);
                    decalCachedChunk.propertyBlock.SetVector("_DecalLightColor", decalDrawCallChunk.lightColors[j]);
                    cmd.DrawMesh(mesh, decalDrawCallChunk.decalToWorlds[j], material, 0, passIndex, decalCachedChunk.propertyBlock);
                }
            }
        }

        private void DrawInstanced(CommandBuffer cmd, DecalEntityChunk decalEntityChunk, DecalCachedChunk decalCachedChunk, DecalDrawCallChunk decalDrawCallChunk, int passIndex)
        {
            var mesh = m_EntityManager.decalProjectorMesh;
            var material = GetMaterial(decalEntityChunk);
            decalCachedChunk.propertyBlock.SetVector("unity_LightData", new Vector4(1, 1, 1, 0)); // GetMainLight requires z component to be set

            int subCallCount = decalDrawCallChunk.subCallCount;
            for (int i = 0; i < subCallCount; ++i)
            {
                var subCall = decalDrawCallChunk.subCalls[i];

                var decalToWorldSlice = decalDrawCallChunk.decalToWorlds.Reinterpret<Matrix4x4>();
                NativeArray<Matrix4x4>.Copy(decalToWorldSlice, subCall.start, m_WorldToDecals, 0, subCall.count);

                var normalToWorldSlice = decalDrawCallChunk.normalToDecals.Reinterpret<Matrix4x4>();
                NativeArray<Matrix4x4>.Copy(normalToWorldSlice, subCall.start, m_NormalToDecals, 0, subCall.count);

                var decalLayerMaskSlice = decalDrawCallChunk.renderingLayerMasks.Reinterpret<float>();
                NativeArray<float>.Copy(decalLayerMaskSlice, subCall.start, m_DecalLayerMasks, 0, subCall.count);
                
                var lightColorSlice = decalDrawCallChunk.lightColors.Reinterpret<Vector4>();
                NativeArray<Vector4>.Copy(lightColorSlice, subCall.start, m_LightColors, 0, subCall.count);

                decalCachedChunk.propertyBlock.SetMatrixArray("_NormalToWorld", m_NormalToDecals);
                decalCachedChunk.propertyBlock.SetFloatArray("_DecalLayerMaskFromDecal", m_DecalLayerMasks);
                decalCachedChunk.propertyBlock.SetVectorArray("_DecalLightColor", m_LightColors);
                cmd.DrawMeshInstanced(mesh, 0, material, passIndex, m_WorldToDecals, subCall.end - subCall.start, decalCachedChunk.propertyBlock);
            }
        }

        public void Execute(in CameraData cameraData)
        {
            using (new ProfilingScope(null, m_Sampler))
            {
                for (int i = 0; i < m_EntityManager.chunkCount; ++i)
                {
                    Execute(
                        cameraData,
                        m_EntityManager.entityChunks[i],
                        m_EntityManager.cachedChunks[i],
                        m_EntityManager.objectDrawCallChunks[i],
                        m_EntityManager.entityChunks[i].count);
                }
            }
        }

        private void Execute(in CameraData cameraData, DecalEntityChunk decalEntityChunk, DecalCachedChunk decalCachedChunk, DecalDrawCallChunk decalDrawCallChunk, int count)
        {
            decalCachedChunk.currentJobHandle.Complete();
            decalDrawCallChunk.currentJobHandle.Complete();

            Material material = GetMaterial(decalEntityChunk);
            int passIndex = GetPassIndex(decalCachedChunk);

            if (count == 0 || passIndex == -1 || material == null)
                return;

            if (SystemInfo.supportsInstancing && material.enableInstancing)
            {
                DrawInstanced(cameraData, decalEntityChunk, decalCachedChunk, decalDrawCallChunk);
            }
            else
            {
                Draw(cameraData, decalEntityChunk, decalCachedChunk, decalDrawCallChunk);
            }
        }

        private void Draw(in CameraData cameraData, DecalEntityChunk decalEntityChunk, DecalCachedChunk decalCachedChunk, DecalDrawCallChunk decalDrawCallChunk)
        {
            var mesh = m_EntityManager.decalProjectorMesh;
            var material = GetMaterial(decalEntityChunk);
            int subCallCount = decalDrawCallChunk.subCallCount;
            for (int i = 0; i < subCallCount; ++i)
            {
                var subCall = decalDrawCallChunk.subCalls[i];

                for (int j = subCall.start; j < subCall.end; ++j)
                {
                    decalCachedChunk.propertyBlock.SetMatrix("_NormalToWorld", decalDrawCallChunk.normalToDecals[j]);
                    decalCachedChunk.propertyBlock.SetFloat("_DecalLayerMaskFromDecal", decalDrawCallChunk.renderingLayerMasks[j]);
                    Graphics.DrawMesh(mesh, decalCachedChunk.decalToWorlds[j], material, decalCachedChunk.layerMasks[j], cameraData.camera, 0, decalCachedChunk.propertyBlock);
                }
            }
        }

        private void DrawInstanced(in CameraData cameraData, DecalEntityChunk decalEntityChunk, DecalCachedChunk decalCachedChunk, DecalDrawCallChunk decalDrawCallChunk)
        {
            var mesh = m_EntityManager.decalProjectorMesh;
            var material = GetMaterial(decalEntityChunk);
            decalCachedChunk.propertyBlock.SetVector("unity_LightData", new Vector4(1, 1, 1, 0)); // GetMainLight requires z component to be set

            int subCallCount = decalDrawCallChunk.subCallCount;
            for (int i = 0; i < subCallCount; ++i)
            {
                var subCall = decalDrawCallChunk.subCalls[i];

                var decalToWorldSlice = decalDrawCallChunk.decalToWorlds.Reinterpret<Matrix4x4>();
                NativeArray<Matrix4x4>.Copy(decalToWorldSlice, subCall.start, m_WorldToDecals, 0, subCall.count);

                var normalToWorldSlice = decalDrawCallChunk.normalToDecals.Reinterpret<Matrix4x4>();
                NativeArray<Matrix4x4>.Copy(normalToWorldSlice, subCall.start, m_NormalToDecals, 0, subCall.count);

                var decalLayerMaskSlice = decalDrawCallChunk.renderingLayerMasks.Reinterpret<float>();
                NativeArray<float>.Copy(decalLayerMaskSlice, subCall.start, m_DecalLayerMasks, 0, subCall.count);

                decalCachedChunk.propertyBlock.SetMatrixArray("_NormalToWorld", m_NormalToDecals);
                decalCachedChunk.propertyBlock.SetFloatArray("_DecalLayerMaskFromDecal", m_DecalLayerMasks);
                Graphics.DrawMeshInstanced(mesh, 0, material,
                    m_WorldToDecals, subCall.count, decalCachedChunk.propertyBlock, ShadowCastingMode.On, true, 0, cameraData.camera);
            }
        }
    }
}