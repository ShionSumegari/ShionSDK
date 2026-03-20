using System.Collections.Generic;
namespace Shion.SDK.Editor
{
    public static class AdMobAdapterConfig
    {
        public const string GoogleMobileAdsPath = "Assets/GoogleMobileAds";
        public const string IntegrateAdSourcesBaseUrl = "https://developers.google.com/admob/unity/mediation/";
        public class AdapterDef
        {
            public string DisplayName { get; set; }
            public string PackageId { get; set; }
            public string IntegrationSlug { get; set; }
            public string MediationFolderName { get; set; }
            public string AndroidAdapterVersion { get; set; }
            public string IosAdapterVersion { get; set; }
            /// <summary>AppLovin folder name in MaxSdk/Mediation (e.g. Facebook, Chartboost). Null = use MediationFolderName.</summary>
            public string AppLovinNetworkId { get; set; }
            /// <summary>Override for Android artifact pattern (e.g. com.google.ads.mediation:facebook). Null = derive from PackageId.</summary>
            public string AndroidParsePattern { get; set; }
            /// <summary>Override for iOS pod name (e.g. GoogleMobileAdsMediationFacebook). Null = derive from MediationFolderName.</summary>
            public string IosParsePattern { get; set; }
            public string IntegrationUrl =>
                $"{IntegrateAdSourcesBaseUrl}{IntegrationSlug}";
        }
        public static readonly IReadOnlyList<AdapterDef> AllAdapters = new List<AdapterDef>
        {
            new AdapterDef { DisplayName = "AppLovin", PackageId = "com.google.ads.mobile.mediation.applovin", IntegrationSlug = "applovin", MediationFolderName = "AppLovin", AppLovinNetworkId = "AppLovin" },
            new AdapterDef { DisplayName = "Chartboost", PackageId = "com.google.ads.mobile.mediation.chartboost", IntegrationSlug = "chartboost", MediationFolderName = "Chartboost", AppLovinNetworkId = "Chartboost", AndroidParsePattern = "com.google.ads.mediation:chartboost", IosParsePattern = "GoogleMobileAdsMediationChartboost" },
            new AdapterDef { DisplayName = "DT Exchange", PackageId = "com.google.ads.mobile.mediation.dtexchange", IntegrationSlug = "fyber", MediationFolderName = "DTExchange", AppLovinNetworkId = "Fyber" },
            new AdapterDef { DisplayName = "i-mobile", PackageId = "com.google.ads.mobile.mediation.imobile", IntegrationSlug = "imobile", MediationFolderName = "IMobile", AppLovinNetworkId = "IMobile" },
            new AdapterDef { DisplayName = "InMobi", PackageId = "com.google.ads.mobile.mediation.inmobi", IntegrationSlug = "inmobi", MediationFolderName = "InMobi", AppLovinNetworkId = "InMobi" },
            new AdapterDef { DisplayName = "IronSource", PackageId = "com.google.ads.mobile.mediation.ironsource", IntegrationSlug = "ironsource", MediationFolderName = "IronSource", AppLovinNetworkId = "IronSource" },
            new AdapterDef { DisplayName = "Liftoff Monetize", PackageId = "com.google.ads.mobile.mediation.liftoffmonetize", IntegrationSlug = "vungle", MediationFolderName = "LiftoffMonetize", AppLovinNetworkId = "Vungle" },
            new AdapterDef { DisplayName = "LINE Ads Network", PackageId = "com.google.ads.mobile.mediation.line", IntegrationSlug = "line", MediationFolderName = "Line", AppLovinNetworkId = "Line" },
            new AdapterDef { DisplayName = "maio", PackageId = "com.google.ads.mobile.mediation.maio", IntegrationSlug = "maio", MediationFolderName = "Maio", AppLovinNetworkId = "Maio" },
            new AdapterDef { DisplayName = "Meta Audience Network", PackageId = "com.google.ads.mobile.mediation.metaaudiencenetwork", IntegrationSlug = "meta", MediationFolderName = "MetaAudienceNetwork", AppLovinNetworkId = "Facebook", AndroidParsePattern = "com.google.ads.mediation:facebook", IosParsePattern = "GoogleMobileAdsMediationFacebook" },
            new AdapterDef { DisplayName = "Mintegral", PackageId = "com.google.ads.mobile.mediation.mintegral", IntegrationSlug = "mintegral", MediationFolderName = "Mintegral", AppLovinNetworkId = "Mintegral" },
            new AdapterDef { DisplayName = "Moloco", PackageId = "com.google.ads.mobile.mediation.moloco", IntegrationSlug = "moloco", MediationFolderName = "Moloco", AppLovinNetworkId = "Moloco" },
            new AdapterDef { DisplayName = "myTarget", PackageId = "com.google.ads.mobile.mediation.mytarget", IntegrationSlug = "mytarget", MediationFolderName = "MyTarget", AppLovinNetworkId = "MyTarget" },
            new AdapterDef { DisplayName = "Pangle", PackageId = "com.google.ads.mobile.mediation.pangle", IntegrationSlug = "pangle", MediationFolderName = "Pangle", AppLovinNetworkId = "Pangle" },
            new AdapterDef { DisplayName = "PubMatic OpenWrap", PackageId = "com.google.ads.mobile.mediation.pubmatic", IntegrationSlug = "pubmatic", MediationFolderName = "PubMatic", AppLovinNetworkId = "PubMatic" },
            new AdapterDef { DisplayName = "Unity Ads", PackageId = "com.google.ads.mobile.mediation.unity", IntegrationSlug = "unity", MediationFolderName = "UnityAds", AppLovinNetworkId = "UnityAds" }
        };
    }
}