using System.Collections.Generic;
using UnityEngine;

namespace Shion.SDK.Editor
{
    [CreateAssetMenu(fileName = "MediationCompatibilityConfig", menuName = "Shion SDK/Mediation Compatibility Config")]
    public class MediationCompatibilityConfig : ScriptableObject
    {
        [Tooltip("AdMob network key (slug/folder) maps to AppLovin NetworkId. Used to match adapters from both plugins.")]
        public List<NetworkMapping> NetworkMappings = new List<NetworkMapping>();

        [Tooltip("Custom parse patterns for AdMob Dependencies.xml when default derivation fails (e.g. Meta uses 'facebook' not 'metaaudiencenetwork').")]
        public List<AdMobParsePattern> AdMobParsePatterns = new List<AdMobParsePattern>();

        [System.Serializable]
        public class NetworkMapping
        {
            [Tooltip("AdMob key: IntegrationSlug or MediationFolderName (e.g. meta, chartboost)")]
            public string AdMobKey;

            [Tooltip("AppLovin NetworkId from MaxSdk/Mediation folder (e.g. facebook, chartboost)")]
            public string AppLovinNetworkId;
        }

        [System.Serializable]
        public class AdMobParsePattern
        {
            [Tooltip("Key: IntegrationSlug, MediationFolderName, or package suffix (e.g. meta, chartboost)")]
            public string Key;

            [Tooltip("Regex pattern for Android artifact (e.g. com.google.ads.mediation:facebook)")]
            public string AndroidPattern;

            [Tooltip("iOS pod name (e.g. GoogleMobileAdsMediationFacebook)")]
            public string IosPodName;
        }

        private static MediationCompatibilityConfig _instance;

        public static MediationCompatibilityConfig Instance
        {
            get
            {
#if UNITY_EDITOR
                if (_instance != null) return _instance;
                var guids = UnityEditor.AssetDatabase.FindAssets("t:MediationCompatibilityConfig");
                if (guids.Length > 0)
                {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<MediationCompatibilityConfig>(path);
                }
                return _instance;
#else
                return null;
#endif
            }
        }
    }
}
