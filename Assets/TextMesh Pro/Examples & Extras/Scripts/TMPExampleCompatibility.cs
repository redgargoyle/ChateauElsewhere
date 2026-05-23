using System.Collections.Generic;
using UnityEngine.TextCore;

namespace TMPro.Examples
{
    internal static class TMPExampleCompatibility
    {
        public static void SetKerning(TMP_Text text, bool enabled)
        {
            if (text == null)
            {
                return;
            }

            List<OTL_FeatureTag> fontFeatures = new List<OTL_FeatureTag>(text.fontFeatures);

            if (enabled)
            {
                if (!fontFeatures.Contains(OTL_FeatureTag.kern))
                {
                    fontFeatures.Add(OTL_FeatureTag.kern);
                }
            }
            else
            {
                fontFeatures.RemoveAll(feature => feature == OTL_FeatureTag.kern);
            }

            text.fontFeatures = fontFeatures;
        }
    }
}
