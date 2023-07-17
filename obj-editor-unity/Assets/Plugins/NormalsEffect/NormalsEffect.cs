using UnityEngine;
using UnityEngine.Rendering;

namespace Ogxd
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class NormalsEffect : MonoBehaviour
    {
        public bool faceNormals = true;
        public bool vertexNormals = true;
        public bool vertexTangents = true;

        private Material effectMaterial;
        private Material EffectMaterial => effectMaterial ?? (effectMaterial = new Material(Shader.Find("Ogxd/Normals")));

        private Material blitMaterial;
        private Material BlitMaterial => blitMaterial ?? (blitMaterial = new Material(Shader.Find("Ogxd/Blit")));

        private Camera sourceCamera;
        private Camera SourceCamera => sourceCamera ?? (sourceCamera = GetComponent<Camera>());

        private RenderTexture effectTexture;
        private CommandBuffer effectCommands;

        private void RefreshCommandBuffer()
        {
            effectTexture = RenderTexture.GetTemporary(SourceCamera.pixelWidth, SourceCamera.pixelHeight, 16, UnityEngine.Experimental.Rendering.GraphicsFormat.B8G8R8A8_UNorm);

            if (effectCommands == null) {
                effectCommands = new CommandBuffer();
                effectCommands.name = "Normals Effect";
                sourceCamera.AddCommandBuffer(CameraEvent.AfterImageEffects, effectCommands);
            }

            effectCommands.Clear();
            effectCommands.SetRenderTarget(effectTexture);
            effectCommands.ClearRenderTarget(true, true, Color.clear);

            Renderer[] renderers = GameObject.FindObjectsOfType<Renderer>();
            for (int i = 0; i < renderers.Length; i++) {
                effectCommands.DrawRenderer(renderers[i], EffectMaterial);
            }

            effectCommands.Blit(effectTexture, sourceCamera.targetTexture, BlitMaterial);
        }

        public void OnPreRender()
        {
            // Updates render texture if viewport size changed
            if (effectTexture == null || effectTexture.width != sourceCamera.pixelWidth || effectTexture.height != sourceCamera.pixelHeight) {
                RefreshCommandBuffer();
            }
        }

        private void Start()
        {
            RefreshCommandBuffer();
        }

        private void OnGUI()
        {
            if (faceNormals)
                EffectMaterial.EnableKeyword("_FACENORMALS");
            else
                EffectMaterial.DisableKeyword("_FACENORMALS");

            if (vertexNormals)
                EffectMaterial.EnableKeyword("_VERTEXNORMALS");
            else
                EffectMaterial.DisableKeyword("_VERTEXNORMALS");

            if (vertexTangents)
                EffectMaterial.EnableKeyword("_VERTEXTANGENTS");
            else
                EffectMaterial.DisableKeyword("_VERTEXTANGENTS");
        }

        private void OnDisable()
        {
            RefreshCommandBuffer();
        }

        void OnDestroy()
        {
            if (effectCommands == null)
                return;

            sourceCamera.RemoveCommandBuffer(CameraEvent.AfterImageEffects, effectCommands);
            effectCommands.Clear();
        }
    }
}