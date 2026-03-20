using UnityEditor;
using UnityEngine;

namespace Shion.SDK.Editor
{
    public static class MediationCompatibilityConfigEditor
    {
        private const string DefaultAssetPath = "Assets/ShionSDK/Resources/MediationCompatibilityConfig.asset";

        [MenuItem("Shion SDK/Mediation Compatibility/Create Config Asset")]
        public static void CreateConfigAsset()
        {
            var config = AssetDatabase.LoadAssetAtPath<MediationCompatibilityConfig>(DefaultAssetPath);
            if (config != null)
            {
                Selection.activeObject = config;
                EditorGUIUtility.PingObject(config);
                return;
            }
            config = ScriptableObject.CreateInstance<MediationCompatibilityConfig>();
            config.NetworkMappings = new System.Collections.Generic.List<MediationCompatibilityConfig.NetworkMapping>
            {
                new MediationCompatibilityConfig.NetworkMapping { AdMobKey = "meta", AppLovinNetworkId = "facebook" },
                new MediationCompatibilityConfig.NetworkMapping { AdMobKey = "metaaudiencenetwork", AppLovinNetworkId = "facebook" },
                new MediationCompatibilityConfig.NetworkMapping { AdMobKey = "facebook", AppLovinNetworkId = "facebook" },
                new MediationCompatibilityConfig.NetworkMapping { AdMobKey = "liftoffmonetize", AppLovinNetworkId = "vungle" },
                new MediationCompatibilityConfig.NetworkMapping { AdMobKey = "vungle", AppLovinNetworkId = "vungle" },
                new MediationCompatibilityConfig.NetworkMapping { AdMobKey = "unityads", AppLovinNetworkId = "unityads" },
                new MediationCompatibilityConfig.NetworkMapping { AdMobKey = "unity", AppLovinNetworkId = "unityads" },
                new MediationCompatibilityConfig.NetworkMapping { AdMobKey = "chartboost", AppLovinNetworkId = "chartboost" },
                new MediationCompatibilityConfig.NetworkMapping { AdMobKey = "ironsource", AppLovinNetworkId = "ironsource" },
                new MediationCompatibilityConfig.NetworkMapping { AdMobKey = "inmobi", AppLovinNetworkId = "inmobi" },
                new MediationCompatibilityConfig.NetworkMapping { AdMobKey = "applovin", AppLovinNetworkId = "applovin" },
                new MediationCompatibilityConfig.NetworkMapping { AdMobKey = "dtxchange", AppLovinNetworkId = "dtexchange" },
                new MediationCompatibilityConfig.NetworkMapping { AdMobKey = "fyber", AppLovinNetworkId = "dtexchange" }
            };
            config.AdMobParsePatterns = new System.Collections.Generic.List<MediationCompatibilityConfig.AdMobParsePattern>
            {
                new MediationCompatibilityConfig.AdMobParsePattern { Key = "meta", AndroidPattern = "com.google.ads.mediation:facebook", IosPodName = "GoogleMobileAdsMediationFacebook" },
                new MediationCompatibilityConfig.AdMobParsePattern { Key = "metaaudiencenetwork", AndroidPattern = "com.google.ads.mediation:facebook", IosPodName = "GoogleMobileAdsMediationFacebook" },
                new MediationCompatibilityConfig.AdMobParsePattern { Key = "facebook", AndroidPattern = "com.google.ads.mediation:facebook", IosPodName = "GoogleMobileAdsMediationFacebook" },
                new MediationCompatibilityConfig.AdMobParsePattern { Key = "chartboost", AndroidPattern = "com.google.ads.mediation:chartboost", IosPodName = "GoogleMobileAdsMediationChartboost" }
            };
            if (!AssetDatabase.IsValidFolder("Assets/ShionSDK/Resources"))
            {
                if (!AssetDatabase.IsValidFolder("Assets/ShionSDK"))
                    AssetDatabase.CreateFolder("Assets", "ShionSDK");
                if (!AssetDatabase.IsValidFolder("Assets/ShionSDK/Resources"))
                    AssetDatabase.CreateFolder("Assets/ShionSDK", "Resources");
            }
            AssetDatabase.CreateAsset(config, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }
    }
}
