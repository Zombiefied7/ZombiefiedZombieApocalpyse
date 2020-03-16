using System;
using Harmony;
using UnityEngine;

namespace Zombiefied
{
    // Token: 0x02000014 RID: 20
    [HarmonyPatch(typeof(Graphics))]
    [HarmonyPatch("DrawMeshImpl")]
    public class Graphics_DrawMeshImpl_Patch
    {
        // Token: 0x06000060 RID: 96 RVA: 0x00004385 File Offset: 0x00002585
        public static void Prefix(ref Matrix4x4 matrix)
        {
            matrix.m00 *= Graphics_DrawMeshImpl_Patch.scale;
            matrix.m22 *= Graphics_DrawMeshImpl_Patch.scale;
        }

        // Token: 0x06000061 RID: 97 RVA: 0x000043A5 File Offset: 0x000025A5
        public static void Reset()
        {
            Graphics_DrawMeshImpl_Patch.scale = 1f;
        }

        // Token: 0x0400005B RID: 91
        public static float scale = 1f;
    }
}

