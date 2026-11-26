using Mongscape.Domain;
using Mongscape.Presentation.Map;
using UnityEngine;

namespace Mongscape.Editor
{
    public static partial class MongscapeArtUpgrade
    {
        // The prison stair has no closing door any more and carries no entrance decor: it is reused inside the corridor.
        private static void AddClosingPrisonEntry(GameObject root,int type)
        {
            var chunk=root.GetComponent<MongscapeMapChunk>();
            foreach(var old in root.GetComponentsInChildren<ClosingSlideGate>(true))Object.DestroyImmediate(old);
            chunk.SlideGates=new ClosingSlideGate[0];
        }
    }
}
